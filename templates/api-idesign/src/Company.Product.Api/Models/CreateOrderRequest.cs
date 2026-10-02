namespace Company.Product.Api.Models;

/// <summary>Request body for creating an order.</summary>
/// <param name="CustomerName">Name of the customer placing the order.</param>
/// <param name="Lines">At least one line, at most 100.</param>
public sealed record CreateOrderRequest(
    string CustomerName,
    IReadOnlyList<CreateOrderLineRequest> Lines);

/// <summary>A line in a create-order request.</summary>
/// <param name="Sku">Stock keeping unit of the product.</param>
/// <param name="Quantity">Number of units. Must be greater than zero.</param>
/// <param name="UnitPrice">Price per unit. Cannot be negative.</param>
public sealed record CreateOrderLineRequest(
    string Sku,
    int Quantity,
    decimal UnitPrice);
