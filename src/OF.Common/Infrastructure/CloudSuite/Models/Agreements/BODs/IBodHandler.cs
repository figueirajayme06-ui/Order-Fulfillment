namespace OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs;
public interface IBodHandler
{
    static abstract string GetSessionId(string body);
    Task Handle(string body);
}