using ECommerce.ProductService.Data;
using ECommerce.ProductService.Models;
using ECommerce.ProductService.Services;
using ECommerce.Shared.Contracts.DTOs;
using ECommerce.Shared.Contracts.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.ProductService.Controllers
{
    // =============================================
    // PRODUCT CONTROLLER
    // =============================================
    [ApiController]
    [Route("api/products")]
    public class ProductController : ControllerBase
    {
        private readonly IProductRepository _repo;
        private readonly IProductBusinessService _service;
        private readonly IImageService _imageService;
        private readonly ILogger<ProductController> _logger;

        public ProductController(
            IProductRepository repo,
            IProductBusinessService service,
            IImageService imageService,
            ILogger<ProductController> logger)
        {
            _repo = repo;
            _service = service;
            _imageService = imageService;
            _logger = logger;
        }

        [HttpGet("/health")]
        public IActionResult Health() =>
            Ok(new { status = "UP", service = "product-service" });

        // ✅ Search + Filter + Pagination
        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] ProductSearchRequest req)
        {
            var (items, total) = await _repo.SearchAsync(req);

            var paged = new PagedResponse<Product>
            {
                Items = items,
                TotalCount = total,
                PageNumber = req.PageNumber,
                PageSize = req.PageSize
            };

            return Ok(ApiResponse<PagedResponse<Product>>.Ok(paged));
        }

        // ✅ Product by ID
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var product = await _repo.GetByIdAsync(id);
            if (product == null)
                return NotFound(ApiResponse<string>.Fail("Product not found"));

            return Ok(ApiResponse<Product>.Ok(product));
        }

        // ✅ Product by Slug — SEO friendly URL
        [HttpGet("slug/{slug}")]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            var product = await _repo.GetBySlugAsync(slug);
            if (product == null)
                return NotFound(ApiResponse<string>.Fail("Product not found"));

            return Ok(ApiResponse<Product>.Ok(product));
        }

        // ✅ Seller apna product create kare
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateProductRequest req)
        {
            var sellerId = GetUserId();
            if (sellerId == Guid.Empty)
                return Unauthorized();

            var (id, slug) = await _service.CreateProductAsync(req, sellerId);

            return StatusCode(201, ApiResponse<object>.Ok(
                new { id, slug, message = "Product submitted for approval" }));
        }

        // ✅ Seller apna product update kare
        [Authorize]
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest req)
        {
            var sellerId = GetUserId();
            await _service.UpdateProductAsync(id, req, sellerId);
            return Ok(ApiResponse<string>.Ok("Product updated successfully"));
        }

        // ✅ Seller apna product delete kare (soft delete)
        [Authorize]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var sellerId = GetUserId();
            await _repo.DeleteAsync(id, sellerId);
            return Ok(ApiResponse<string>.Ok("Product deleted"));
        }

        // ✅ Admin product approve/reject kare
        [Authorize(Roles = "Admin,SuperAdmin")]
        [HttpPatch("{id:guid}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromQuery] ProductStatus status)
        {
            await _repo.UpdateStatusAsync(id, status);
            return Ok(ApiResponse<string>.Ok($"Product status updated to {status}"));
        }

        // ✅ Stock update karo (Order service use karega)
        [Authorize]
        [HttpPatch("{id:guid}/stock")]
        public async Task<IActionResult> UpdateStock(Guid id, [FromBody] UpdateStockRequest req)
        {
            if (req.Quantity <= 0)
                return BadRequest(ApiResponse<string>.Fail("Quantity must be greater than 0"));

            await _repo.UpdateStockAsync(id, req.Quantity, req.Operation);
            return Ok(ApiResponse<string>.Ok("Stock updated"));
        }

        // ✅ Image upload
        [Authorize]
        [HttpPost("{id:guid}/images")]
        public async Task<IActionResult> UploadImage(Guid id, IFormFile file,
            [FromQuery] bool isPrimary = false)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponse<string>.Fail("No file provided"));

            var sellerId = GetUserId();
            var imageUrl = await _imageService.UploadAsync(file, $"products/{id}");
            await _service.AddImageAsync(id, imageUrl, isPrimary, sellerId);

            return Ok(ApiResponse<object>.Ok(new { imageUrl }));
        }

        // ✅ Image delete
        [Authorize]
        [HttpDelete("{productId:guid}/images/{imageId:guid}")]
        public async Task<IActionResult> DeleteImage(Guid productId, Guid imageId)
        {
            await _repo.DeleteImageAsync(imageId, productId);
            return Ok(ApiResponse<string>.Ok("Image deleted"));
        }

        // ✅ Variant add karo
        [Authorize]
        [HttpPost("{id:guid}/variants")]
        public async Task<IActionResult> AddVariant(Guid id, [FromBody] CreateVariantRequest req)
        {
            await _repo.AddVariantAsync(new ProductVariant
            {
                Id = Guid.NewGuid(),
                ProductId = id,
                Name = req.Name,
                Value = req.Value,
                PriceAdjust = req.PriceAdjust,
                Stock = req.Stock,
                SKU = req.SKU,
                CreatedAt = DateTime.UtcNow
            });

            return Ok(ApiResponse<string>.Ok("Variant added"));
        }

        // ✅ Reviews
        [HttpGet("{id:guid}/reviews")]
        public async Task<IActionResult> GetReviews(Guid id,
            [FromQuery] int page = 1, [FromQuery] int size = 10)
        {
            var reviews = await _repo.GetReviewsAsync(id, page, size);
            return Ok(ApiResponse<List<ProductReview>>.Ok(reviews));
        }

        [Authorize]
        [HttpPost("{id:guid}/reviews")]
        public async Task<IActionResult> AddReview(Guid id, [FromBody] AddReviewRequest req)
        {
            var userId = GetUserId();
            var userName = User.FindFirst("name")?.Value ?? "User";

            await _service.AddReviewAsync(id, req, userId, userName);

            return StatusCode(201, ApiResponse<string>.Ok("Review added successfully"));
        }

        private Guid GetUserId()
        {
            var id = User.FindFirst("userId")?.Value;
            return id != null ? Guid.Parse(id) : Guid.Empty;
        }
    }

    // =============================================
    // CATEGORY CONTROLLER
    // =============================================
    [ApiController]
    [Route("api/categories")]
    public class CategoryController : ControllerBase
    {
        private readonly ICategoryRepository _repo;
        private readonly IProductBusinessService _service;

        public CategoryController(ICategoryRepository repo, IProductBusinessService service)
        {
            _repo = repo;
            _service = service;
        }

        // ✅ Saari categories — tree structure ke saath
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var categories = await _repo.GetTopLevelAsync();

            // ✅ Children add karo
            foreach (var cat in categories)
                cat.Children = await _repo.GetChildrenAsync(cat.Id);

            return Ok(ApiResponse<List<Category>>.Ok(categories));
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var category = await _repo.GetByIdAsync(id);
            if (category == null)
                return NotFound(ApiResponse<string>.Fail("Category not found"));

            category.Children = await _repo.GetChildrenAsync(id);
            return Ok(ApiResponse<Category>.Ok(category));
        }

        [HttpGet("slug/{slug}")]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            var category = await _repo.GetBySlugAsync(slug);
            if (category == null)
                return NotFound(ApiResponse<string>.Fail("Category not found"));

            return Ok(ApiResponse<Category>.Ok(category));
        }

        // ✅ Admin category create kare
        [Authorize(Roles = "Admin,SuperAdmin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCategoryRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(ApiResponse<string>.Fail("Name is required"));

            var slug = _service.GenerateSlug(req.Name);

            var category = new Category
            {
                Id = Guid.NewGuid(),
                Name = req.Name.Trim(),
                Slug = slug,
                Description = req.Description,
                ParentId = req.ParentId,
                ImageUrl = req.ImageUrl,
                IsActive = true,
                SortOrder = req.SortOrder,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repo.CreateAsync(category);

            return StatusCode(201, ApiResponse<object>.Ok(
                new { id = category.Id, slug = category.Slug }));
        }

        [Authorize(Roles = "Admin,SuperAdmin")]
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateCategoryRequest req)
        {
            var category = await _repo.GetByIdAsync(id);
            if (category == null)
                return NotFound(ApiResponse<string>.Fail("Category not found"));

            category.Name = req.Name.Trim();
            category.Description = req.Description;
            category.ImageUrl = req.ImageUrl;
            category.SortOrder = req.SortOrder;
            category.UpdatedAt = DateTime.UtcNow;

            await _repo.UpdateAsync(category);
            return Ok(ApiResponse<string>.Ok("Category updated"));
        }

        [Authorize(Roles = "Admin,SuperAdmin")]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _repo.DeleteAsync(id);
            return Ok(ApiResponse<string>.Ok("Category deleted"));
        }
    }
}