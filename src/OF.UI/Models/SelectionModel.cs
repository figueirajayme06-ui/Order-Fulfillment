using ExternalConfigurator.Controllers;
using OF.Data.Database;

namespace OF.UI.Models;

public class SelectionModel
{
    public string Language { get; set; }
    public Header? Header { get; internal set; }
    public AgreementQuery Query { get; internal set; }
}