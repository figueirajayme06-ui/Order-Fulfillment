using ExternalConfigurator.Controllers;
using OF.Data.Database;

namespace OF.UI.Models;

public class IndexModel
{
    public string RpcRemoteDomain { get; set; } = "";
    public string Language { get; set; }
    public Header? Header { get; internal set; }
    public string GenericCode { get; internal set; }
    public AgreementQuery Query { get; internal set; }
}