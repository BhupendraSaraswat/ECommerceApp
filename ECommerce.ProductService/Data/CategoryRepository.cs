using ECommerce.ProductService.Models;
using ECommerce.Shared.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ECommerce.ProductService.Data
{
    public interface ICategoryRepository
    {
        Task<List<Category>> GetAllAsync();
        Task<List<Category>> GetTopLevelAsync();
        Task<List<Category>> GetChildrenAsync(Guid parentId);
        Task<Category?> GetByIdAsync(Guid id);
        Task<Category?> GetBySlugAsync(string slug);
        Task<Guid> CreateAsync(Category category);
        Task UpdateAsync(Category category);
        Task DeleteAsync(Guid id);
    }

    public class CategoryRepository : BaseRepository, ICategoryRepository
    {
        public CategoryRepository(IDbConnectionFactory factory,
            ILogger<CategoryRepository> logger) : base(factory, logger) { }

        // ✅ Saari categories
        public async Task<List<Category>> GetAllAsync()
        {
            const string sql = @"
                SELECT Id, Name, Slug, Description, ParentId,
                       ImageUrl, IsActive, SortOrder, CreatedAt, UpdatedAt
                FROM   Categories
                WHERE  IsDeleted = 0
                ORDER  BY SortOrder, Name";

            return await QueryAsync(sql, MapCategory);
        }

        // ✅ Top level categories (ParentId = NULL)
        public async Task<List<Category>> GetTopLevelAsync()
        {
            const string sql = @"
                SELECT Id, Name, Slug, Description, ParentId,
                       ImageUrl, IsActive, SortOrder, CreatedAt, UpdatedAt
                FROM   Categories
                WHERE  ParentId IS NULL AND IsDeleted = 0
                ORDER  BY SortOrder, Name";

            return await QueryAsync(sql, MapCategory);
        }

        // ✅ Kisi category ke children
        public async Task<List<Category>> GetChildrenAsync(Guid parentId)
        {
            const string sql = @"
                SELECT Id, Name, Slug, Description, ParentId,
                       ImageUrl, IsActive, SortOrder, CreatedAt, UpdatedAt
                FROM   Categories
                WHERE  ParentId = @ParentId AND IsDeleted = 0
                ORDER  BY SortOrder, Name";

            return await QueryAsync(sql, MapCategory,
                new[] { new SqlParameter("@ParentId", parentId) });
        }

        public async Task<Category?> GetByIdAsync(Guid id)
        {
            const string sql = @"
                SELECT Id, Name, Slug, Description, ParentId,
                       ImageUrl, IsActive, SortOrder, CreatedAt, UpdatedAt
                FROM   Categories
                WHERE  Id = @Id AND IsDeleted = 0";

            return await QuerySingleAsync(sql, MapCategory,
                new[] { new SqlParameter("@Id", id) });
        }

        public async Task<Category?> GetBySlugAsync(string slug)
        {
            const string sql = @"
                SELECT Id, Name, Slug, Description, ParentId,
                       ImageUrl, IsActive, SortOrder, CreatedAt, UpdatedAt
                FROM   Categories
                WHERE  Slug = @Slug AND IsDeleted = 0";

            return await QuerySingleAsync(sql, MapCategory,
                new[] { new SqlParameter("@Slug", slug.ToLower()) });
        }

        public async Task<Guid> CreateAsync(Category category)
        {
            const string sql = @"
                INSERT INTO Categories
                    (Id, Name, Slug, Description, ParentId, ImageUrl,
                     IsActive, SortOrder, CreatedAt, UpdatedAt)
                VALUES
                    (@Id, @Name, @Slug, @Description, @ParentId, @ImageUrl,
                     @IsActive, @SortOrder, GETUTCDATE(), GETUTCDATE())";

            await ExecuteAsync(sql, new[]
            {
                new SqlParameter("@Id",          category.Id),
                new SqlParameter("@Name",         category.Name),
                new SqlParameter("@Slug",         category.Slug.ToLower()),
                new SqlParameter("@Description",
                    category.Description != null ? (object)category.Description : DBNull.Value),
                new SqlParameter("@ParentId",
                    category.ParentId.HasValue ? (object)category.ParentId.Value : DBNull.Value),
                new SqlParameter("@ImageUrl",
                    category.ImageUrl != null ? (object)category.ImageUrl : DBNull.Value),
                new SqlParameter("@IsActive",     category.IsActive),
                new SqlParameter("@SortOrder",    category.SortOrder)
            });

            return category.Id;
        }

        public async Task UpdateAsync(Category category)
        {
            const string sql = @"
                UPDATE Categories SET
                    Name        = @Name,
                    Description = @Description,
                    ImageUrl    = @ImageUrl,
                    IsActive    = @IsActive,
                    SortOrder   = @SortOrder,
                    UpdatedAt   = GETUTCDATE()
                WHERE Id = @Id AND IsDeleted = 0";

            await ExecuteAsync(sql, new[]
            {
                new SqlParameter("@Id",          category.Id),
                new SqlParameter("@Name",         category.Name),
                new SqlParameter("@Description",
                    category.Description != null ? (object)category.Description : DBNull.Value),
                new SqlParameter("@ImageUrl",
                    category.ImageUrl != null ? (object)category.ImageUrl : DBNull.Value),
                new SqlParameter("@IsActive",     category.IsActive),
                new SqlParameter("@SortOrder",    category.SortOrder)
            });
        }

        public async Task DeleteAsync(Guid id)
        {
            const string sql = @"
                UPDATE Categories SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
                WHERE Id = @Id";

            await ExecuteAsync(sql, new[] { new SqlParameter("@Id", id) });
        }

        private static Category MapCategory(SqlDataReader r) => new Category
        {
            Id = GetGuid(r, "Id"),
            Name = GetString(r, "Name"),
            Slug = GetString(r, "Slug"),
            Description = GetValueOrDefault<string?>(r, "Description"),
            ParentId = GetValueOrDefault<Guid?>(r, "ParentId"),
            ImageUrl = GetValueOrDefault<string?>(r, "ImageUrl"),
            IsActive = GetBool(r, "IsActive"),
            SortOrder = GetInt(r, "SortOrder"),
            CreatedAt = GetDateTime(r, "CreatedAt"),
            UpdatedAt = GetDateTime(r, "UpdatedAt")
        };
    }
}