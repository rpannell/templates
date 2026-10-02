namespace Company.Product.Api.Accessors.Models;

/// <summary>Row shape of the <c>order_lines</c> table. Never leaves the Accessors namespace.</summary>
public sealed class OrderLineEntity
{
    /// <summary>Column <c>order_id</c>. Part of the primary key.</summary>
    public Guid OrderId { get; set; }

    /// <summary>Column <c>line_number</c>. Part of the primary key.</summary>
    public int LineNumber { get; set; }

    /// <summary>Column <c>sku</c>.</summary>
    public string Sku { get; set; } = string.Empty;

    /// <summary>Column <c>quantity</c>.</summary>
    public int Quantity { get; set; }

    /// <summary>Column <c>unit_price</c>.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Column <c>line_total</c>.</summary>
    public decimal LineTotal { get; set; }
}
