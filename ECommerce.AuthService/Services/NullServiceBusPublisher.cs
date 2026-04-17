using ECommerce.Shared.Contracts.Events;
using ECommerce.Shared.Infrastructure.Messaging;

namespace ECommerce.AuthService.Services
{
    // ✅ Local development ke liye — Service Bus nahi hai
    // Production mein real ServiceBusPublisher use hoga
    public class NullServiceBusPublisher : IServiceBusPublisher
    {
        private readonly ILogger<NullServiceBusPublisher> _logger;

        public NullServiceBusPublisher(ILogger<NullServiceBusPublisher> logger)
        {
            _logger = logger;
        }

        public Task PublishAsync<T>(string queueName, T message) where T : BaseEvent
        {
            _logger.LogInformation(
                "LOCAL DEV: Event '{EventType}' would be published to '{Queue}'",
                typeof(T).Name, queueName);

            return Task.CompletedTask;
        }
    }
}