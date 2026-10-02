using System.Diagnostics.Metrics;
using Company.Product.Api.Accessors;
using Company.Product.Api.Accessors.Http;
using Company.Product.Api.Engines;
using Company.Product.Api.Managers.Exceptions;
using Company.Product.Api.Managers.Models;
using Company.Product.Api.Utilities.Telemetry;

namespace Company.Product.Api.Managers;

/// <summary>Order use cases.</summary>
public interface IOrderManager
{
    /// <summary>Validates, prices and stores a new order.</summary>
    /// <param name="order">The order to create.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The priced, stored order.</returns>
    /// <exception cref="BusinessValidationException">The order breaks a business rule.</exception>
    Task<OrderDto> CreateAsync(CreateOrderDto order, CancellationToken cancellationToken);

    /// <summary>Gets one order.</summary>
    /// <param name="orderId">Identifier of the order.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The order.</returns>
    /// <exception cref="OrderNotFoundException">No order has that identifier.</exception>
    Task<OrderDto> GetAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Lists orders, newest first.</summary>
    /// <param name="skip">Number of orders to skip.</param>
    /// <param name="take">Maximum number of orders to return.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One page of orders.</returns>
    /// <exception cref="BusinessValidationException">The paging window is out of range.</exception>
    Task<IReadOnlyList<OrderDto>> ListAsync(int skip, int take, CancellationToken cancellationToken);

    /// <summary>Checks whether current stock in the inventory service can fulfill an order.</summary>
    /// <param name="orderId">Identifier of the order.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Per-line availability and whether the whole order can be fulfilled.</returns>
    /// <exception cref="OrderNotFoundException">No order has that identifier.</exception>
    Task<OrderAvailabilityDto> GetAvailabilityAsync(Guid orderId, CancellationToken cancellationToken);
}

/// <summary>
/// Order use cases. Sequences Engine and Accessor calls; holds no business rules and does no I/O itself.
/// </summary>
/// <param name="validationEngine">Order business rules.</param>
/// <param name="pricingEngine">Order pricing calculations.</param>
/// <param name="fulfillmentEngine">Order fulfillment rules.</param>
/// <param name="orderAccessor">Order storage.</param>
/// <param name="inventoryAccessor">Stock levels from the inventory service.</param>
/// <param name="timeProvider">Source of the current time.</param>
/// <param name="logger">Logger.</param>
public sealed class OrderManager(
    IOrderValidationEngine validationEngine,
    IOrderPricingEngine pricingEngine,
    IOrderFulfillmentEngine fulfillmentEngine,
    IOrderAccessor orderAccessor,
    IInventoryApiAccessor inventoryAccessor,
    TimeProvider timeProvider,
    ILogger<OrderManager> logger) : IOrderManager
{
    private static readonly Counter<long> OrdersCreated = ApplicationTelemetry.Meter.CreateCounter<long>(
        "orders.created", unit: "{order}", description: "Orders created.");

    /// <inheritdoc />
    public async Task<OrderDto> CreateAsync(CreateOrderDto order, CancellationToken cancellationToken)
    {
        using var activity = ApplicationTelemetry.ActivitySource.StartActivity("OrderManager.Create");

        validationEngine.Validate(order);

        var priced = pricingEngine.Price(Guid.CreateVersion7(), order, timeProvider.GetUtcNow());

        await orderAccessor.SaveAsync(priced, cancellationToken);

        activity?.SetTag("order.id", priced.Id);
        OrdersCreated.Add(1);

        logger.LogInformation(
            "Created order {OrderId} for {CustomerName} totaling {Total}",
            priced.Id, priced.CustomerName, priced.Total);

        return priced;
    }

    /// <inheritdoc />
    public async Task<OrderDto> GetAsync(Guid orderId, CancellationToken cancellationToken) =>
        await orderAccessor.FindAsync(orderId, cancellationToken)
            ?? throw new OrderNotFoundException(orderId);

    /// <inheritdoc />
    public Task<IReadOnlyList<OrderDto>> ListAsync(int skip, int take, CancellationToken cancellationToken)
    {
        validationEngine.ValidatePage(skip, take);

        return orderAccessor.ListAsync(skip, take, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrderAvailabilityDto> GetAvailabilityAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await GetAsync(orderId, cancellationToken);

        var skus = order.Lines.Select(line => line.Sku).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var stockLevels = await inventoryAccessor.GetStockLevelsAsync(skus, cancellationToken);

        return fulfillmentEngine.CheckAvailability(order, stockLevels);
    }
}
