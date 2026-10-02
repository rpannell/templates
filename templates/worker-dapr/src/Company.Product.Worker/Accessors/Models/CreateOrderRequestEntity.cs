namespace Company.Product.Worker.Accessors.Models;

/// <summary>Request body for the orders API's <c>POST api/orders</c>. Never leaves the Accessors namespace.</summary>
/// <param name="CustomerName">Customer name.</param>
/// <param name="Lines">Order lines.</param>
public sealed record CreateOrderRequestEntity(string CustomerName, IReadOnlyList<CreateOrderLineRequestEntity> Lines);

/// <summary>A line in a create-order request.</summary>
/// <param name="Sku">Stock keeping unit.</param>
/// <param name="Quantity">Units.</param>
/// <param name="UnitPrice">Price per unit.</param>
public sealed record CreateOrderLineRequestEntity(string Sku, int Quantity, decimal UnitPrice);
