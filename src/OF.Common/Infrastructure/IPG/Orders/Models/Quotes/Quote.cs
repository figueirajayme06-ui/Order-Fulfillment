using Newtonsoft.Json;

namespace OF.Common.Infrastructure.IPG.Orders.Models.Quotes
{
    public class Quote : SfObjectBase
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        [JsonProperty("SBQQ__PrimaryContact__r")]
        public required OpportunityContact Contact { get; set; }

        [JsonProperty("Off_Hire_Date__c")]
        public DateTime? OffHireDate { get; set; }

        [JsonProperty("On_Hire_Date__c")]
        public DateTime? OnHireDate { get; set; }

        [JsonProperty("Aggreko_Warehouse__r")]
        public OppWarehouse? Warehouse { get; set; }

        [JsonProperty("OverviewOfServicesContent__c")]
        public string? OverviewOfServices { get; set; }

        // Work Plan

        [JsonProperty("ARM_Required__c")]
        public bool ARMRequired { get; set; }

        [JsonProperty("ARM_Contact_Hours__c")]
        public string? ARMContactHours { get; set; }

        [JsonProperty("ARM_Contact_Info__c")]
        public string? ARMContactInfo { get; set; }

        [JsonProperty("Freight_Method__c")]
        public string? FreightMethod { get; set; }

        [JsonProperty("Freight_Details__c")]
        public string? FreightDetails { get; set; }

        [JsonProperty("Fuel_Service_Selected__c")]
        public string? FuelServiceSelected { get; set; }

        [JsonProperty("Fuel_Service_Details__c")]
        public string? FuelServiceDetails { get; set; }

        [JsonProperty("Operational_Requirements_Details__c")]
        public string? OperationalRequirementsDetails { get; set; }

        [JsonProperty("Site_Specific_Requirements__c")]
        public string? SiteSpecificRequirements { get; set; }

        [JsonProperty("Order_Notes__c")]
        public string? OrderNotes { get; set; }

        [JsonProperty("M3_Rate_Type__c")]
        public string? RateType { get; set; }

        [JsonProperty("Aggreko_Tech_Required__c")]
        public bool AggrekoTechRequired { get; set; }

        [JsonProperty("Technician_Requirement_Details__c")]
        public string? TechnicianRequirementDetails { get; set; }

        [JsonProperty("Technical_Requirements__c")]
        public string? TechnicalRequirements { get; set; }

        [JsonProperty("CollectionDayTime__c")]
        public DateTime? CollectionDate { get; set; }

        [JsonProperty("Proposal_Contact_Name__c")]
        public string? ProposalContactName { get; set; }

        [JsonProperty("Proposal_Contact_Email__c")]
        public string? ProposalContactEmail { get; set; }

        [JsonProperty("SBQQ__Notes__c")]
        public string? QuoteNotes { get; set; }

        [JsonProperty("Site_Contact_Name__c")]
        public string? SiteContactName { get; set; }

        [JsonProperty("Site_Contact_Email__c")]
        public string? SiteContactEmail { get; set; }

        [JsonProperty("Site_Contact_Mobile__c")]
        public string? SiteContactMobile { get; set; }

        [JsonProperty("M3OrderNumber__c")]
        public string? M3OrderNumber { get; set; }

        [JsonProperty("M3CustomerNumber__c")]
        public string? M3CustomerNumber { get; set; }

        [JsonProperty("m3MasterOrderId__r")]
        public QuoteOrder? Order { get; set; }

        [JsonProperty("Customer_Delivery_Date__c")]
        public DateTime? DeliveryDate { get; set; }

        [JsonProperty("PS_AG_CustomerDeliverySlot__c")]
        public string? DeliverySlot { get; set; }

        [JsonProperty("PS_AG_DeliveryStartTime__c")]
        public string? DeliverySlotStartTime { get; set; }

        [JsonProperty("PS_AG_DeliveryEndTime__c")]
        public string? DeliverySlotEndTime { get; set; }
    }

    public class QuoteOrder : SfObjectBase
    {
        public string? OrderNumber { get; set; }

        [JsonProperty("PS_AG_Shipping_Address_Details__c")]
        public string? ShippingAddress { get; set; }

        [JsonProperty("FSM_Assigned_Owner__c")]
        public string? OwnerName { get; set; }
    }
}
