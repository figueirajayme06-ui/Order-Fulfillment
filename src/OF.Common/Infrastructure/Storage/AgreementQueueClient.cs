using Azure.Messaging.ServiceBus;

namespace OF.Common.Infrastructure.Storage;

public class AgreementQueueClient : IAgreementMessageSynchronizer 
{
    private readonly ServiceBusSender sender;

    public AgreementQueueClient(ServiceBusClient client)
    {
        sender = client.CreateSender(Constants.Queues.AgreementSync);
    }

    

    public async Task QueueMessage(
        string sessionId,
        string messageBody,
        string operation,
        string? correlationId = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Session ID cannot be null or whitespace.", nameof(sessionId));
        }

        ServiceBusMessage message = new(messageBody);
        message.SessionId = sessionId;
        message.ApplicationProperties[Constants.AgreementSync.QueueOperationKey] = operation;
        message.CorrelationId = correlationId ?? Guid.NewGuid().ToString();

        await sender.SendMessageAsync(message);
    }
}
