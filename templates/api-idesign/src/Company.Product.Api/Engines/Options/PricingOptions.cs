namespace Company.Product.Api.Engines.Options;

/// <summary>Pricing settings, bound from the <c>Pricing</c> configuration section.</summary>
public sealed class PricingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Pricing";

    /// <summary>Subtotal at or above which the discount applies.</summary>
    public decimal DiscountThreshold { get; set; } = 500m;

    /// <summary>Discount as a percentage of the subtotal (5 = 5%).</summary>
    public decimal DiscountPercent { get; set; } = 5m;
}
