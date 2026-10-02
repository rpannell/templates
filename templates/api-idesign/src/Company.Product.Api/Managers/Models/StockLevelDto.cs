namespace Company.Product.Api.Managers.Models;

/// <summary>Units on hand for one SKU.</summary>
/// <param name="Sku">Stock keeping unit.</param>
/// <param name="Available">Units on hand.</param>
public sealed record StockLevelDto(string Sku, int Available);
