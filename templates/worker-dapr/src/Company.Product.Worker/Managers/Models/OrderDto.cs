namespace Company.Product.Worker.Managers.Models;

/// <summary>An order as the worker needs it: a summary of what the orders API returns.</summary>
/// <param name="Id">Order identifier.</param>
/// <param name="CustomerName">Customer who placed the order.</param>
/// <param name="LineCount">Number of lines on the order.</param>
/// <param name="Total">Amount due.</param>
/// <param name="CreatedAt">When the order was created (UTC).</param>
public sealed record OrderDto(Guid Id, string CustomerName, int LineCount, decimal Total, DateTimeOffset CreatedAt);
