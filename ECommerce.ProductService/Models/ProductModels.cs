using ECommerce.Shared.Contracts.Enums;

namespace ECommerce.ProductService.Models
{
    // =============================================
    // DATABASE ENTITIES
    // =============================================

    public class Category
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid? ParentId { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<Category> Children { get; set; } = new();
    }

    public class Product
    {
        public Guid Id { get; set; }
        public Guid SellerId { get; set; }
        public Guid CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ShortDescription { get; set; }
        public decimal MRP { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal Discount { get; set; }
        public int Stock { get; set; }
        public int MinStock { get; set; } = 5;
        public string SKU { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public decimal? Weight { get; set; }
        public ProductStatus Status { get; set; } = ProductStatus.Draft;
        public bool IsFeatured { get; set; }
        public decimal AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int TotalSold { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? CategoryName { get; set; }
        public List<ProductImage> Images { get; set; } = new();
        public List<ProductVariant> Variants { get; set; } = new();
    }

    public class ProductImage
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? AltText { get; set; }
        public bool IsPrimary { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ProductVariant
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public decimal PriceAdjust { get; set; }
        public int Stock { get; set; }
        public string? SKU { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ProductReview
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public Guid? OrderId { get; set; }
        public int Rating { get; set; }
        public string? Title { get; set; }
        public string? Comment { get; set; }
        public bool IsVerified { get; set; }
        public bool IsApproved { get; set; }
        public int HelpfulCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // =============================================
    // REQUEST DTOs
    // =============================================

    public class CreateProductRequest
    {
        public Guid CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ShortDescription { get; set; }
        public decimal MRP { get; set; }
        public decimal SellingPrice { get; set; }
        public int Stock { get; set; }
        public int MinStock { get; set; } = 5;
        public string SKU { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public decimal? Weight { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public List<CreateVariantRequest> Variants { get; set; } = new();
    }

    public class UpdateProductRequest
    {
        public Guid CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ShortDescription { get; set; }
        public decimal MRP { get; set; }
        public decimal SellingPrice { get; set; }
        public int MinStock { get; set; }
        public string? Brand { get; set; }
        public decimal? Weight { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
    }

    public class CreateVariantRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public decimal PriceAdjust { get; set; }
        public int Stock { get; set; }
        public string? SKU { get; set; }
    }

    public class UpdateStockRequest
    {
        public int Quantity { get; set; }
        public string Operation { get; set; } = "reduce";
    }

    public class AddReviewRequest
    {
        public int Rating { get; set; }
        public string? Title { get; set; }
        public string? Comment { get; set; }
        public Guid? OrderId { get; set; }
    }

    public class CreateCategoryRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid? ParentId { get; set; }
        public string? ImageUrl { get; set; }
        public int SortOrder { get; set; }
    }

    public class ProductSearchRequest
    {
        public string? Query { get; set; }
        public Guid? CategoryId { get; set; }
        public string? Brand { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public decimal? MinRating { get; set; }
        public bool? InStock { get; set; }
        public string SortBy { get; set; } = "createdAt";
        public string SortOrder { get; set; } = "desc";
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public Guid? SellerId { get; set; }
    }
}