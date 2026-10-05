using System;
using System.Text.Json.Serialization;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;

namespace OF.Common.Infrastructure.IPG.Orders.Models.ChangeOrder
{
    public class SalesforceChangeOrder : SfObjectBase
    {
        [JsonPropertyName("Id")]
        public string? Id { get; set; }

        [JsonPropertyName("Delivery_Day_Time__c")]
        public DateTime? DeliveryDayTime { get; set; }

        [JsonPropertyName("Customer_Delivery_Date__c")]
        public string? CustomerDeliveryDate { get; set; }

        [JsonPropertyName("PS_AG_DeliveryStartTime__c")]
        public string? DeliveryStartTime { get; set; }

        [JsonPropertyName("PS_AG_DeliveryEndTime__c")]
        public string? DeliveryEndTime { get; set; }

        [JsonPropertyName("Onhire_Date__c")]
        public string? OnhireDate { get; set; }

        [JsonPropertyName("Offhire_Date__c")]
        public string? OffhireDate { get; set; }

        [JsonPropertyName("Collection_Day_Time__c")]
        public DateTime? CollectionDayTime { get; set; }

        [JsonPropertyName("Customer_Pickup_Date__c")]
        public string? CustomerPickupDate { get; set; }

        [JsonPropertyName("PS_AG_PickUpStartTime__c")]
        public string? PickUpStartTime { get; set; }

        [JsonPropertyName("PS_AG_PickUpEndTime__c")]
        public string? PickUpEndTime { get; set; }

        [JsonPropertyName("Terminated_Date__c")]
        public string? TerminatedDate { get; set; }

        [JsonPropertyName("PO_Number__c")]
        public string? PoNumber { get; set; }

        [JsonPropertyName("PO_Amount__c")]
        public decimal? PoAmount { get; set; }

        [JsonPropertyName("PS_AG_Project_Code__c")]
        public string? ProjectCode { get; set; }

        [JsonPropertyName("Sales_Person_ID__c")]
        public string? SalesPersonId { get; set; }

        [JsonPropertyName("PS_AG_Sales_Rep__c")]
        public string? SalesRep { get; set; }

        [JsonPropertyName("Job_AIC__c")]
        public string? JobAic { get; set; }

        [JsonPropertyName("Rental_Period__c")]
        public double? RentalPeriod { get; set; }

        [JsonPropertyName("M3_Rate_Type__c")]
        public string? RateType { get; set; }

        [JsonPropertyName("Days_in_Week__c")]
        public string? DaysInWeek { get; set; }

        [JsonPropertyName("Weeks_in_Month__c")]
        public string? WeeksInMonth { get; set; }

        [JsonPropertyName("SBQQ__Quote__r")]
        public SalesforceChangeOrderQuote? QuoteReference { get; set; }

        [JsonPropertyName("PS_AG_ARM_Contact__r")]
        public SalesforceChangeOrderContact? ArmContact { get; set; }

        [JsonPropertyName("PS_AG_Primary_Contact__r")]
        public SalesforceChangeOrderContact? PrimaryContact { get; set; }

        [JsonPropertyName("PS_AG_Site_Contact__r")]
        public SalesforceChangeOrderContact? SiteContact { get; set; }

        [JsonPropertyName("PS_AG_Billing_Contact__r")]
        public SalesforceChangeOrderContact? BillingContact { get; set; }

        [JsonPropertyName("FSM_Assigned_Owner__c")]
        public string? AssignedOwner { get; set; }

        [JsonPropertyName("OwnerId")]
        public string? OwnerId { get; set; }

        [JsonPropertyName("BillingAddress")]
        public SalesforceChangeOrderAddress? BillingAddress { get; set; }

        [JsonPropertyName("ShippingAddress")]
        public SalesforceChangeOrderAddress? ShippingAddress { get; set; }
    }
}
