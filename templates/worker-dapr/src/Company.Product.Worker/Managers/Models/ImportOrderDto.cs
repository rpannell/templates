namespace Company.Product.Worker.Managers.Models;

/// <summary>An order read from an import file, ready to send to the orders API.</summary>
/// <param name="Reference">The file's own order reference (<c>order_ref</c>), used to group lines and in logs.</param>
/// <param name="CustomerName">Customer placing the order.</param>
/// <param name="Lines">Order lines, in file order.</param>
public sealed record ImportOrderDto(string Reference, string CustomerName, IReadOnlyList<ImportOrderLineDto> Lines);

/// <summary>A line of an imported order.</summary>
/// <param name="Sku">Stock keeping unit.</param>
/// <param name="Quantity">Units ordered.</param>
/// <param name="UnitPrice">Price per unit.</param>
public sealed record ImportOrderLineDto(string Sku, int Quantity, decimal UnitPrice);
