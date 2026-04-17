using ECommerce.AuthService.Data;
using ECommerce.AuthService.Services;
using ECommerce.Shared.Infrastructure.Data;
using ECommerce.Shared.Infrastructure.Extensions;
using ECommerce.Shared.Infrastructure.Messaging;
using ECommerce.Shared.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// ✅ ADO.NET Connection Factory
builder.Services.AddAdoNetConnection();

// ✅ Repositories (ADO.NET — no EF Core)
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IOtpRepository, OtpRepository>();

// ✅ JWT Service
builder.Services.AddScoped<IJwtService, JwtService>();

// ✅ Azure Service Bus (optional — local dev mein skip hoga)
builder.Services.AddAzureServiceBus(builder.Configuration);

// ✅ Local development mein Service Bus nahi hai
// Isliye Null Publisher register karo
builder.Services.AddSingleton<IServiceBusPublisher, NullServiceBusPublisher>();

// ✅ JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured");

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
            ClockSkew = TimeSpan.Zero  // Token exact time pe expire hoga
        };
    });

builder.Services.AddAuthorization();

// ✅ CORS — React apps se calls allow karo
builder.Services.AddCors(options => options.AddPolicy("AllowFrontend", policy =>
    policy
        .WithOrigins(
            "http://localhost:3000",   // Buyer App
            "http://localhost:3001",   // Seller App
            "http://localhost:3002"    // Admin App
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()
));

// ✅ Application Insights (Azure monitoring — optional)
if (!string.IsNullOrEmpty(builder.Configuration["ApplicationInsights:ConnectionString"]))
    builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

// ✅ Middleware Pipeline — ORDER BAHUT IMPORTANT HAI!
app.UseMiddleware<RequestLoggingMiddleware>();   // 1. Har request log karo
app.UseMiddleware<GlobalExceptionMiddleware>();  // 2. Exceptions pakdo
app.UseCors("AllowFrontend");                   // 3. CORS
app.UseAuthentication();                        // 4. Who are you?
app.UseAuthorization();                         // 5. What can you do?
app.MapControllers();

Console.WriteLine($"✅ AuthService running on port 5001 | Env: {app.Environment.EnvironmentName}");

app.Run();