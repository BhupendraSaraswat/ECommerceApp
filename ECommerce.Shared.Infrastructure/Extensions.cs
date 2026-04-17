using Azure.Messaging.ServiceBus;
using ECommerce.Shared.Infrastructure.Data;
using ECommerce.Shared.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Shared.Infrastructure.Extensions
{
    // ✅ Har service ke Program.cs mein ek line se sab register ho jayega
    public static class ServiceCollectionExtensions
    {
        // ✅ ADO.NET Connection Factory register karo
        public static IServiceCollection AddAdoNetConnection(
            this IServiceCollection services)
        {
            services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
            return services;
        }

        // ✅ Azure Service Bus register karo
        public static IServiceCollection AddAzureServiceBus(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration["AzureServiceBus:ConnectionString"];

            if (string.IsNullOrEmpty(connectionString))
            {
                // Local development mein Service Bus nahi hoga — skip karo
                return services;
            }

            services.AddSingleton(new ServiceBusClient(connectionString));
            services.AddSingleton<IServiceBusPublisher, ServiceBusPublisher>();

            return services;
        }
    }
}