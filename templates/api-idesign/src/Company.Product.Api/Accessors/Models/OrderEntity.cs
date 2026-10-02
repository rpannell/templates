namespace Company.Product.Api.Accessors.Models;

/// <summary>Row shape of the <c>orders</c> table. Never leaves the Accessors namespace.</summary>
public sealed class OrderEntity
{
    /// <summary>Column <c>id</c>.</summary>
    public Guid Id { get; set; }

    /// <summary>Column <c>customer_name</c>.</summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>Column <c>subtotal</c>.</summary>
    public decimal Subtotal { get; set; }

    /// <summary>Column <c>discount</c>.</summary>
    public decimal Discount { get; set; }

    /// <summary>Column <c>total</c>.</summary>
    public decimal Total { get; set; }

    /// <summary>Column <c>created_at</c> (timestamptz, UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Rows in <c>order_lines</c> for this order.</summary>
    public List<OrderLineEntity> Lines { get; set; } = [];
}
