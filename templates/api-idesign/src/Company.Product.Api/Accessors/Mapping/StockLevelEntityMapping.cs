using Company.Product.Api.Accessors.Models;
using Company.Product.Api.Managers.Models;

namespace Company.Product.Api.Accessors.Mapping;

/// <summary>Maps inventory service responses to Dtos.</summary>
internal static class StockLevelEntityMapping
{
    /// <summary>Maps a stock level from the inventory service to a Dto.</summary>
    /// <param name="stockLevel">The inventory service's stock level.</param>
    /// <returns>The stock level Dto.</returns>
    public static StockLevelDto ToDto(this StockLevelEntity stockLevel) =>
        new(stockLevel.Sku, stockLevel.QuantityOnHand);
}
