namespace OF.Common.Infrastructure.IPG.Pricing.Models
{
    public class CalculationResult
    {
        public bool Successful { get; set; }
        public string Id { get; set; }
        public string Duration { get; set; }
        public CalculationOptions Options { get; set; }
        public QuotePrice Quote { get; set; }
        public CalculationLine[] Lines { get; set; }
    }
    public class CalculationLine
    {
        public bool Successful { get; set; }
        public CalculationQuoteLine QuoteLine { get; set; }
        public Calculation Calculation { get; set; }
    }

    public class CalculationQuoteLine
    {
        public string Id { get; set; }
        public string Generic { get; set; }
        public float Quantity { get; set; }
        public CalculationFeatures Features { get; set; }
        public DateTime OnHire { get; set; }
        public DateTime OffHire { get; set; }
    }

    public class CalculationFeatures
    {
        public bool ColdWeatherPackage { get; set; }
        public bool WithPump { get; set; }
        public bool ExplosionProtected { get; set; }
        public bool EpaEmissionRequirement { get; set; }
        public bool TrailerMounted { get; set; }
        public bool HighVoltage { get; set; }
        public bool DistributionPackage { get; set; }
        public bool IsIndustrial { get; set; }
        public bool IsEvent { get; set; }
        public bool WithHeat { get; set; }
        public bool HighPressure { get; set; }
        public bool Portable { get; set; }
        public bool CrashFrame { get; set; }
        public bool WithAftercooler { get; set; }
        public bool DuelFuelSource { get; set; }
        public string CoolingMethod { get; set; }
        public string ProductStyle { get; set; }
        public string LeavingFluidTemp { get; set; }
        public string TemperatureRange { get; set; }
        public string ShiftFactor { get; set; }
    }

    public class Calculation
    {
        public decimal ListPrice {  get; set; }
        public bool IsContractPricing { get; set; }
        public string OnHireSource { get; set; }
        public string OffHireSource { get; set; }
    }
}
