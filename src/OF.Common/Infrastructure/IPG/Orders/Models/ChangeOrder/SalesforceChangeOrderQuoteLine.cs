using System.Text.Json.Serialization;

namespace OF.Common.Infrastructure.IPG.Orders.Models.ChangeOrder
{
    public class SalesforceChangeOrderQuoteLine
    {
        [JsonPropertyName("Rate_Type__c")]
        public string? RateType { get; set; }

        [JsonPropertyName("Price_Adjustment__c")]
        public decimal? PriceAdjustment { get; set; }

        [JsonPropertyName("Duration_Price2__c")]
        public decimal? DurationPrice2 { get; set; }

        [JsonPropertyName("SBQQ__ProductName__c")]
        public string? ProductName { get; set; }

        [JsonPropertyName("SBQQ__Quantity__c")]
        public decimal? Quantity { get; set; }

        [JsonPropertyName("Daily_Rate_Final__c")]
        public decimal? DailyRateFinal { get; set; }

        [JsonPropertyName("Adj_Daily_Rate_unit__c")]
        public decimal? AdjDailyRateUnit { get; set; }

        [JsonPropertyName("Daily_F_Rate__c")]
        public decimal? DailyFRate { get; set; }

        [JsonPropertyName("Daily_M_Rate__c")]
        public decimal? DailyMRate { get; set; }

        [JsonPropertyName("Daily_P_Rate__c")]
        public decimal? DailyPRate { get; set; }

        [JsonPropertyName("Daily_Rate__c")]
        public decimal? DailyRate { get; set; }

        [JsonPropertyName("Weekly_Rate_Final__c")]
        public decimal? WeeklyRateFinal { get; set; }

        [JsonPropertyName("Adj_Weekly_Rate_unit__c")]
        public decimal? AdjWeeklyRateUnit { get; set; }

        [JsonPropertyName("Weekly_F_Rate__c")]
        public decimal? WeeklyFRate { get; set; }

        [JsonPropertyName("Weekly_M_Rate__c")]
        public decimal? WeeklyMRate { get; set; }

        [JsonPropertyName("Weekly_P_Rate__c")]
        public decimal? WeeklyPRate { get; set; }

        [JsonPropertyName("Weekly_Rate__c")]
        public decimal? WeeklyRate { get; set; }

        [JsonPropertyName("Weekly_Rate_No_Currency__c")]
        public decimal? WeeklyRateNoCurrency { get; set; }

        [JsonPropertyName("Monthly_Rate_Final__c")]
        public decimal? MonthlyRateFinal { get; set; }

        [JsonPropertyName("Adj_Monthly_Rate_unit__c")]
        public decimal? AdjMonthlyRateUnit { get; set; }

        [JsonPropertyName("Monthly_F_Rate__c")]
        public decimal? MonthlyFRate { get; set; }

        [JsonPropertyName("Monthly_M_Rate__c")]
        public decimal? MonthlyMRate { get; set; }

        [JsonPropertyName("Monthly_P_Rate__c")]
        public decimal? MonthlyPRate { get; set; }

        [JsonPropertyName("Monthly_Rate__c")]
        public decimal? MonthlyRate { get; set; }
    }
}
