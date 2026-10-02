using Company.Product.Api.Accessors.Models;
using Microsoft.EntityFrameworkCore;

namespace Company.Product.Api.Accessors.Database;

/// <summary>
/// EF Core mapping over the schema that Liquibase owns. Do not add EF migrations.
/// </summary>
/// <param name="options">Context options, including the connection string.</param>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>The <c>orders</c> table.</summary>
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderEntity>(order =>
        {
            order.ToTable("orders");
            order.HasKey(o => o.Id);

            order.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();
            order.Property(o => o.CustomerName).HasColumnName("customer_name").HasMaxLength(200);
            order.Property(o => o.Subtotal).HasColumnName("subtotal").HasPrecision(18, 2);
            order.Property(o => o.Discount).HasColumnName("discount").HasPrecision(18, 2);
            order.Property(o => o.Total).HasColumnName("total").HasPrecision(18, 2);
            order.Property(o => o.CreatedAt).HasColumnName("created_at");

            order.HasMany(o => o.Lines)
                .WithOne()
                .HasForeignKey(l => l.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderLineEntity>(line =>
        {
            line.ToTable("order_lines");
            line.HasKey(l => new { l.OrderId, l.LineNumber });

            line.Property(l => l.OrderId).HasColumnName("order_id");
            line.Property(l => l.LineNumber).HasColumnName("line_number").ValueGeneratedNever();
            line.Property(l => l.Sku).HasColumnName("sku").HasMaxLength(64);
            line.Property(l => l.Quantity).HasColumnName("quantity");
            line.Property(l => l.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 2);
            line.Property(l => l.LineTotal).HasColumnName("line_total").HasPrecision(18, 2);
        });
    }
}
