-- =============================================
-- ProductService Database Tables
-- SQL Server pe run karo ek baar
-- =============================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'ECommerce_Products')
BEGIN
    CREATE DATABASE ECommerce_Products
END
GO

USE ECommerce_Products
GO

-- =============================================
-- Categories Table
-- =============================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Categories' AND xtype='U')
BEGIN
    CREATE TABLE Categories (
        Id          UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
        Name        NVARCHAR(100)       NOT NULL,
        Slug        NVARCHAR(100)       NOT NULL,
        Description NVARCHAR(500)       NULL,
        ParentId    UNIQUEIDENTIFIER    NULL,
        ImageUrl    NVARCHAR(500)       NULL,
        IsActive    BIT                 NOT NULL DEFAULT 1,
        SortOrder   INT                 NOT NULL DEFAULT 0,
        IsDeleted   BIT                 NOT NULL DEFAULT 0,
        CreatedAt   DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt   DATETIME2           NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT FK_Categories_Parent FOREIGN KEY (ParentId)
            REFERENCES Categories(Id)
    )

    CREATE UNIQUE INDEX IX_Categories_Slug ON Categories(Slug) WHERE IsDeleted = 0
    CREATE INDEX IX_Categories_ParentId    ON Categories(ParentId)

    INSERT INTO Categories (Id, Name, Slug, Description, SortOrder) VALUES
        (NEWID(), 'Electronics',   'electronics',   'Electronic items',     1),
        (NEWID(), 'Fashion',       'fashion',        'Clothing and fashion', 2),
        (NEWID(), 'Home & Living', 'home-living',    'Home decor and more',  3),
        (NEWID(), 'Sports',        'sports',         'Sports and fitness',   4),
        (NEWID(), 'Books',         'books',          'Books and stationery', 5)

    PRINT 'Categories table created'
END
GO

-- =============================================
-- Products Table
-- =============================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Products' AND xtype='U')
BEGIN
    CREATE TABLE Products (
        Id               UNIQUEIDENTIFIER  NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
        SellerId         UNIQUEIDENTIFIER  NOT NULL,
        CategoryId       UNIQUEIDENTIFIER  NOT NULL,
        Name             NVARCHAR(200)     NOT NULL,
        Slug             NVARCHAR(200)     NOT NULL,
        Description      NVARCHAR(MAX)     NULL,
        ShortDescription NVARCHAR(500)     NULL,
        MRP              DECIMAL(18,2)     NOT NULL,
        SellingPrice     DECIMAL(18,2)     NOT NULL,
        Discount         DECIMAL(5,2)      NOT NULL DEFAULT 0,
        Stock            INT               NOT NULL DEFAULT 0,
        MinStock         INT               NOT NULL DEFAULT 5,
        SKU              NVARCHAR(100)     NOT NULL,
        Brand            NVARCHAR(100)     NULL,
        Weight           DECIMAL(10,2)     NULL,
        Status           INT               NOT NULL DEFAULT 0,
        IsFeatured       BIT               NOT NULL DEFAULT 0,
        AverageRating    DECIMAL(3,2)      NOT NULL DEFAULT 0,
        TotalReviews     INT               NOT NULL DEFAULT 0,
        TotalSold        INT               NOT NULL DEFAULT 0,
        MetaTitle        NVARCHAR(200)     NULL,
        MetaDescription  NVARCHAR(500)     NULL,
        IsDeleted        BIT               NOT NULL DEFAULT 0,
        CreatedAt        DATETIME2         NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt        DATETIME2         NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT FK_Products_Category FOREIGN KEY (CategoryId)
            REFERENCES Categories(Id)
    )

    CREATE UNIQUE INDEX IX_Products_Slug     ON Products(Slug) WHERE IsDeleted = 0
    CREATE UNIQUE INDEX IX_Products_SKU      ON Products(SKU)  WHERE IsDeleted = 0
    CREATE INDEX IX_Products_SellerId        ON Products(SellerId)
    CREATE INDEX IX_Products_CategoryId      ON Products(CategoryId)
    CREATE INDEX IX_Products_Status          ON Products(Status)

    PRINT 'Products table created'
END
GO

-- =============================================
-- ProductImages Table
-- =============================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ProductImages' AND xtype='U')
BEGIN
    CREATE TABLE ProductImages (
        Id        UNIQUEIDENTIFIER  NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
        ProductId UNIQUEIDENTIFIER  NOT NULL,
        ImageUrl  NVARCHAR(500)     NOT NULL,
        AltText   NVARCHAR(200)     NULL,
        IsPrimary BIT               NOT NULL DEFAULT 0,
        SortOrder INT               NOT NULL DEFAULT 0,
        IsDeleted BIT               NOT NULL DEFAULT 0,
        CreatedAt DATETIME2         NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT FK_ProductImages_Product FOREIGN KEY (ProductId)
            REFERENCES Products(Id)
    )

    CREATE INDEX IX_ProductImages_ProductId ON ProductImages(ProductId)
    PRINT 'ProductImages table created'
END
GO

-- =============================================
-- ProductVariants Table
-- =============================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ProductVariants' AND xtype='U')
BEGIN
    CREATE TABLE ProductVariants (
        Id          UNIQUEIDENTIFIER  NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
        ProductId   UNIQUEIDENTIFIER  NOT NULL,
        Name        NVARCHAR(100)     NOT NULL,
        Value       NVARCHAR(100)     NOT NULL,
        PriceAdjust DECIMAL(18,2)     NOT NULL DEFAULT 0,
        Stock       INT               NOT NULL DEFAULT 0,
        SKU         NVARCHAR(100)     NULL,
        IsDeleted   BIT               NOT NULL DEFAULT 0,
        CreatedAt   DATETIME2         NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT FK_ProductVariants_Product FOREIGN KEY (ProductId)
            REFERENCES Products(Id)
    )

    CREATE INDEX IX_ProductVariants_ProductId ON ProductVariants(ProductId)
    PRINT 'ProductVariants table created'
END
GO

-- =============================================
-- ProductReviews Table
-- =============================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ProductReviews' AND xtype='U')
BEGIN
    CREATE TABLE ProductReviews (
        Id           UNIQUEIDENTIFIER  NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
        ProductId    UNIQUEIDENTIFIER  NOT NULL,
        UserId       UNIQUEIDENTIFIER  NOT NULL,
        UserName     NVARCHAR(100)     NOT NULL,
        OrderId      UNIQUEIDENTIFIER  NULL,
        Rating       INT               NOT NULL,
        Title        NVARCHAR(200)     NULL,
        Comment      NVARCHAR(1000)    NULL,
        IsVerified   BIT               NOT NULL DEFAULT 0,
        IsApproved   BIT               NOT NULL DEFAULT 1,
        HelpfulCount INT               NOT NULL DEFAULT 0,
        IsDeleted    BIT               NOT NULL DEFAULT 0,
        CreatedAt    DATETIME2         NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt    DATETIME2         NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT FK_ProductReviews_Product FOREIGN KEY (ProductId)
            REFERENCES Products(Id),
        CONSTRAINT CK_Rating CHECK (Rating BETWEEN 1 AND 5)
    )

    CREATE INDEX IX_ProductReviews_ProductId ON ProductReviews(ProductId)
    CREATE INDEX IX_ProductReviews_UserId    ON ProductReviews(UserId)
    PRINT 'ProductReviews table created'
END
GO

PRINT 'All ProductService tables created successfully!'
GO