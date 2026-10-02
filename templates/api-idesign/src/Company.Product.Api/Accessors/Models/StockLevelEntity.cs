namespace Company.Product.Api.Accessors.Models;

/// <summary>Item shape returned by the inventory service's <c>GET api/stock</c>. Never leaves the Accessors namespace.</summary>
public sealed class StockLevelEntity
{
    /// <summary>Stock keeping unit.</summary>
    public string Sku { get; set; } = string.Empty;

    /// <summary>Units on hand in the warehouse.</summary>
    public int QuantityOnHand { get; set; }
}
