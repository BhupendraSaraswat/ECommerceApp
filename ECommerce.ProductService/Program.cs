using ECommerce.ProductService.Data;
using ECommerce.ProductService.Services;
using ECommerce.Shared.Infrastructure.Data;
using ECommerce.Shared.Infrastructure.Extensions;
using ECommerce.Shared.Infrastructure.Messaging;
using ECommerce.Shared.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// ? ADO.NET Connection Factory
builder.Services.AddAdoNetConnection();

// ? Repositories
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();

// ? Business Services
builder.Services.AddScoped<IProductBusinessService, ProductBusinessService>();

// ? Image Service — local dev mein Local, production mein Azure Blob
var blobConnection = builder.Configuration["AzureBlob:ConnectionString"];
if (!string.IsNullOrEmpty(blobConnection))
{
    builder.Services.AddSingleton(new Azure.Storage.Blobs.BlobServiceClient(blobConnection));
    builder.Services.AddScoped<IImageService, AzureBlobImageService>();
}
else
{
    builder.Services.AddScoped<IImageService, LocalImageService>();
}

// ? Service Bus — local dev mein Null publisher
builder.Services.AddSingleton<IServiceBusPublisher, NullServiceBusPublisher>();

// ? JWT Authentication — same secret as AuthService
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                                          Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ? CORS
builder.Services.AddCors(options => options.AddPolicy("AllowFrontend", policy =>
    policy
        .WithOrigins(
            "http://localhost:3000",
            "http://localhost:3001",
            "http://localhost:3002"
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()
));

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

Console.WriteLine($"? ProductService running on port 5002 | Env: {app.Environment.EnvironmentName}");

app.Run();