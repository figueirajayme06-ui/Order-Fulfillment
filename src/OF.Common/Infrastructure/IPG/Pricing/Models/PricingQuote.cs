using System.Globalization;

namespace OF.Common.Infrastructure.IPG.Pricing.Models;


public class PricingRequest
{
    public string? Id { get; set; }
    public required PricingQuote Quote { get; set; }
}

public class PricingQuote
{
    public PricingOptions? Options { get; set; }
    public required PricingDetails Details { get; set; }
    public required PricingLine[] Lines { get; set; }
}

public class PricingOptions
{
    public bool ContinueOnFailedLine { get; set; } = true;
    public bool OutputSteps { get; set; } = false;
}


public class PricingDetails
{ 
    public required string CurrencyISOCode { get; set; }
    public required string Division { get; set; }
    public string? Id { get; set; }
    public required string OnHire { get; set; }
    public string OffHireDate => DateTime.ParseExact(OffHire, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToUniversalTime().ToString("o");
    public required string OffHire { get; set; }
    public string OnHireDate => DateTime.ParseExact(OnHire, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToUniversalTime().ToString("o");
    public required string Warehouse { get; set; }
    public required string JobAICCode { get; set; }
}

public class PricingLine
{
    public PricingFeatures? Features { get; set; }
    public required string Generic { get; set; }
    public string? Id { get; set; }
    public required string OffHire { get; set; }
    public string OffHireDate => DateTime.ParseExact(OffHire, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToUniversalTime().ToString("o");
    public required string OnHire { get; set; }
    public string OnHireDate => DateTime.ParseExact(OnHire, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToUniversalTime().ToString("o");
    public required int Quantity { get; set; }
}

public class PricingFeatures
{
    public bool ColdWeatherPackage { get; set; }
    public string? CoolingMethod { get; set; }
    public bool CrashFrame { get; set; }
    public bool DistributionPackage { get; set; }
    public bool DuelFuelSource { get; set; }
    public bool EpaEmissionRequirement { get; set; }
    public bool ExplosionProtected { get; set; }
    public bool HighPressure { get; set; }
    public bool HighVoltage { get; set; }
    public bool IsEvent { get; set; }
    public bool IsIndustrial { get; set; }
    public string? LeavingFluidTemp { get; set; }
    public bool Portable { get; set; }
    public string? ProductStyle { get; set; }
    public string? ShiftFactor { get; set; }
    public string? TemperatureRange { get; set; }
    public bool TrailerMounted { get; set; }
    public bool WithAftercooler { get; set; }
    public bool WithHeat { get; set; }
    public bool WithPump { get; set; }
}
