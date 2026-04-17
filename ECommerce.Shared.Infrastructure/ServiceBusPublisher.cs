using Azure.Messaging.ServiceBus;
using ECommerce.Shared.Contracts.Events;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ECommerce.Shared.Infrastructure.Messaging
{
    // ✅ Azure Service Bus pe events publish karta hai
    public interface IServiceBusPublisher
    {
        Task PublishAsync<T>(string queueName, T message) where T : BaseEvent;
    }

    public class ServiceBusPublisher : IServiceBusPublisher
    {
        private readonly ServiceBusClient _client;
        private readonly ILogger<ServiceBusPublisher> _logger;

        public ServiceBusPublisher(
            ServiceBusClient client,
            ILogger<ServiceBusPublisher> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task PublishAsync<T>(string queueName, T message) where T : BaseEvent
        {
            try
            {
                var sender = _client.CreateSender(queueName);

                var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var serviceBusMessage = new ServiceBusMessage(json)
                {
                    MessageId = message.EventId.ToString(),
                    CorrelationId = message.CorrelationId,
                    ContentType = "application/json",
                    Subject = typeof(T).Name
                };

                await sender.SendMessageAsync(serviceBusMessage);

                _logger.LogInformation(
                    "Event published | Type: {EventType} | Id: {EventId} | Queue: {Queue}",
                    typeof(T).Name,
                    message.EventId,
                    queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to publish event | Type: {EventType} | Queue: {Queue}",
                    typeof(T).Name,
                    queueName);
                throw;
            }
        }
    }
}