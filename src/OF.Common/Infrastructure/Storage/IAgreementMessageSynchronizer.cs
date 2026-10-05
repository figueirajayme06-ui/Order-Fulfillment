namespace OF.Common.Infrastructure.Storage;
public interface IAgreementMessageSynchronizer
{
    Task QueueMessage(string sessionId, string messageBody, string operation, string? correlationId = null);
}
