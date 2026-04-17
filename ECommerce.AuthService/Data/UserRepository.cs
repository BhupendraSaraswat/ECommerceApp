using ECommerce.AuthService.Models;
using ECommerce.Shared.Contracts.Enums;
using ECommerce.Shared.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ECommerce.AuthService.Data
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetByEmailAsync(string email);
        Task<bool> EmailExistsAsync(string email);
        Task<bool> PhoneExistsAsync(string phone);
        Task<Guid> CreateAsync(User user);
        Task UpdateAsync(User user);
        Task UpdateLoginInfoAsync(Guid userId, bool success);
    }

    public interface IRefreshTokenRepository
    {
        Task<RefreshToken?> GetByTokenAsync(string token);
        Task CreateAsync(RefreshToken token);
        Task RevokeAsync(string token, string? replacedBy = null);
        Task RevokeAllForUserAsync(Guid userId);
    }

    public interface IOtpRepository
    {
        Task CreateAsync(OtpRecord otp);
        Task<OtpRecord?> GetValidOtpAsync(string target, string otpCode, string purpose);
        Task InvalidateOldOtpsAsync(string target, string purpose);
        Task MarkUsedAsync(Guid otpId);
        Task IncrementAttemptsAsync(string target, string purpose);
    }

    // ======================================================
    // USER REPOSITORY
    // ======================================================
    public class UserRepository : BaseRepository, IUserRepository
    {
        public UserRepository(IDbConnectionFactory factory, ILogger<UserRepository> logger)
            : base(factory, logger) { }

        // ✅ ID se user dhundo
        public async Task<User?> GetByIdAsync(Guid id)
        {
            const string sql = @"
                SELECT Id, Name, Email, PasswordHash, Phone, Role,
                       IsEmailVerified, IsPhoneVerified, IsActive,
                       LastLoginAt, FailedLoginAttempts, LockoutEnd,
                       CreatedAt, UpdatedAt
                FROM   Users
                WHERE  Id = @Id AND IsDeleted = 0";

            return await QuerySingleAsync(sql, MapUser,
                new[] { new SqlParameter("@Id", id) });
        }

        // ✅ Email se user dhundo (login ke liye)
        public async Task<User?> GetByEmailAsync(string email)
        {
            const string sql = @"
                SELECT Id, Name, Email, PasswordHash, Phone, Role,
                       IsEmailVerified, IsPhoneVerified, IsActive,
                       LastLoginAt, FailedLoginAttempts, LockoutEnd,
                       CreatedAt, UpdatedAt
                FROM   Users
                WHERE  Email = @Email AND IsDeleted = 0";

            return await QuerySingleAsync(sql, MapUser,
                new[] { new SqlParameter("@Email", email.ToLower()) });
        }

        // ✅ Email unique check
        public async Task<bool> EmailExistsAsync(string email)
        {
            const string sql = "SELECT COUNT(1) FROM Users WHERE Email = @Email AND IsDeleted = 0";
            var count = await ExecuteScalarAsync<int>(sql,
                new[] { new SqlParameter("@Email", email.ToLower()) });
            return count > 0;
        }

        // ✅ Phone unique check
        public async Task<bool> PhoneExistsAsync(string phone)
        {
            const string sql = "SELECT COUNT(1) FROM Users WHERE Phone = @Phone AND IsDeleted = 0";
            var count = await ExecuteScalarAsync<int>(sql,
                new[] { new SqlParameter("@Phone", phone) });
            return count > 0;
        }

        // ✅ Naya user banao — ID return karta hai
        public async Task<Guid> CreateAsync(User user)
        {
            const string sql = @"
                INSERT INTO Users
                    (Id, Name, Email, PasswordHash, Phone, Role,
                     IsEmailVerified, IsPhoneVerified, IsActive,
                     FailedLoginAttempts, CreatedAt, UpdatedAt)
                VALUES
                    (@Id, @Name, @Email, @PasswordHash, @Phone, @Role,
                     @IsEmailVerified, @IsPhoneVerified, @IsActive,
                     0, GETUTCDATE(), GETUTCDATE())";

            var parameters = new[]
            {
                new SqlParameter("@Id",              user.Id),
                new SqlParameter("@Name",            user.Name),
                new SqlParameter("@Email",           user.Email.ToLower()),
                new SqlParameter("@PasswordHash",    user.PasswordHash),
                new SqlParameter("@Phone",           user.Phone),
                new SqlParameter("@Role",            (int)user.Role),
                new SqlParameter("@IsEmailVerified", user.IsEmailVerified),
                new SqlParameter("@IsPhoneVerified", user.IsPhoneVerified),
                new SqlParameter("@IsActive",        user.IsActive)
            };

            await ExecuteAsync(sql, parameters);
            return user.Id;
        }

        // ✅ User update karo (password change, email verify, etc.)
        public async Task UpdateAsync(User user)
        {
            const string sql = @"
                UPDATE Users SET
                    Name                = @Name,
                    PasswordHash        = @PasswordHash,
                    IsEmailVerified     = @IsEmailVerified,
                    IsPhoneVerified     = @IsPhoneVerified,
                    IsActive            = @IsActive,
                    FailedLoginAttempts = @FailedLoginAttempts,
                    LockoutEnd          = @LockoutEnd,
                    UpdatedAt           = GETUTCDATE()
                WHERE Id = @Id AND IsDeleted = 0";

            var parameters = new[]
            {
                new SqlParameter("@Id",                  user.Id),
                new SqlParameter("@Name",                user.Name),
                new SqlParameter("@PasswordHash",        user.PasswordHash),
                new SqlParameter("@IsEmailVerified",     user.IsEmailVerified),
                new SqlParameter("@IsPhoneVerified",     user.IsPhoneVerified),
                new SqlParameter("@IsActive",            user.IsActive),
                new SqlParameter("@FailedLoginAttempts", user.FailedLoginAttempts),
                new SqlParameter("@LockoutEnd",
                    user.LockoutEnd.HasValue ? (object)user.LockoutEnd.Value : DBNull.Value)
            };

            await ExecuteAsync(sql, parameters);
        }

        // ✅ Login ke baad info update karo (LastLoginAt, failed attempts)
        public async Task UpdateLoginInfoAsync(Guid userId, bool success)
        {
            string sql = success
                ? @"UPDATE Users SET
                        LastLoginAt         = GETUTCDATE(),
                        FailedLoginAttempts = 0,
                        LockoutEnd          = NULL,
                        UpdatedAt           = GETUTCDATE()
                    WHERE Id = @Id"
                : @"UPDATE Users SET
                        FailedLoginAttempts = FailedLoginAttempts + 1,
                        LockoutEnd = CASE
                            WHEN FailedLoginAttempts >= 4
                            THEN DATEADD(MINUTE, 15, GETUTCDATE())
                            ELSE NULL
                        END,
                        UpdatedAt = GETUTCDATE()
                    WHERE Id = @Id";

            await ExecuteAsync(sql, new[] { new SqlParameter("@Id", userId) });
        }

        // ✅ SQL Reader se User object banao
        private static User MapUser(SqlDataReader r) => new User
        {
            Id = GetGuid(r, "Id"),
            Name = GetString(r, "Name"),
            Email = GetString(r, "Email"),
            PasswordHash = GetString(r, "PasswordHash"),
            Phone = GetString(r, "Phone"),
            Role = (UserRole)GetInt(r, "Role"),
            IsEmailVerified = GetBool(r, "IsEmailVerified"),
            IsPhoneVerified = GetBool(r, "IsPhoneVerified"),
            IsActive = GetBool(r, "IsActive"),
            LastLoginAt = GetValueOrDefault<DateTime?>(r, "LastLoginAt"),
            FailedLoginAttempts = GetInt(r, "FailedLoginAttempts"),
            LockoutEnd = GetValueOrDefault<DateTime?>(r, "LockoutEnd"),
            CreatedAt = GetDateTime(r, "CreatedAt"),
            UpdatedAt = GetDateTime(r, "UpdatedAt")
        };
    }

    // ======================================================
    // REFRESH TOKEN REPOSITORY
    // ======================================================
    public class RefreshTokenRepository : BaseRepository, IRefreshTokenRepository
    {
        public RefreshTokenRepository(IDbConnectionFactory factory,
            ILogger<RefreshTokenRepository> logger) : base(factory, logger) { }

        public async Task<RefreshToken?> GetByTokenAsync(string token)
        {
            const string sql = @"
                SELECT Id, UserId, Token, ExpiresAt, IsRevoked,
                       ReplacedByToken, IpAddress, UserAgent, CreatedAt
                FROM   RefreshTokens
                WHERE  Token = @Token AND IsDeleted = 0";

            return await QuerySingleAsync(sql, r => new RefreshToken
            {
                Id = GetGuid(r, "Id"),
                UserId = GetGuid(r, "UserId"),
                Token = GetString(r, "Token"),
                ExpiresAt = GetDateTime(r, "ExpiresAt"),
                IsRevoked = GetBool(r, "IsRevoked"),
                ReplacedByToken = GetValueOrDefault<string?>(r, "ReplacedByToken"),
                IpAddress = GetValueOrDefault<string?>(r, "IpAddress"),
                UserAgent = GetValueOrDefault<string?>(r, "UserAgent"),
                CreatedAt = GetDateTime(r, "CreatedAt")
            }, new[] { new SqlParameter("@Token", token) });
        }

        public async Task CreateAsync(RefreshToken token)
        {
            const string sql = @"
                INSERT INTO RefreshTokens
                    (Id, UserId, Token, ExpiresAt, IsRevoked, IpAddress, UserAgent, CreatedAt, UpdatedAt)
                VALUES
                    (@Id, @UserId, @Token, @ExpiresAt, 0, @IpAddress, @UserAgent, GETUTCDATE(), GETUTCDATE())";

            await ExecuteAsync(sql, new[]
            {
                new SqlParameter("@Id",        token.Id),
                new SqlParameter("@UserId",    token.UserId),
                new SqlParameter("@Token",     token.Token),
                new SqlParameter("@ExpiresAt", token.ExpiresAt),
                new SqlParameter("@IpAddress",
                    token.IpAddress != null ? (object)token.IpAddress : DBNull.Value),
                new SqlParameter("@UserAgent",
                    token.UserAgent != null ? (object)token.UserAgent : DBNull.Value)
            });
        }

        public async Task RevokeAsync(string token, string? replacedBy = null)
        {
            const string sql = @"
                UPDATE RefreshTokens SET
                    IsRevoked       = 1,
                    ReplacedByToken = @ReplacedBy,
                    UpdatedAt       = GETUTCDATE()
                WHERE Token = @Token";

            await ExecuteAsync(sql, new[]
            {
                new SqlParameter("@Token",      token),
                new SqlParameter("@ReplacedBy",
                    replacedBy != null ? (object)replacedBy : DBNull.Value)
            });
        }

        public async Task RevokeAllForUserAsync(Guid userId)
        {
            const string sql = @"
                UPDATE RefreshTokens SET
                    IsRevoked = 1,
                    UpdatedAt = GETUTCDATE()
                WHERE UserId = @UserId AND IsRevoked = 0";

            await ExecuteAsync(sql, new[] { new SqlParameter("@UserId", userId) });
        }
    }

    // ======================================================
    // OTP REPOSITORY
    // ======================================================
    public class OtpRepository : BaseRepository, IOtpRepository
    {
        public OtpRepository(IDbConnectionFactory factory, ILogger<OtpRepository> logger)
            : base(factory, logger) { }

        public async Task CreateAsync(OtpRecord otp)
        {
            const string sql = @"
                INSERT INTO OtpRecords
                    (Id, Target, OtpCode, Purpose, ExpiresAt, IsUsed, Attempts, CreatedAt, UpdatedAt)
                VALUES
                    (@Id, @Target, @OtpCode, @Purpose, @ExpiresAt, 0, 0, GETUTCDATE(), GETUTCDATE())";

            await ExecuteAsync(sql, new[]
            {
                new SqlParameter("@Id",        otp.Id),
                new SqlParameter("@Target",    otp.Target.ToLower()),
                new SqlParameter("@OtpCode",   otp.OtpCode),
                new SqlParameter("@Purpose",   otp.Purpose),
                new SqlParameter("@ExpiresAt", otp.ExpiresAt)
            });
        }

        // ✅ Valid OTP dhundo (used nahi, expire nahi)
        public async Task<OtpRecord?> GetValidOtpAsync(string target, string otpCode, string purpose)
        {
            const string sql = @"
                SELECT Id, Target, OtpCode, Purpose, ExpiresAt, IsUsed, Attempts, CreatedAt
                FROM   OtpRecords
                WHERE  Target   = @Target
                  AND  OtpCode  = @OtpCode
                  AND  Purpose  = @Purpose
                  AND  IsUsed   = 0
                  AND  IsDeleted = 0
                  AND  ExpiresAt > GETUTCDATE()";

            return await QuerySingleAsync(sql, r => new OtpRecord
            {
                Id = GetGuid(r, "Id"),
                Target = GetString(r, "Target"),
                OtpCode = GetString(r, "OtpCode"),
                Purpose = GetString(r, "Purpose"),
                ExpiresAt = GetDateTime(r, "ExpiresAt"),
                IsUsed = GetBool(r, "IsUsed"),
                Attempts = GetInt(r, "Attempts"),
                CreatedAt = GetDateTime(r, "CreatedAt")
            }, new[]
            {
                new SqlParameter("@Target",  target.ToLower()),
                new SqlParameter("@OtpCode", otpCode),
                new SqlParameter("@Purpose", purpose)
            });
        }

        // ✅ Purane OTPs invalid karo (naya generate karne se pehle)
        public async Task InvalidateOldOtpsAsync(string target, string purpose)
        {
            const string sql = @"
                UPDATE OtpRecords SET
                    IsUsed    = 1,
                    UpdatedAt = GETUTCDATE()
                WHERE Target  = @Target
                  AND Purpose = @Purpose
                  AND IsUsed  = 0";

            await ExecuteAsync(sql, new[]
            {
                new SqlParameter("@Target",  target.ToLower()),
                new SqlParameter("@Purpose", purpose)
            });
        }

        public async Task MarkUsedAsync(Guid otpId)
        {
            const string sql = @"
                UPDATE OtpRecords SET IsUsed = 1, UpdatedAt = GETUTCDATE()
                WHERE Id = @Id";

            await ExecuteAsync(sql, new[] { new SqlParameter("@Id", otpId) });
        }

        public async Task IncrementAttemptsAsync(string target, string purpose)
        {
            const string sql = @"
                UPDATE OtpRecords SET
                    Attempts  = Attempts + 1,
                    UpdatedAt = GETUTCDATE()
                WHERE Target  = @Target
                  AND Purpose = @Purpose
                  AND IsUsed  = 0";

            await ExecuteAsync(sql, new[]
            {
                new SqlParameter("@Target",  target.ToLower()),
                new SqlParameter("@Purpose", purpose)
            });
        }
    }
}