using System.Net;
using System.Text.Json;
using Company.Product.Worker.Accessors.Exceptions;
using Company.Product.Worker.Accessors.Mapping;
using Company.Product.Worker.Accessors.Models;
using Company.Product.Worker.Managers.Exceptions;
using Company.Product.Worker.Managers.Models;
using Polly;

namespace Company.Product.Worker.Accessors.Http;

/// <summary>The orders API. The worker never touches the orders database: it goes through the API.</summary>
public interface IOrdersApiAccessor
{
    /// <summary>Creates an order.</summary>
    /// <param name="order">The order to create.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The created order.</returns>
    /// <exception cref="BusinessValidationException">The API rejected the order (400). Retrying won't help.</exception>
    /// <exception cref="ResourceAccessException">The API is unreachable or failing. Retrying may help.</exception>
    Task<OrderDto> CreateAsync(ImportOrderDto order, CancellationToken cancellationToken);

    /// <summary>Finds one order.</summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The order, or <see langword="null"/> if the API doesn't know it.</returns>
    /// <exception cref="ResourceAccessException">The API is unreachable or failing.</exception>
    Task<OrderDto?> FindAsync(Guid orderId, CancellationToken cancellationToken);
}

/// <summary>
/// Calls the orders API through <see cref="IHttpClientFactory"/>. The named client's base address
/// (<c>https+http://orders-api</c>) is resolved by service discovery. No business logic.
/// </summary>
/// <param name="httpClientFactory">Creates the named client per call.</param>
public sealed class OrdersApiAccessor(IHttpClientFactory httpClientFactory) : IOrdersApiAccessor
{
    /// <summary>Name of the <see cref="HttpClient"/> registered for the orders API.</summary>
    public const string HttpClientName = "orders-api";

    /// <inheritdoc />
    public async Task<OrderDto> CreateAsync(ImportOrderDto order, CancellationToken cancellationToken)
    {
        var operation = $"create order '{order.Reference}'";
        using var response = await SendAsync(
            client => client.PostAsJsonAsync("api/orders", order.ToRequestEntity(), cancellationToken),
            operation,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new BusinessValidationException(new Dictionary<string, string[]>
            {
                [order.Reference] = [$"The orders API rejected the order: {problem}"],
            });
        }

        var created = await ReadAsync<OrderEntity>(response, operation, cancellationToken);
        return created.ToDto();
    }

    /// <inheritdoc />
    public async Task<OrderDto?> FindAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var operation = $"get order '{orderId}'";
        using var response = await SendAsync(
            client => client.GetAsync($"api/orders/{orderId}", cancellationToken),
            operation,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        var order = await ReadAsync<OrderEntity>(response, operation, cancellationToken);
        return order.ToDto();
    }

    /// <summary>Sends a request, turning transport failures and 5xx/429 responses into <see cref="ResourceAccessException"/>.</summary>
    /// <param name="send">The call to make.</param>
    /// <param name="operation">Description for error messages.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <returns>The response, for status codes the caller handles (2xx, 400, 404).</returns>
    private async Task<HttpResponseMessage> SendAsync(
        Func<HttpClient, Task<HttpResponseMessage>> send,
        string operation,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await send(httpClientFactory.CreateClient(HttpClientName));
        }
        catch (Exception ex) when (IsTransportFailure(ex, cancellationToken))
        {
            throw new ResourceAccessException($"Could not {operation}: the orders API is unreachable.", ex);
        }

        if (response.IsSuccessStatusCode || response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
        {
            return response;
        }

        var status = response.StatusCode;
        response.Dispose();
        throw new ResourceAccessException(
            $"Could not {operation}: the orders API returned {(int)status}.",
            new HttpRequestException($"Status {(int)status}", null, status));
    }

    /// <summary>Reads a JSON body, turning bad or empty bodies into <see cref="ResourceAccessException"/>.</summary>
    /// <typeparam name="T">Body type.</typeparam>
    /// <param name="response">A successful response.</param>
    /// <param name="operation">Description for error messages.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <returns>The body.</returns>
    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw new JsonException("The response body was empty.");
        }
        catch (JsonException ex)
        {
            throw new ResourceAccessException($"Could not {operation}: the orders API returned an unexpected body.", ex);
        }
    }

    /// <summary>Failures that mean the API couldn't be used, as opposed to the caller cancelling.</summary>
    /// <param name="ex">The caught exception.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <returns><see langword="true"/> if the exception should surface as <see cref="ResourceAccessException"/>.</returns>
    private static bool IsTransportFailure(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException or ExecutionRejectedException
        || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);
}
