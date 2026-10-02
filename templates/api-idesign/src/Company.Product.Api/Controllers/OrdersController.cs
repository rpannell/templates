using Company.Product.Api.Controllers.Mapping;
using Company.Product.Api.Managers;
using Company.Product.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Company.Product.Api.Controllers;

/// <summary>
/// Create and read orders.
/// </summary>
/// <remarks>
/// Client layer. Maps API models to Dtos and delegates each action to one Manager call. No business logic, no I/O.
/// </remarks>
/// <param name="orderManager">Order use cases.</param>
[ApiController]
[Route("api/orders")]
public sealed class OrdersController(IOrderManager orderManager) : ControllerBase
{
    /// <summary>Creates an order.</summary>
    /// <remarks>
    /// Validates the order, prices every line and applies the volume discount when the subtotal
    /// reaches the configured threshold.
    /// </remarks>
    /// <param name="request">The customer and the lines to order.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The created order, priced.</returns>
    /// <response code="201">The order was created. The <c>Location</c> header points to it.</response>
    /// <response code="400">The request breaks one or more business rules.</response>
    /// <response code="503">The database is unavailable.</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<Order>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<Order>> Create(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var created = await orderManager.CreateAsync(request.ToDto(), cancellationToken);
        var order = created.ToModel();

        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    /// <summary>Gets one order.</summary>
    /// <param name="id">Identifier of the order.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The order.</returns>
    /// <response code="200">The order.</response>
    /// <response code="404">No order has that identifier.</response>
    /// <response code="503">The database is unavailable.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<Order>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<Order>> Get(Guid id, CancellationToken cancellationToken)
    {
        var order = await orderManager.GetAsync(id, cancellationToken);

        return order.ToModel();
    }

    /// <summary>Checks whether current stock can fulfill an order.</summary>
    /// <remarks>
    /// Asks the inventory service (found through service discovery) for stock levels. Lines with the same SKU
    /// share stock in line order.
    /// </remarks>
    /// <param name="id">Identifier of the order.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Per-line availability and whether the whole order can be fulfilled.</returns>
    /// <response code="200">Availability for every line.</response>
    /// <response code="404">No order has that identifier.</response>
    /// <response code="503">The database or the inventory service is unavailable.</response>
    [HttpGet("{id:guid}/availability")]
    [ProducesResponseType<OrderAvailability>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OrderAvailability>> GetAvailability(Guid id, CancellationToken cancellationToken)
    {
        var availability = await orderManager.GetAvailabilityAsync(id, cancellationToken);

        return availability.ToModel();
    }

    /// <summary>Lists orders, newest first.</summary>
    /// <param name="skip">Number of orders to skip. Defaults to 0.</param>
    /// <param name="take">Page size, 1 to 200. Defaults to 50.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>One page of orders.</returns>
    /// <response code="200">One page of orders. Empty when there are no more.</response>
    /// <response code="400">The paging window is out of range.</response>
    /// <response code="503">The database is unavailable.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<Order>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<Order>>> List(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var orders = await orderManager.ListAsync(skip, take, cancellationToken);

        return Ok(orders.Select(o => o.ToModel()).ToList());
    }
}
