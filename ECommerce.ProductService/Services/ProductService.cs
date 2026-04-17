using ECommerce.ProductService.Data;
using ECommerce.ProductService.Models;
using ECommerce.Shared.Contracts.Enums;
using System.Text.RegularExpressions;

namespace ECommerce.ProductService.Services
{
    public interface IProductBusinessService
    {
        Task<(Guid Id, string Slug)> CreateProductAsync(CreateProductRequest req, Guid sellerId);
        Task UpdateProductAsync(Guid id, UpdateProductRequest req, Guid sellerId);
        Task AddImageAsync(Guid productId, string imageUrl, bool isPrimary, Guid sellerId);
        Task AddReviewAsync(Guid productId, AddReviewRequest req, Guid userId, string userName);
        string GenerateSlug(string name);
    }

    public class ProductBusinessService : IProductBusinessService
    {
        private readonly IProductRepository _productRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly ILogger<ProductBusinessService> _logger;

        public ProductBusinessService(
            IProductRepository productRepo,
            ICategoryRepository categoryRepo,
            ILogger<ProductBusinessService> logger)
        {
            _productRepo = productRepo;
            _categoryRepo = categoryRepo;
            _logger = logger;
        }

        // ✅ Product create karo — validation + slug generate
        public async Task<(Guid Id, string Slug)> CreateProductAsync(
            CreateProductRequest req, Guid sellerId)
        {
            // Validation
            var errors = ValidateCreateProduct(req);
            if (errors.Any())
                throw new ArgumentException(string.Join(", ", errors));

            // Category exist karta hai?
            var category = await _categoryRepo.GetByIdAsync(req.CategoryId);
            if (category == null)
                throw new KeyNotFoundException("Category not found");

            // SKU unique hai?
            if (await _productRepo.SkuExistsAsync(req.SKU))
                throw new InvalidOperationException($"SKU '{req.SKU}' already exists");

            // ✅ Unique slug generate karo
            var slug = GenerateSlug(req.Name);
            var counter = 1;
            var baseSlug = slug;
            while (await _productRepo.GetBySlugAsync(slug) != null)
                slug = $"{baseSlug}-{counter++}";

            // ✅ Discount calculate karo
            var discount = req.MRP > 0
                ? Math.Round((req.MRP - req.SellingPrice) / req.MRP * 100, 2)
                : 0;

            var product = new Product
            {
                Id = Guid.NewGuid(),
                SellerId = sellerId,
                CategoryId = req.CategoryId,
                Name = req.Name.Trim(),
                Slug = slug,
                Description = req.Description,
                ShortDescription = req.ShortDescription,
                MRP = req.MRP,
                SellingPrice = req.SellingPrice,
                Discount = discount,
                Stock = req.Stock,
                MinStock = req.MinStock,
                SKU = req.SKU.Trim().ToUpper(),
                Brand = req.Brand?.Trim(),
                Weight = req.Weight,
                Status = ProductStatus.PendingApproval,  // Admin approve karega
                MetaTitle = req.MetaTitle ?? req.Name,
                MetaDescription = req.MetaDescription,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _productRepo.CreateAsync(product);

            // ✅ Variants add karo
            foreach (var v in req.Variants)
            {
                await _productRepo.AddVariantAsync(new ProductVariant
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Name = v.Name,
                    Value = v.Value,
                    PriceAdjust = v.PriceAdjust,
                    Stock = v.Stock,
                    SKU = v.SKU,
                    CreatedAt = DateTime.UtcNow
                });
            }

            _logger.LogInformation("Product created: {ProductId} | {Name} | Seller: {SellerId}",
                product.Id, product.Name, sellerId);

            return (product.Id, slug);
        }

        // ✅ Product update karo
        public async Task UpdateProductAsync(Guid id, UpdateProductRequest req, Guid sellerId)
        {
            var product = await _productRepo.GetByIdAsync(id);

            if (product == null)
                throw new KeyNotFoundException("Product not found");

            if (product.SellerId != sellerId)
                throw new UnauthorizedAccessException("You can only update your own products");

            product.CategoryId = req.CategoryId;
            product.Name = req.Name.Trim();
            product.Description = req.Description;
            product.ShortDescription = req.ShortDescription;
            product.MRP = req.MRP;
            product.SellingPrice = req.SellingPrice;
            product.Discount = req.MRP > 0
                ? Math.Round((req.MRP - req.SellingPrice) / req.MRP * 100, 2) : 0;
            product.MinStock = req.MinStock;
            product.Brand = req.Brand;
            product.Weight = req.Weight;
            product.MetaTitle = req.MetaTitle;
            product.MetaDescription = req.MetaDescription;

            await _productRepo.UpdateAsync(product);
        }

        // ✅ Image add karo
        public async Task AddImageAsync(Guid productId, string imageUrl,
            bool isPrimary, Guid sellerId)
        {
            var product = await _productRepo.GetByIdAsync(productId);
            if (product == null)
                throw new KeyNotFoundException("Product not found");

            if (product.SellerId != sellerId)
                throw new UnauthorizedAccessException("You can only update your own products");

            await _productRepo.AddImageAsync(new ProductImage
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                ImageUrl = imageUrl,
                IsPrimary = isPrimary,
                CreatedAt = DateTime.UtcNow
            });
        }

        // ✅ Review add karo
        public async Task AddReviewAsync(Guid productId, AddReviewRequest req,
            Guid userId, string userName)
        {
            if (req.Rating < 1 || req.Rating > 5)
                throw new ArgumentException("Rating must be between 1 and 5");

            if (await _productRepo.HasUserReviewedAsync(productId, userId))
                throw new InvalidOperationException("You have already reviewed this product");

            await _productRepo.AddReviewAsync(new ProductReview
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                UserId = userId,
                UserName = userName,
                OrderId = req.OrderId,
                Rating = req.Rating,
                Title = req.Title,
                Comment = req.Comment,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            // ✅ Average rating update karo
            await _productRepo.UpdateRatingAsync(productId);
        }

        // ✅ URL-friendly slug banao
        public string GenerateSlug(string name)
        {
            var slug = name.ToLower().Trim();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            return slug.Trim('-');
        }

        private static List<string> ValidateCreateProduct(CreateProductRequest req)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(req.Name))
                errors.Add("Product name is required");
            else if (req.Name.Length > 200)
                errors.Add("Product name cannot exceed 200 characters");

            if (req.MRP <= 0)
                errors.Add("MRP must be greater than 0");

            if (req.SellingPrice <= 0)
                errors.Add("Selling price must be greater than 0");

            if (req.SellingPrice > req.MRP)
                errors.Add("Selling price cannot be greater than MRP");

            if (req.Stock < 0)
                errors.Add("Stock cannot be negative");

            if (string.IsNullOrWhiteSpace(req.SKU))
                errors.Add("SKU is required");

            if (req.CategoryId == Guid.Empty)
                errors.Add("Category is required");

            return errors;
        }
    }
}