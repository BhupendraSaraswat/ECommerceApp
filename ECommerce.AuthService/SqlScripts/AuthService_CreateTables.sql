-- =============================================
-- AuthService Database Tables
-- SQL Server pe run karo ek baar
-- =============================================

-- Database banao
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'ECommerce_Auth')
BEGIN
    CREATE DATABASE ECommerce_Auth
END
GO

USE ECommerce_Auth
GO

-- =============================================
-- Users Table
-- =============================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
BEGIN
    CREATE TABLE Users (
        Id                    UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
        Name                  NVARCHAR(100)       NOT NULL,
        Email                 NVARCHAR(200)       NOT NULL,
        PasswordHash          NVARCHAR(500)       NOT NULL,
        Phone                 NVARCHAR(15)        NOT NULL,
        Role                  INT                 NOT NULL DEFAULT 0,  -- 0=Buyer, 1=Seller, 2=Admin
        IsEmailVerified       BIT                 NOT NULL DEFAULT 0,
        IsPhoneVerified       BIT                 NOT NULL DEFAULT 0,
        IsActive              BIT                 NOT NULL DEFAULT 1,
        LastLoginAt           DATETIME2           NULL,
        FailedLoginAttempts   INT                 NOT NULL DEFAULT 0,
        LockoutEnd            DATETIME2           NULL,
        IsDeleted             BIT                 NOT NULL DEFAULT 0,
        CreatedAt             DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt             DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy             NVARCHAR(100)       NULL,
        UpdatedBy             NVARCHAR(100)       NULL
    )

    -- Email unique index
    CREATE UNIQUE INDEX IX_Users_Email ON Users(Email) WHERE IsDeleted = 0
    -- Phone index
    CREATE INDEX IX_Users_Phone ON Users(Phone)

    PRINT 'Users table created'
END
GO

-- =============================================
-- RefreshTokens Table
-- =============================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='RefreshTokens' AND xtype='U')
BEGIN
    CREATE TABLE RefreshTokens (
        Id                UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
        UserId            UNIQUEIDENTIFIER    NOT NULL,
        Token             NVARCHAR(500)       NOT NULL,
        ExpiresAt         DATETIME2           NOT NULL,
        IsRevoked         BIT                 NOT NULL DEFAULT 0,
        ReplacedByToken   NVARCHAR(500)       NULL,
        IpAddress         NVARCHAR(50)        NULL,
        UserAgent         NVARCHAR(500)       NULL,
        IsDeleted         BIT                 NOT NULL DEFAULT 0,
        CreatedAt         DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt         DATETIME2           NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId)
            REFERENCES Users(Id)
    )

    CREATE INDEX IX_RefreshTokens_Token  ON RefreshTokens(Token)
    CREATE INDEX IX_RefreshTokens_UserId ON RefreshTokens(UserId)

    PRINT 'RefreshTokens table created'
END
GO

-- =============================================
-- OtpRecords Table
-- =============================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='OtpRecords' AND xtype='U')
BEGIN
    CREATE TABLE OtpRecords (
        Id          UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
        Target      NVARCHAR(200)       NOT NULL,  -- email ya phone
        OtpCode     NVARCHAR(10)        NOT NULL,
        Purpose     NVARCHAR(50)        NOT NULL,  -- verify_email, reset_password, login_otp
        ExpiresAt   DATETIME2           NOT NULL,
        IsUsed      BIT                 NOT NULL DEFAULT 0,
        Attempts    INT                 NOT NULL DEFAULT 0,
        IsDeleted   BIT                 NOT NULL DEFAULT 0,
        CreatedAt   DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt   DATETIME2           NOT NULL DEFAULT GETUTCDATE()
    )

    CREATE INDEX IX_OtpRecords_Target_Purpose ON OtpRecords(Target, Purpose)

    PRINT 'OtpRecords table created'
END
GO

PRINT 'All AuthService tables created successfully!'
GO