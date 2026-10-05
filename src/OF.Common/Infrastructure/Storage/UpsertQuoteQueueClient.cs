using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;

namespace OF.Common.Infrastructure.Storage
{
    public class UpsertQuoteQueueClient
    {
        private readonly ServiceBusSender sender;

        public UpsertQuoteQueueClient(ServiceBusClient queueClient)
        {
            sender = queueClient.CreateSender(Constants.Queues.UpsertQuote);
        }

        public async Task QueueQuote(string quoteNumber)
        {
            var json = JsonConvert.SerializeObject(new { QuoteNumber = quoteNumber });
            ServiceBusMessage message = new ServiceBusMessage(json);
            await sender.SendMessageAsync(message);
        }
    }
}
