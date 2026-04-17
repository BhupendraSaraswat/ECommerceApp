using ECommerce.AuthService.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ECommerce.AuthService.Services
{
    public interface IJwtService
    {
        string       GenerateAccessToken(User user);
        RefreshToken GenerateRefreshToken(string? ipAddress, string? userAgent, Guid userId);
        ClaimsPrincipal? ValidateToken(string token);
    }

    public class JwtService : IJwtService
    {
        private readonly IConfiguration _config;

        public JwtService(IConfiguration config) => _config = config;

        // ✅ JWT Access Token generate karo — 60 minute valid
        public string GenerateAccessToken(User user)
        {
            var secret  = _config["Jwt:Secret"]
                ?? throw new InvalidOperationException("Jwt:Secret not configured");

            var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var minutes = int.Parse(_config["Jwt:AccessTokenMinutes"] ?? "60");
            var expiry  = DateTime.UtcNow.AddMinutes(minutes);

            // ✅ Token mein user ki info claims ke roop mein store hoti hai
            var claims = new[]
            {
                new Claim("userId", user.Id.ToString()),
                new Claim("email",  user.Email),
                new Claim("name",   user.Name),
                new Claim("role",   user.Role.ToString()),
                new Claim("phone",  user.Phone),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64)
            };

            var token = new JwtSecurityToken(
                issuer             : _config["Jwt:Issuer"],
                audience           : _config["Jwt:Audience"],
                claims             : claims,
                expires            : expiry,
                signingCredentials : creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // ✅ Refresh Token generate karo — 7 din valid, random bytes se
        public RefreshToken GenerateRefreshToken(string? ipAddress, string? userAgent, Guid userId)
        {
            return new RefreshToken
            {
                Id        = Guid.NewGuid(),
                UserId    = userId,
                Token     = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                IpAddress = ipAddress,
                UserAgent = userAgent,
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            };
        }

        // ✅ Token validate karo — Refresh flow mein use hota hai
        public ClaimsPrincipal? ValidateToken(string token)
        {
            try
            {
                var secret  = _config["Jwt:Secret"]!;
                var handler = new JwtSecurityTokenHandler();

                var result = handler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey        = new SymmetricSecurityKey(
                                                 Encoding.UTF8.GetBytes(secret)),
                    ValidateIssuer          = true,
                    ValidIssuer             = _config["Jwt:Issuer"],
                    ValidateAudience        = true,
                    ValidAudience           = _config["Jwt:Audience"],
                    ValidateLifetime        = false  // Expired token bhi validate karo refresh ke liye
                }, out _);

                return result;
            }
            catch
            {
                return null;
            }
        }
    }
}