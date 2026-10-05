using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;

namespace OF.Common.Infrastructure.Storage
{
    public interface IActivateHeaderQueueClient
    {
        Task QueueActivation(int headerId, IDictionary<string, object>? properties = null, CancellationToken cancellationToken = default);
    }

    public class ActivateHeaderQueueClient : IActivateHeaderQueueClient
    {
        private readonly ServiceBusSender sender;

        public ActivateHeaderQueueClient(ServiceBusClient queueClient)
        {
            sender = queueClient.CreateSender(Constants.Queues.ActivateHeader);
        }

        public Task QueueActivation(int headerId, IDictionary<string, object>? properties, CancellationToken cancellationToken = default)
        {
            var json = JsonConvert.SerializeObject(new { HeaderId = headerId });
            var message = new ServiceBusMessage(json);

            if (properties is not null)
            {
                foreach (var (k, v) in properties)
                {
                    message.ApplicationProperties[k] = v;
                }
            }

            return sender.ScheduleMessageAsync(message, DateTime.UtcNow.AddMinutes(1), cancellationToken);
        }
    }
}
