using ECommerce.AuthService.Data;
using ECommerce.AuthService.Models;
using ECommerce.AuthService.Services;
using ECommerce.Shared.Contracts.DTOs;
using ECommerce.Shared.Contracts.Events;
using ECommerce.Shared.Infrastructure.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BCrypt.Net;

namespace ECommerce.AuthService.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _userRepo;
        private readonly IRefreshTokenRepository _tokenRepo;
        private readonly IOtpRepository _otpRepo;
        private readonly IJwtService _jwt;
        private readonly IServiceBusPublisher _bus;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IUserRepository userRepo,
            IRefreshTokenRepository tokenRepo,
            IOtpRepository otpRepo,
            IJwtService jwt,
            IServiceBusPublisher bus,
            ILogger<AuthController> logger)
        {
            _userRepo = userRepo;
            _tokenRepo = tokenRepo;
            _otpRepo = otpRepo;
            _jwt = jwt;
            _bus = bus;
            _logger = logger;
        }

        // ─── Health Check ─────────────────────────────────────
        [HttpGet("/health")]
        public IActionResult Health() =>
            Ok(new { status = "UP", service = "auth-service", time = DateTime.UtcNow });

        // ─── Register ─────────────────────────────────────────
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            // ✅ Deep Validation
            var errors = ValidateRegisterRequest(req);
            if (errors.Any())
                return BadRequest(ApiResponse<string>.ValidationFail(errors));

            // ✅ Email duplicate check
            if (await _userRepo.EmailExistsAsync(req.Email))
                return Conflict(ApiResponse<string>.Fail("Email already registered"));

            // ✅ Phone duplicate check
            if (await _userRepo.PhoneExistsAsync(req.Phone))
                return Conflict(ApiResponse<string>.Fail("Phone number already registered"));

            // ✅ Password hash — BCrypt work factor 12
            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = req.Name.Trim(),
                Email = req.Email.ToLower().Trim(),
                Phone = req.Phone.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 12),
                Role = req.Role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _userRepo.CreateAsync(user);

            // ✅ Email verification OTP generate karo
            await GenerateOtpAsync(user.Email, "verify_email");

            // ✅ Service Bus pe event publish karo — Notification Service email bhejegi
            try
            {
                await _bus.PublishAsync("user.registered", new UserRegisteredEvent
                {
                    UserId = user.Id.ToString(),
                    Email = user.Email,
                    Name = user.Name,
                    Phone = user.Phone,
                    Role = user.Role
                });
            }
            catch (Exception ex)
            {
                // Event fail hone pe registration fail nahi hogi
                _logger.LogWarning(ex, "Failed to publish UserRegisteredEvent for {UserId}", user.Id);
            }

            _logger.LogInformation("New user registered: {UserId} | {Email} | Role: {Role}",
                user.Id, user.Email, user.Role);

            return StatusCode(201, ApiResponse<object>.Ok(
                new { userId = user.Id, message = "Registration successful! Check email for OTP." }));
        }

        // ─── Login ────────────────────────────────────────────
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(ApiResponse<string>.Fail("Email and password are required"));

            var user = await _userRepo.GetByEmailAsync(req.Email);

            // ✅ Lockout check
            if (user != null && user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
                return Unauthorized(ApiResponse<string>.Fail(
                    $"Account locked until {user.LockoutEnd:HH:mm} UTC. Too many failed attempts."));

            // ✅ Password verify — timing-safe BCrypt comparison
            if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            {
                if (user != null)
                    await _userRepo.UpdateLoginInfoAsync(user.Id, success: false);

                return Unauthorized(ApiResponse<string>.Fail("Invalid email or password"));
            }

            if (!user.IsActive)
                return Unauthorized(ApiResponse<string>.Fail("Account is deactivated"));

            // ✅ Login success — update info
            await _userRepo.UpdateLoginInfoAsync(user.Id, success: true);

            // ✅ Tokens generate karo
            var accessToken = _jwt.GenerateAccessToken(user);
            var refreshToken = _jwt.GenerateRefreshToken(req.IpAddress, req.UserAgent, user.Id);

            await _tokenRepo.CreateAsync(refreshToken);

            _logger.LogInformation("User logged in: {UserId} | {Email}", user.Id, user.Email);

            return Ok(ApiResponse<AuthResponse>.Ok(new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                AccessTokenExpiry = DateTime.UtcNow.AddMinutes(60),
                UserId = user.Id.ToString(),
                Name = user.Name,
                Email = user.Email,
                Role = user.Role.ToString()
            }));
        }

        // ─── Refresh Token ────────────────────────────────────
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Token))
                return BadRequest(ApiResponse<string>.Fail("Token is required"));

            var existing = await _tokenRepo.GetByTokenAsync(req.Token);

            if (existing == null || existing.IsRevoked || existing.ExpiresAt < DateTime.UtcNow)
                return Unauthorized(ApiResponse<string>.Fail("Invalid or expired refresh token"));

            var user = await _userRepo.GetByIdAsync(existing.UserId);
            if (user == null || !user.IsActive)
                return Unauthorized(ApiResponse<string>.Fail("User not found or inactive"));

            // ✅ Rotate — purana revoke karo, naya banao
            var newRefreshToken = _jwt.GenerateRefreshToken(null, null, user.Id);
            await _tokenRepo.RevokeAsync(existing.Token, newRefreshToken.Token);
            await _tokenRepo.CreateAsync(newRefreshToken);

            var newAccessToken = _jwt.GenerateAccessToken(user);

            return Ok(ApiResponse<AuthResponse>.Ok(new AuthResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken.Token,
                AccessTokenExpiry = DateTime.UtcNow.AddMinutes(60),
                UserId = user.Id.ToString(),
                Name = user.Name,
                Email = user.Email,
                Role = user.Role.ToString()
            }));
        }

        // ─── Forgot Password ──────────────────────────────────
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email))
                return BadRequest(ApiResponse<string>.Fail("Email is required"));

            // ✅ Security: Email exist karta hai ya nahi — same response dono case mein
            var user = await _userRepo.GetByEmailAsync(req.Email);
            if (user != null)
                await GenerateOtpAsync(user.Email, "reset_password");

            return Ok(ApiResponse<string>.Ok("If email is registered, reset OTP has been sent"));
        }

        // ─── Reset Password ───────────────────────────────────
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
        {
            var errors = ValidatePassword(req.NewPassword, req.ConfirmPassword);
            if (errors.Any())
                return BadRequest(ApiResponse<string>.ValidationFail(errors));

            // ✅ OTP validate karo
            var otp = await _otpRepo.GetValidOtpAsync(req.Email, req.OtpCode, "reset_password");
            if (otp == null)
            {
                await _otpRepo.IncrementAttemptsAsync(req.Email, "reset_password");
                return BadRequest(ApiResponse<string>.Fail("Invalid or expired OTP"));
            }

            var user = await _userRepo.GetByEmailAsync(req.Email);
            if (user == null)
                return NotFound(ApiResponse<string>.Fail("User not found"));

            // ✅ Password update karo
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword, workFactor: 12);
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;

            await _userRepo.UpdateAsync(user);
            await _otpRepo.MarkUsedAsync(otp.Id);

            // ✅ Security: Password reset pe saare refresh tokens revoke karo
            await _tokenRepo.RevokeAllForUserAsync(user.Id);

            _logger.LogInformation("Password reset for: {UserId}", user.Id);
            return Ok(ApiResponse<string>.Ok("Password reset successful"));
        }

        // ─── Change Password (Login ke baad) ─────────────────
        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
        {
            var errors = ValidatePassword(req.NewPassword, req.ConfirmPassword);
            if (errors.Any())
                return BadRequest(ApiResponse<string>.ValidationFail(errors));

            var userId = User.FindFirst("userId")?.Value;
            if (userId == null) return Unauthorized();

            var user = await _userRepo.GetByIdAsync(Guid.Parse(userId));
            if (user == null) return NotFound();

            if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
                return BadRequest(ApiResponse<string>.Fail("Current password is incorrect"));

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword, workFactor: 12);
            await _userRepo.UpdateAsync(user);

            return Ok(ApiResponse<string>.Ok("Password changed successfully"));
        }

        // ─── Verify OTP ───────────────────────────────────────
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest req)
        {
            var otp = await _otpRepo.GetValidOtpAsync(req.Target, req.OtpCode, req.Purpose);

            if (otp == null)
            {
                await _otpRepo.IncrementAttemptsAsync(req.Target, req.Purpose);
                return BadRequest(ApiResponse<string>.Fail("Invalid or expired OTP"));
            }

            await _otpRepo.MarkUsedAsync(otp.Id);

            // ✅ Email verify karo agar OTP purpose verify_email hai
            if (req.Purpose == "verify_email")
            {
                var user = await _userRepo.GetByEmailAsync(req.Target);
                if (user != null)
                {
                    user.IsEmailVerified = true;
                    await _userRepo.UpdateAsync(user);
                }
            }

            return Ok(ApiResponse<string>.Ok("OTP verified successfully"));
        }

        // ─── Logout ───────────────────────────────────────────
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest req)
        {
            if (!string.IsNullOrWhiteSpace(req.Token))
                await _tokenRepo.RevokeAsync(req.Token);

            return Ok(ApiResponse<string>.Ok("Logged out successfully"));
        }

        // ─── Private Helpers ──────────────────────────────────
        private async Task GenerateOtpAsync(string target, string purpose)
        {
            // Purane OTPs invalid karo
            await _otpRepo.InvalidateOldOtpsAsync(target, purpose);

            var otp = new OtpRecord
            {
                Id = Guid.NewGuid(),
                Target = target.ToLower(),
                OtpCode = Random.Shared.Next(100000, 999999).ToString(),
                Purpose = purpose,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                CreatedAt = DateTime.UtcNow
            };

            await _otpRepo.CreateAsync(otp);

            _logger.LogInformation("OTP generated for {Target} | Purpose: {Purpose}", target, purpose);
        }

        private static List<string> ValidateRegisterRequest(RegisterRequest req)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(req.Name))
                errors.Add("Name is required");
            else if (req.Name.Length > 100)
                errors.Add("Name cannot exceed 100 characters");

            if (string.IsNullOrWhiteSpace(req.Email))
                errors.Add("Email is required");
            else if (!Regex.IsMatch(req.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                errors.Add("Invalid email format");

            if (string.IsNullOrWhiteSpace(req.Phone))
                errors.Add("Phone is required");
            else if (!Regex.IsMatch(req.Phone, @"^[6-9]\d{9}$"))
                errors.Add("Invalid Indian phone number (must start with 6-9, 10 digits)");

            errors.AddRange(ValidatePassword(req.Password, req.ConfirmPassword));

            return errors;
        }

        private static List<string> ValidatePassword(string password, string confirmPassword)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(password))
            { errors.Add("Password is required"); return errors; }

            if (password.Length < 8)
                errors.Add("Password must be at least 8 characters");
            if (!Regex.IsMatch(password, @"[A-Z]"))
                errors.Add("Password must contain at least one uppercase letter");
            if (!Regex.IsMatch(password, @"[a-z]"))
                errors.Add("Password must contain at least one lowercase letter");
            if (!Regex.IsMatch(password, @"[0-9]"))
                errors.Add("Password must contain at least one number");
            if (!Regex.IsMatch(password, @"[^a-zA-Z0-9]"))
                errors.Add("Password must contain at least one special character");
            if (password != confirmPassword)
                errors.Add("Passwords do not match");

            return errors;
        }
    }
}