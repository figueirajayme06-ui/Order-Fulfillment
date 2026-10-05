namespace OF.Common.Infrastructure;

/// <summary>
/// Marker interface to get a correlation from a model. Works well with Refit model as HTTP request content injected in the HttpRequestMessage.Options
/// </summary>
public interface ICorrelatable
{
    string CorrelationId { get; }
}
