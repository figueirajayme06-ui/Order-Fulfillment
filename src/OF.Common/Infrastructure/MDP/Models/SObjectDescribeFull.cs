using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace OF.Common.Infrastructure.MDP.Models;

[Keyless]
public class SObjectDescribeFull
{
    [JsonProperty(PropertyName = "fields")]
    public List<SObjectFieldMetadata> Fields { get; set; }

    public List<PickListValue> GetPicklistValues(string fieldName)
    {
        List<PickListValue> result = new List<PickListValue>();
        if (Fields != null && Fields.Count > 0)
        {
            result = (from f in Fields
                      where f.Name == fieldName
                      select f.PicklistValues).SingleOrDefault();
        }

        return result;
    }
}

[Keyless]
public class SObjectFieldMetadata
{
    [JsonProperty(PropertyName = "autoNumber")]
    public bool AutoNumber { get; set; }

    [JsonProperty(PropertyName = "calculated")]
    public bool Calculated { get; set; }

    [JsonProperty(PropertyName = "calculatedFormula")]
    public string CalculatedFormula { get; set; }

    [JsonProperty(PropertyName = "createable")]
    public bool Creatable { get; set; }

    [JsonProperty(PropertyName = "custom")]
    public bool Custom { get; set; }

    [JsonProperty(PropertyName = "defaultValue")]
    public string DefaultValue { get; set; }

    [JsonProperty(PropertyName = "externalId")]
    public string ExternalId { get; set; }

    [JsonProperty(PropertyName = "htmlFormatted")]
    public bool HtmlFormatted { get; set; }

    [JsonProperty(PropertyName = "inlineHelpText")]
    public string InlineHelpText { get; set; }

    [JsonProperty(PropertyName = "label")]
    public string Label { get; set; }

    [JsonProperty(PropertyName = "length")]
    public int Length { get; set; }

    [JsonProperty(PropertyName = "name")]
    public string Name { get; set; }

    [JsonProperty(PropertyName = "nillable")]
    public bool Nillable { get; set; }

    [JsonProperty(PropertyName = "picklistValues")]
    public List<PickListValue> PicklistValues { get; set; }

    [JsonProperty(PropertyName = "referenceTo")]
    public List<string> ReferenceTo { get; set; }

    [JsonProperty(PropertyName = "relationshipName")]
    public string RelationshipName { get; set; }

    [JsonProperty(PropertyName = "type")]
    public string Type { get; set; }

    [JsonProperty(PropertyName = "unique")]
    public bool Unique { get; set; }

    [JsonProperty(PropertyName = "updateable")]
    public bool Updateable { get; set; }
}

[Keyless]
public class PickListValue
{
    [JsonProperty(PropertyName = "active")]
    public bool Active { get; set; }

    [JsonProperty(PropertyName = "defaultValue")]
    public bool DefaultValue { get; set; }

    [JsonProperty(PropertyName = "label")]
    public string Label { get; set; }

    [JsonProperty(PropertyName = "validFor")]
    public byte[] ValidFor { get; set; }

    [JsonProperty(PropertyName = "value")]
    public string Value { get; set; }
}