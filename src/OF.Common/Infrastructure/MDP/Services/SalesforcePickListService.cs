using Newtonsoft.Json;
using OF.Common.Infrastructure.MDP.Models;
using OF.Common.Infrastructure.MDP.Resources;

namespace OF.Common.Infrastructure.MDP.Services;

public class SalesforcePickListService
{
    public static Dictionary<string, string> Segments()
    {
        SObjectDescribeFull quoteLine = JsonConvert.DeserializeObject<SObjectDescribeFull>(SalesforceSchemas.Contact)!;

        var fields = quoteLine.Fields
            .Where(f => f.Name == "CAP_AG_Contact_Segment__c")
            .SelectMany(f => f.PicklistValues)
            .ToDictionary(p => p.Value, p => p.Label);

        return fields;
    }

    public static Dictionary<string, string> Salutations()
    {
        SObjectDescribeFull quoteLine = JsonConvert.DeserializeObject<SObjectDescribeFull>(SalesforceSchemas.Contact)!;

        return quoteLine.Fields
            .Where(f => f.Name == "Salutation")
            .SelectMany(f => f.PicklistValues)
            .ToDictionary(p => p.Value, p => p.Label);
    }
}
