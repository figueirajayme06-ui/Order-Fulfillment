using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;

namespace OF.Common.Infrastructure.Storage
{
    public class ActivateAgreementQueueClient
    {
        private readonly ServiceBusSender sender;

        public ActivateAgreementQueueClient(ServiceBusClient queueClient)
        {
            sender = queueClient.CreateSender(Constants.Queues.ActivateAgreement);
        }

        public async Task QueueActivation(int headerId)
        {
            var json = JsonConvert.SerializeObject(new { HeaderId = headerId });
            ServiceBusMessage message = new ServiceBusMessage(json);
            await sender.SendMessageAsync(message);
        }
    }
}
