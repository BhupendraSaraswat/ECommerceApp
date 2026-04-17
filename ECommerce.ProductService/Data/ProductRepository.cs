using ECommerce.ProductService.Models;
using ECommerce.Shared.Contracts.Enums;
using ECommerce.Shared.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ECommerce.ProductService.Data
{
    public interface IProductRepository
    {
        Task<(List<Product> Items, int Total)> SearchAsync(ProductSearchRequest req);
        Task<Product?> GetByIdAsync(Guid id);
        Task<Product?> GetBySlugAsync(string slug);
        Task<bool> SkuExistsAsync(string sku);
        Task<Guid> CreateAsync(Product product);
        Task UpdateAsync(Product product);
        Task UpdateStatusAsync(Guid id, ProductStatus status);
        Task UpdateStockAsync(Guid id, int quantity, string operation);
        Task UpdateRatingAsync(Guid productId);
        Task DeleteAsync(Guid id, Guid sellerId);
        Task AddImageAsync(ProductImage image);
        Task DeleteImageAsync(Guid imageId, Guid productId);
        Task AddVariantAsync(ProductVariant variant);
        Task DeleteVariantAsync(Guid variantId, Guid productId);
        Task<List<ProductReview>> GetReviewsAsync(Guid productId, int page, int size);
        Task AddReviewAsync(ProductReview review);
        Task<bool> HasUserReviewedAsync(Guid productId, Guid userId);
    }

    public class ProductRepository : BaseRepository, IProductRepository
    {
        public ProductRepository(IDbConnectionFactory factory,
            ILogger<ProductRepository> logger) : base(factory, logger) { }

        // ✅ Search + Filter + Pagination — ADO.NET se dynamic query
        public async Task<(List<Product> Items, int Total)> SearchAsync(ProductSearchRequest req)
        {
            var where = new List<string> { "p.IsDeleted = 0", "p.Status = 1" };
            var countParams = new List<SqlParameter>();
            var dataParams = new List<SqlParameter>();

            if (!string.IsNullOrWhiteSpace(req.Query))
            {
                where.Add("(p.Name LIKE @Query OR p.Brand LIKE @Query OR p.ShortDescription LIKE @Query)");
                countParams.Add(new SqlParameter("@Query", $"%{req.Query}%"));
                dataParams.Add(new SqlParameter("@Query", $"%{req.Query}%"));
            }

            if (req.CategoryId.HasValue)
            {
                where.Add("p.CategoryId = @CategoryId");
                countParams.Add(new SqlParameter("@CategoryId", req.CategoryId.Value));
                dataParams.Add(new SqlParameter("@CategoryId", req.CategoryId.Value));
            }

            if (!string.IsNullOrWhiteSpace(req.Brand))
            {
                where.Add("p.Brand = @Brand");
                countParams.Add(new SqlParameter("@Brand", req.Brand));
                dataParams.Add(new SqlParameter("@Brand", req.Brand));
            }

            if (req.MinPrice.HasValue)
            {
                where.Add("p.SellingPrice >= @MinPrice");
                countParams.Add(new SqlParameter("@MinPrice", req.MinPrice.Value));
                dataParams.Add(new SqlParameter("@MinPrice", req.MinPrice.Value));
            }

            if (req.MaxPrice.HasValue)
            {
                where.Add("p.SellingPrice <= @MaxPrice");
                countParams.Add(new SqlParameter("@MaxPrice", req.MaxPrice.Value));
                dataParams.Add(new SqlParameter("@MaxPrice", req.MaxPrice.Value));
            }

            if (req.MinRating.HasValue)
            {
                where.Add("p.AverageRating >= @MinRating");
                countParams.Add(new SqlParameter("@MinRating", req.MinRating.Value));
                dataParams.Add(new SqlParameter("@MinRating", req.MinRating.Value));
            }

            if (req.InStock == true)
            {
                where.Add("p.Stock > 0");
            }

            if (req.SellerId.HasValue)
            {
                where.Add("p.SellerId = @SellerId");
                countParams.Add(new SqlParameter("@SellerId", req.SellerId.Value));
                dataParams.Add(new SqlParameter("@SellerId", req.SellerId.Value));
            }

            var whereClause = string.Join(" AND ", where);

            var orderBy = req.SortBy?.ToLower() switch
            {
                "price" => "p.SellingPrice",
                "rating" => "p.AverageRating",
                "sold" => "p.TotalSold",
                "name" => "p.Name",
                _ => "p.CreatedAt"
            };
            var direction = req.SortOrder?.ToLower() == "asc" ? "ASC" : "DESC";

            var countSql = $"SELECT COUNT(1) FROM Products p WHERE {whereClause}";
            var total = await ExecuteScalarAsync<int>(countSql, countParams.ToArray());

            var offset = (req.PageNumber - 1) * req.PageSize;
            var dataSql = $@"
        SELECT p.Id, p.SellerId, p.CategoryId, p.Name, p.Slug,
               p.ShortDescription, p.MRP, p.SellingPrice, p.Discount,
               p.Stock, p.SKU, p.Brand, p.Status, p.IsFeatured,
               p.AverageRating, p.TotalReviews, p.TotalSold,
               p.CreatedAt, p.UpdatedAt,
               c.Name AS CategoryName,
               (SELECT TOP 1 ImageUrl FROM ProductImages
                WHERE ProductId = p.Id AND IsPrimary = 1
                  AND IsDeleted = 0) AS PrimaryImage
        FROM   Products p
        JOIN   Categories c ON c.Id = p.CategoryId
        WHERE  {whereClause}
        ORDER  BY {orderBy} {direction}
        OFFSET {offset} ROWS FETCH NEXT {req.PageSize} ROWS ONLY";

            var products = await QueryAsync(dataSql, MapProductList, dataParams.ToArray());

            return (products, total);
        }
        // ✅ Product detail — images aur variants ke saath
        public async Task<Product?> GetByIdAsync(Guid id)
        {
            const string sql = @"
                SELECT p.Id, p.SellerId, p.CategoryId, p.Name, p.Slug,
                       p.Description, p.ShortDescription, p.MRP, p.SellingPrice,
                       p.Discount, p.Stock, p.MinStock, p.SKU, p.Brand, p.Weight,
                       p.Status, p.IsFeatured, p.AverageRating, p.TotalReviews,
                       p.TotalSold, p.MetaTitle, p.MetaDescription,
                       p.CreatedAt, p.UpdatedAt,
                       c.Name AS CategoryName
                FROM   Products p
                JOIN   Categories c ON c.Id = p.CategoryId
                WHERE  p.Id = @Id AND p.IsDeleted = 0";

            var product = await QuerySingleAsync(sql, MapProductDetail,
                new[] { new SqlParameter("@Id", id) });

            if (product == null) return null;

            product.Images = await GetImagesAsync(id);
            product.Variants = await GetVariantsAsync(id);

            return product;
        }

        public async Task<Product?> GetBySlugAsync(string slug)
        {
            const string sql = @"
                SELECT p.Id, p.SellerId, p.CategoryId, p.Name, p.Slug,
                       p.Description, p.ShortDescription, p.MRP, p.SellingPrice,
                       p.Discount, p.Stock, p.MinStock, p.SKU, p.Brand, p.Weight,
                       p.Status, p.IsFeatured, p.AverageRating, p.TotalReviews,
                       p.TotalSold, p.MetaTitle, p.MetaDescription,
                       p.CreatedAt, p.UpdatedAt,
                       c.Name AS CategoryName
                FROM   Products p
                JOIN   Categories c ON c.Id = p.CategoryId
                WHERE  p.Slug = @Slug AND p.IsDeleted = 0";

            var product = await QuerySingleAsync(sql, MapProductDetail,
                new[] { new SqlParameter("@Slug", slug.ToLower()) });

            if (product == null) return null;

            product.Images = await GetImagesAsync(product.Id);
            product.Variants = await GetVariantsAsync(product.Id);

            return product;
        }

        public async Task<bool> SkuExistsAsync(string sku)
        {
            const string sql = "SELECT COUNT(1) FROM Products WHERE SKU = @SKU AND IsDeleted = 0";
            var count = await ExecuteScalarAsync<int>(sql,
                new[] { new SqlParameter("@SKU", sku) });
            return count > 0;
        }

        public async Task<Guid> CreateAsync(Product product)
        {
            const string sql = @"
                INSERT INTO Products
                    (Id, SellerId, CategoryId, Name, Slug, Description,
                     ShortDescription, MRP, SellingPrice, Discount, Stock,
                     MinStock, SKU, Brand, Weight, Status, IsFeatured,
                     MetaTitle, MetaDescription, CreatedAt, UpdatedAt)
                VALUES
                    (@Id, @SellerId, @CategoryId, @Name, @Slug, @Description,
                     @ShortDescription, @MRP, @SellingPrice, @Discount, @Stock,
                     @MinStock, @SKU, @Brand, @Weight, @Status, @IsFeatured,
                     @MetaTitle, @MetaDescription, GETUTCDATE(), GETUTCDATE())";

            await ExecuteAsync(sql, new[]
            {
                new SqlParameter("@Id",               product.Id),
                new SqlParameter("@SellerId",          product.SellerId),
                new SqlParameter("@CategoryId",        product.CategoryId),
                new SqlParameter("@Name",              product.Name),
                new SqlParameter("@Slug",              product.Slug.ToLower()),
                new SqlParameter("@Description",
                    product.Description != null ? (object)product.Description : DBNull.Value),
                new SqlParameter("@ShortDescription",
                    product.ShortDescription != null ? (object)product.ShortDescription : DBNull.Value),
                new SqlParameter("@MRP",               product.MRP),
                new SqlParameter("@SellingPrice",      product.SellingPrice),
                new SqlParameter("@Discount",          product.Discount),
                new SqlParameter("@Stock",             product.Stock),
                new SqlParameter("@MinStock",          product.MinStock),
                new SqlParameter("@SKU",               product.SKU),
                new SqlParameter("@Brand",
                    product.Brand != null ? (object)product.Brand : DBNull.Value),
                new SqlParameter("@Weight",
                    product.Weight.HasValue ? (object)product.Weight.Value : DBNull.Value),
                new SqlParameter("@Status",            (int)product.Status),
                new SqlParameter("@IsFeatured",        product.IsFeatured),
                new SqlParameter("@MetaTitle",
                    product.MetaTitle != null ? (object)product.MetaTitle : DBNull.Value),
                new SqlParameter("@MetaDescription",
                    product.MetaDescription != null ? (object)product.MetaDescription : DBNull.Value)
            });

            return product.Id;
        }

        public async Task UpdateAsync(Product product)
        {
            const string sql = @"
                UPDATE Products SET
                    CategoryId       = @CategoryId,
                    Name             = @Name,
                    Description      = @Description,
                    ShortDescription = @ShortDescription,
                    MRP              = @MRP,
                    SellingPrice     = @SellingPrice,
                    Discount         = @Discount,
                    MinStock         = @MinStock,
                    Brand            = @Brand,
                    Weight           = @Weight,
                    MetaTitle        = @MetaTitle,
                    MetaDescription  = @MetaDescription,
                    UpdatedAt        = GETUTCDATE()
                WHERE Id = @Id AND SellerId = @SellerId AND IsDeleted = 0";

            await ExecuteAsync(sql, new[]
            {
                new SqlParameter("@Id",               product.Id),
                new SqlParameter("@SellerId",          product.SellerId),
                new SqlParameter("@CategoryId",        product.CategoryId),
                new SqlParameter("@Name",              product.Name),
                new SqlParameter("@Description",
                    product.Description != null ? (object)product.Description : DBNull.Value),
                new SqlParameter("@ShortDescription",
                    product.ShortDescription != null ? (object)product.ShortDescription : DBNull.Value),
                new SqlParameter("@MRP",               product.MRP),
                new SqlParameter("@SellingPrice",      product.SellingPrice),
                new SqlParameter("@Discount",          product.Discount),
                new SqlParameter("@MinStock",          product.MinStock),
                new SqlParameter("@Brand",
                    product.Brand != null ? (object)product.Brand : DBNull.Value),
                new SqlParameter("@Weight",
                    product.Weight.HasValue ? (object)product.Weight.Value : DBNull.Value),
                new SqlParameter("@MetaTitle",
                    product.MetaTitle != null ? (object)product.MetaTitle : DBNull.Value),
                new SqlParameter("@MetaDescription",
                    product.MetaDescription != null ? (object)product.MetaDescription : DBNull.Value)
            });
        }

        public async Task UpdateStatusAsync(Guid id, ProductStatus status)
        {
            const string sql = @"
                UPDATE Products SET Status = @Status, UpdatedAt = GETUTCDATE()
                WHERE Id = @Id AND IsDeleted = 0";

            await ExecuteAsync(sql, new[]
            {
                new SqlParameter("@Id",     id),
                new SqlParameter("@Status", (int)status)
            });
        }

        public async Task UpdateStockAsync(Guid id, int quantity, string operation)
        {
            var sql = operation == "reduce"
                ? @"UPDATE Products SET
                        Stock     = CASE WHEN Stock - @Qty < 0 THEN 0 ELSE Stock - @Qty END,
                        TotalSold = TotalSold + @Qty,
                        Status    = CASE WHEN Stock - @Qty <= 0 THEN 2 ELSE Status END,
                        UpdatedAt = GETUTCDATE()
                    WHERE Id = @Id AND IsDeleted = 0"
                : @"UPDATE Products SET
                        Stock     = Stock + @Qty,
                        Status    = CASE WHEN Status = 2 AND Stock + @Qty > 0 THEN 1 ELSE Status END,
                        UpdatedAt = GETUTCDATE()
                    WHERE Id = @Id AND IsDeleted = 0";

            await ExecuteAsync(sql, new[]
            {
                new SqlParameter("@Id",  id),
                new SqlParameter("@Qty", quantity)
            });
        }

        public async Task UpdateRatingAsync(Guid productId)
        {
            const string sql = @"
                UPDATE Products SET
                    AverageRating = (
                        SELECT CAST(AVG(CAST(Rating AS DECIMAL(3,2))) AS DECIMAL(3,2))
                        FROM ProductReviews
                        WHERE ProductId = @ProductId AND IsDeleted = 0 AND IsApproved = 1
                    ),
                    TotalReviews = (
                        SELECT COUNT(1) FROM ProductReviews
                        WHERE ProductId = @ProductId AND IsDeleted = 0 AND IsApproved = 1
                    ),
                    UpdatedAt = GETUTCDATE()
                WHERE Id = @ProductId";

            await ExecuteAsync(sql, new[] { new SqlParameter("@ProductId", productId) });
        }

        public async Task DeleteAsync(Guid id, Guid sellerId)
        {
            const string sql = @"
                UPDATE Products SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
                WHERE Id = @Id AND SellerId = @SellerId";

            await ExecuteAsync(sql, new[]
            {
                new SqlParameter("@Id",       id),
                new SqlParameter("@SellerId", sellerId)
            });
        }

        public async Task AddImageAsync(ProductImage image)
        {
            // ✅ Agar primary hai toh purani primary reset karo
            if (image.IsPrimary)
            {
                await ExecuteAsync(
                    "UPDATE ProductImages SET IsPrimary = 0 WHERE ProductId = @ProductId",
                    new[] { new SqlParameter("@ProductId", image.ProductId) });
            }

            const string sql = @"
                INSERT INTO ProductImages
                    (Id, ProductId, ImageUrl, AltText, IsPrimary, SortOrder, CreatedAt)
                VALUES
                    (@Id, @ProductId, @ImageUrl, @AltText, @IsPrimary, @SortOrder, GETUTCDATE())";

            await ExecuteAsync(sql, new[]
            {
                new SqlParameter("@Id",        image.Id),
                new SqlParameter("@ProductId", image.ProductId),
                new SqlParameter("@ImageUrl",  image.ImageUrl),
                new SqlParameter("@AltText",
                    image.AltText != null ? (object)image.AltText : DBNull.Value),
                new SqlParameter("@IsPrimary", image.IsPrimary),
                new SqlParameter("@SortOrder", image.SortOrder)
            });
        }

        public async Task DeleteImageAsync(Guid imageId, Guid productId)
        {
            const string sql = @"
                UPDATE ProductImages SET IsDeleted = 1
                WHERE Id = @Id AND ProductId = @ProductId";

            await ExecuteAsync(sql, new[]
            {
                new SqlParameter("@Id",        imageId),
                new SqlParameter("@ProductId", productId)
            });
        }

        public async Task AddVariantAsync(ProductVariant variant)
        {
            const string sql = @"
                INSERT INTO ProductVariants
                    (Id, ProductId, Name, Value, PriceAdjust, Stock, SKU, CreatedAt)
                VALUES
                    (@Id, @ProductId, @Name, @Value, @PriceAdjust, @Stock, @SKU, GETUTCDATE())";

            await ExecuteAsync(sql, new[]
            {
                new SqlParameter("@Id",          variant.Id),
                new SqlParameter("@ProductId",   variant.ProductId),
                new SqlParameter("@Name",         variant.Name),
                new SqlParameter("@Value",        variant.Value),
                new SqlParameter("@PriceAdjust",  variant.PriceAdjust),
                new SqlParameter("@Stock",        variant.Stock),
                new SqlParameter("@SKU",
                    variant.SKU != null ? (object)variant.SKU : DBNull.Value)
            });
        }

        public async Task DeleteVariantAsync(Guid variantId, Guid productId)
        {
            const string sql = @"
                UPDATE ProductVariants SET IsDeleted = 1
                WHERE Id = @Id AND ProductId = @ProductId";

            await ExecuteAsync(sql, new[]
            {
                new SqlParameter("@Id",        variantId),
                new SqlParameter("@ProductId", productId)
            });
        }

        public async Task<List<ProductReview>> GetReviewsAsync(Guid productId, int page, int size)
        {
            var offset = (page - 1) * size;
            var sql = $@"
                SELECT Id, ProductId, UserId, UserName, OrderId,
                       Rating, Title, Comment, IsVerified, IsApproved,
                       HelpfulCount, CreatedAt, UpdatedAt
                FROM   ProductReviews
                WHERE  ProductId = @ProductId AND IsDeleted = 0 AND IsApproved = 1
                ORDER  BY CreatedAt DESC
                OFFSET {offset} ROWS FETCH NEXT {size} ROWS ONLY";

            return await QueryAsync(sql, MapReview,
                new[] { new SqlParameter("@ProductId", productId) });
        }

        public async Task AddReviewAsync(ProductReview review)
        {
            const string sql = @"
                INSERT INTO ProductReviews
                    (Id, ProductId, UserId, UserName, OrderId, Rating,
                     Title, Comment, IsVerified, IsApproved, CreatedAt, UpdatedAt)
                VALUES
                    (@Id, @ProductId, @UserId, @UserName, @OrderId, @Rating,
                     @Title, @Comment, @IsVerified, 1, GETUTCDATE(), GETUTCDATE())";

            await ExecuteAsync(sql, new[]
            {
                new SqlParameter("@Id",         review.Id),
                new SqlParameter("@ProductId",  review.ProductId),
                new SqlParameter("@UserId",     review.UserId),
                new SqlParameter("@UserName",   review.UserName),
                new SqlParameter("@OrderId",
                    review.OrderId.HasValue ? (object)review.OrderId.Value : DBNull.Value),
                new SqlParameter("@Rating",     review.Rating),
                new SqlParameter("@Title",
                    review.Title != null ? (object)review.Title : DBNull.Value),
                new SqlParameter("@Comment",
                    review.Comment != null ? (object)review.Comment : DBNull.Value),
                new SqlParameter("@IsVerified", review.IsVerified)
            });
        }

        public async Task<bool> HasUserReviewedAsync(Guid productId, Guid userId)
        {
            const string sql = @"
                SELECT COUNT(1) FROM ProductReviews
                WHERE ProductId = @ProductId AND UserId = @UserId AND IsDeleted = 0";

            var count = await ExecuteScalarAsync<int>(sql, new[]
            {
                new SqlParameter("@ProductId", productId),
                new SqlParameter("@UserId",    userId)
            });
            return count > 0;
        }

        // ─── Private Helpers ──────────────────────────────────
        private async Task<List<ProductImage>> GetImagesAsync(Guid productId)
        {
            const string sql = @"
                SELECT Id, ProductId, ImageUrl, AltText, IsPrimary, SortOrder, CreatedAt
                FROM   ProductImages
                WHERE  ProductId = @ProductId AND IsDeleted = 0
                ORDER  BY IsPrimary DESC, SortOrder";

            return await QueryAsync(sql, r => new ProductImage
            {
                Id = GetGuid(r, "Id"),
                ProductId = GetGuid(r, "ProductId"),
                ImageUrl = GetString(r, "ImageUrl"),
                AltText = GetValueOrDefault<string?>(r, "AltText"),
                IsPrimary = GetBool(r, "IsPrimary"),
                SortOrder = GetInt(r, "SortOrder"),
                CreatedAt = GetDateTime(r, "CreatedAt")
            }, new[] { new SqlParameter("@ProductId", productId) });
        }

        private async Task<List<ProductVariant>> GetVariantsAsync(Guid productId)
        {
            const string sql = @"
                SELECT Id, ProductId, Name, Value, PriceAdjust, Stock, SKU, CreatedAt
                FROM   ProductVariants
                WHERE  ProductId = @ProductId AND IsDeleted = 0";

            return await QueryAsync(sql, r => new ProductVariant
            {
                Id = GetGuid(r, "Id"),
                ProductId = GetGuid(r, "ProductId"),
                Name = GetString(r, "Name"),
                Value = GetString(r, "Value"),
                PriceAdjust = GetDecimal(r, "PriceAdjust"),
                Stock = GetInt(r, "Stock"),
                SKU = GetValueOrDefault<string?>(r, "SKU"),
                CreatedAt = GetDateTime(r, "CreatedAt")
            }, new[] { new SqlParameter("@ProductId", productId) });
        }

        private static Product MapProductList(SqlDataReader r) => new Product
        {
            Id = GetGuid(r, "Id"),
            SellerId = GetGuid(r, "SellerId"),
            CategoryId = GetGuid(r, "CategoryId"),
            Name = GetString(r, "Name"),
            Slug = GetString(r, "Slug"),
            ShortDescription = GetValueOrDefault<string?>(r, "ShortDescription"),
            MRP = GetDecimal(r, "MRP"),
            SellingPrice = GetDecimal(r, "SellingPrice"),
            Discount = GetDecimal(r, "Discount"),
            Stock = GetInt(r, "Stock"),
            SKU = GetString(r, "SKU"),
            Brand = GetValueOrDefault<string?>(r, "Brand"),
            Status = (ProductStatus)GetInt(r, "Status"),
            IsFeatured = GetBool(r, "IsFeatured"),
            AverageRating = GetDecimal(r, "AverageRating"),
            TotalReviews = GetInt(r, "TotalReviews"),
            TotalSold = GetInt(r, "TotalSold"),
            CreatedAt = GetDateTime(r, "CreatedAt"),
            UpdatedAt = GetDateTime(r, "UpdatedAt"),
            CategoryName = GetValueOrDefault<string?>(r, "CategoryName")
        };

        private static Product MapProductDetail(SqlDataReader r) => new Product
        {
            Id = GetGuid(r, "Id"),
            SellerId = GetGuid(r, "SellerId"),
            CategoryId = GetGuid(r, "CategoryId"),
            Name = GetString(r, "Name"),
            Slug = GetString(r, "Slug"),
            Description = GetValueOrDefault<string?>(r, "Description"),
            ShortDescription = GetValueOrDefault<string?>(r, "ShortDescription"),
            MRP = GetDecimal(r, "MRP"),
            SellingPrice = GetDecimal(r, "SellingPrice"),
            Discount = GetDecimal(r, "Discount"),
            Stock = GetInt(r, "Stock"),
            MinStock = GetInt(r, "MinStock"),
            SKU = GetString(r, "SKU"),
            Brand = GetValueOrDefault<string?>(r, "Brand"),
            Weight = GetValueOrDefault<decimal?>(r, "Weight"),
            Status = (ProductStatus)GetInt(r, "Status"),
            IsFeatured = GetBool(r, "IsFeatured"),
            AverageRating = GetDecimal(r, "AverageRating"),
            TotalReviews = GetInt(r, "TotalReviews"),
            TotalSold = GetInt(r, "TotalSold"),
            MetaTitle = GetValueOrDefault<string?>(r, "MetaTitle"),
            MetaDescription = GetValueOrDefault<string?>(r, "MetaDescription"),
            CreatedAt = GetDateTime(r, "CreatedAt"),
            UpdatedAt = GetDateTime(r, "UpdatedAt"),
            CategoryName = GetValueOrDefault<string?>(r, "CategoryName")
        };

        private static ProductReview MapReview(SqlDataReader r) => new ProductReview
        {
            Id = GetGuid(r, "Id"),
            ProductId = GetGuid(r, "ProductId"),
            UserId = GetGuid(r, "UserId"),
            UserName = GetString(r, "UserName"),
            OrderId = GetValueOrDefault<Guid?>(r, "OrderId"),
            Rating = GetInt(r, "Rating"),
            Title = GetValueOrDefault<string?>(r, "Title"),
            Comment = GetValueOrDefault<string?>(r, "Comment"),
            IsVerified = GetBool(r, "IsVerified"),
            IsApproved = GetBool(r, "IsApproved"),
            HelpfulCount = GetInt(r, "HelpfulCount"),
            CreatedAt = GetDateTime(r, "CreatedAt"),
            UpdatedAt = GetDateTime(r, "UpdatedAt")
        };
    }
}