namespace Company.Product.Api.Managers.Models;

/// <summary>Internal representation of an order to create, before validation and pricing.</summary>
/// <param name="CustomerName">Name of the customer placing the order.</param>
/// <param name="Lines">Requested order lines.</param>
public sealed record CreateOrderDto(
    string CustomerName,
    IReadOnlyList<CreateOrderLineDto> Lines);

/// <summary>A requested order line, before pricing.</summary>
/// <param name="Sku">Stock keeping unit of the product.</param>
/// <param name="Quantity">Number of units.</param>
/// <param name="UnitPrice">Price per unit.</param>
public sealed record CreateOrderLineDto(
    string Sku,
    int Quantity,
    decimal UnitPrice);
