namespace OF.Common.Extensions;
public static class ServiceBusReceivedMessageExtensions
{
    public static string GetBodyAsString(this Azure.Messaging.ServiceBus.ServiceBusReceivedMessage message)
    {
        return message.Body?.ToString() ?? string.Empty;
    }
}
