using Company.Product.Api.Accessors.Exceptions;
using Company.Product.Api.Accessors.Mapping;
using Company.Product.Api.Accessors.Models;
using Company.Product.Api.Managers.Models;
using Polly;

namespace Company.Product.Api.Accessors.Http;

/// <summary>Stock levels from the inventory service.</summary>
public interface IInventoryApiAccessor
{
    /// <summary>Gets the units on hand for each SKU.</summary>
    /// <param name="skus">SKUs to look up.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One stock level per SKU the inventory service knows. Unknown SKUs are left out.</returns>
    /// <exception cref="ResourceAccessException">The inventory service is unreachable, failing, or returned an invalid response.</exception>
    Task<IReadOnlyList<StockLevelDto>> GetStockLevelsAsync(IReadOnlyCollection<string> skus, CancellationToken cancellationToken);
}

/// <summary>
/// Calls the inventory service through <see cref="IHttpClientFactory"/>. The named client's base address
/// (<c>https+http://inventory</c>) is resolved by service discovery. No business logic.
/// </summary>
/// <param name="httpClientFactory">Creates the named client per call, so this Singleton never holds a stale connection.</param>
public sealed class InventoryApiAccessor(IHttpClientFactory httpClientFactory) : IInventoryApiAccessor
{
    /// <summary>Name of the <see cref="HttpClient"/> registered for the inventory service.</summary>
    public const string HttpClientName = "inventory";

    /// <inheritdoc />
    public async Task<IReadOnlyList<StockLevelDto>> GetStockLevelsAsync(
        IReadOnlyCollection<string> skus,
        CancellationToken cancellationToken)
    {
        if (skus.Count == 0)
        {
            return [];
        }

        var query = string.Join("&", skus.Select(sku => $"sku={Uri.EscapeDataString(sku)}"));

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);

            var stockLevels = await client.GetFromJsonAsync<List<StockLevelEntity>>(
                $"api/stock?{query}",
                cancellationToken);

            return (stockLevels ?? []).Select(stockLevel => stockLevel.ToDto()).ToList();
        }
        catch (Exception ex) when (IsTransportFailure(ex, cancellationToken))
        {
            throw new ResourceAccessException("Could not read stock levels from the inventory service.", ex);
        }
    }

    /// <summary>Failures that mean the inventory service couldn't be used, as opposed to the caller cancelling.</summary>
    /// <param name="ex">The caught exception.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <returns><see langword="true"/> if the exception should surface as <see cref="ResourceAccessException"/>.</returns>
    private static bool IsTransportFailure(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException or ExecutionRejectedException or System.Text.Json.JsonException
        || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);
}
