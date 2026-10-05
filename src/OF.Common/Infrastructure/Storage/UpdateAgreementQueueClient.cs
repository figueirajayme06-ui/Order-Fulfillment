using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;

namespace OF.Common.Infrastructure.Storage
{
    public class UpdateAgreementQueueClient
    {
        private readonly ServiceBusSender sender;

        public UpdateAgreementQueueClient(ServiceBusClient queueClient)
        {
            sender = queueClient.CreateSender(Constants.Queues.UpdateByAgreement);
        }

        public async Task QueueAgreement(string agreementNumber)
        {
            var json = JsonConvert.SerializeObject(new { AgreementNumber = agreementNumber });
            ServiceBusMessage message = new ServiceBusMessage(json);
            await sender.SendMessageAsync(message);
        }
    }
}
