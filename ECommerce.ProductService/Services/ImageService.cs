using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace ECommerce.ProductService.Services
{
    public interface IImageService
    {
        Task<string> UploadAsync(IFormFile file, string folder);
        Task DeleteAsync(string imageUrl);
    }

    public class AzureBlobImageService : IImageService
    {
        private readonly BlobServiceClient _blobClient;
        private readonly string _containerName;
        private readonly ILogger<AzureBlobImageService> _logger;

        public AzureBlobImageService(BlobServiceClient blobClient,
            IConfiguration config, ILogger<AzureBlobImageService> logger)
        {
            _blobClient = blobClient;
            _containerName = config["AzureBlob:ContainerName"] ?? "product-images";
            _logger = logger;
        }

        public async Task<string> UploadAsync(IFormFile file, string folder)
        {
            // ✅ File validation
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowedTypes.Contains(file.ContentType.ToLower()))
                throw new ArgumentException("Only JPG, PNG, WebP images allowed");

            if (file.Length > 5 * 1024 * 1024)  // 5MB limit
                throw new ArgumentException("Image size cannot exceed 5MB");

            var container = _blobClient.GetBlobContainerClient(_containerName);
            await container.CreateIfNotExistsAsync(PublicAccessType.Blob);

            // ✅ Unique file name — GUID + original extension
            var extension = Path.GetExtension(file.FileName).ToLower();
            var fileName = $"{folder}/{Guid.NewGuid()}{extension}";
            var blob = container.GetBlobClient(fileName);

            await blob.UploadAsync(file.OpenReadStream(), new BlobHttpHeaders
            {
                ContentType = file.ContentType
            });

            _logger.LogInformation("Image uploaded: {FileName}", fileName);

            return blob.Uri.ToString();
        }

        public async Task DeleteAsync(string imageUrl)
        {
            try
            {
                var uri = new Uri(imageUrl);
                var fileName = uri.AbsolutePath.TrimStart('/').Replace($"{_containerName}/", "");
                var container = _blobClient.GetBlobContainerClient(_containerName);
                var blob = container.GetBlobClient(fileName);

                await blob.DeleteIfExistsAsync();
                _logger.LogInformation("Image deleted: {ImageUrl}", imageUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete image: {ImageUrl}", imageUrl);
            }
        }
    }

    // ✅ Local development ke liye — Azure Blob nahi hai
    public class LocalImageService : IImageService
    {
        private readonly ILogger<LocalImageService> _logger;

        public LocalImageService(ILogger<LocalImageService> logger)
            => _logger = logger;

        public Task<string> UploadAsync(IFormFile file, string folder)
        {
            _logger.LogInformation("LOCAL DEV: Image upload simulated for {FileName}", file.FileName);
            return Task.FromResult($"https://placeholder.com/products/{Guid.NewGuid()}.jpg");
        }

        public Task DeleteAsync(string imageUrl)
        {
            _logger.LogInformation("LOCAL DEV: Image delete simulated for {Url}", imageUrl);
            return Task.CompletedTask;
        }
    }
}