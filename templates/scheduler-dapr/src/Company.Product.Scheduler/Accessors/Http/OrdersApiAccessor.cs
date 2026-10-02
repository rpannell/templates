using System.Net;
using System.Text.Json;
using Company.Product.Scheduler.Accessors.Exceptions;
using Company.Product.Scheduler.Accessors.Mapping;
using Company.Product.Scheduler.Accessors.Models;
using Company.Product.Scheduler.Managers.Models;
using Polly;

namespace Company.Product.Scheduler.Accessors.Http;

/// <summary>The orders API. The scheduler never touches the orders database: it goes through the API.</summary>
public interface IOrdersApiAccessor
{
    /// <summary>Lists the newest orders.</summary>
    /// <param name="take">Maximum number of orders (the API allows up to 200).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Orders, newest first.</returns>
    /// <exception cref="ResourceAccessException">The API is unreachable or failing.</exception>
    Task<IReadOnlyList<OrderDto>> ListRecentAsync(int take, CancellationToken cancellationToken);

    /// <summary>Calls the API's <c>/health</c> endpoint. Never throws for an unhealthy API: that's a result, not an error.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Whether the API is healthy, with the status or error and the duration.</returns>
    Task<DependencyHealthDto> CheckHealthAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Calls the orders API through <see cref="IHttpClientFactory"/>. The named client's base address
/// (<c>https+http://orders-api</c>) is resolved by service discovery. No business logic.
/// </summary>
/// <param name="httpClientFactory">Creates the named client per call.</param>
/// <param name="timeProvider">Clock used to time health checks.</param>
public sealed class OrdersApiAccessor(IHttpClientFactory httpClientFactory, TimeProvider timeProvider) : IOrdersApiAccessor
{
    /// <summary>Name of the <see cref="HttpClient"/> registered for the orders API, and the dependency name in metrics.</summary>
    public const string HttpClientName = "orders-api";

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrderDto>> ListRecentAsync(int take, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClientFactory.CreateClient(HttpClientName)
                .GetAsync($"api/orders?skip=0&take={take}", cancellationToken);
        }
        catch (Exception ex) when (IsTransportFailure(ex, cancellationToken))
        {
            throw new ResourceAccessException("Could not list orders: the orders API is unreachable.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new ResourceAccessException(
                    $"Could not list orders: the orders API returned {(int)response.StatusCode}.",
                    new HttpRequestException($"Status {(int)response.StatusCode}", null, response.StatusCode));
            }

            try
            {
                var orders = await response.Content.ReadFromJsonAsync<List<OrderEntity>>(cancellationToken)
                    ?? throw new JsonException("The response body was empty.");
                return orders.Select(order => order.ToDto()).ToList();
            }
            catch (JsonException ex)
            {
                throw new ResourceAccessException("Could not list orders: the orders API returned an unexpected body.", ex);
            }
        }
    }

    /// <inheritdoc />
    public async Task<DependencyHealthDto> CheckHealthAsync(CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        try
        {
            using var response = await httpClientFactory.CreateClient(HttpClientName).GetAsync("health", cancellationToken);
            return new DependencyHealthDto(
                HttpClientName,
                response.IsSuccessStatusCode,
                $"HTTP {(int)response.StatusCode}",
                timeProvider.GetElapsedTime(started));
        }
        catch (Exception ex) when (IsTransportFailure(ex, cancellationToken))
        {
            return new DependencyHealthDto(HttpClientName, false, ex.Message, timeProvider.GetElapsedTime(started));
        }
    }

    /// <summary>Failures that mean the API couldn't be used, as opposed to the caller cancelling.</summary>
    /// <param name="ex">The caught exception.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <returns><see langword="true"/> for a transport failure.</returns>
    private static bool IsTransportFailure(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException or ExecutionRejectedException
        || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);
}
