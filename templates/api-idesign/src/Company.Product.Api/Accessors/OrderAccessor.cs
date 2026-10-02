using Company.Product.Api.Accessors.Database;
using Company.Product.Api.Accessors.Exceptions;
using Company.Product.Api.Accessors.Mapping;
using Company.Product.Api.Managers.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Company.Product.Api.Accessors;

/// <summary>Order storage.</summary>
public interface IOrderAccessor
{
    /// <summary>Stores a new order and its lines.</summary>
    /// <param name="order">The priced order to store.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the order is stored.</returns>
    /// <exception cref="ResourceAccessException">The database call failed.</exception>
    Task SaveAsync(OrderDto order, CancellationToken cancellationToken);

    /// <summary>Finds one order with its lines.</summary>
    /// <param name="orderId">Identifier of the order.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The order, or <see langword="null"/> if it does not exist.</returns>
    /// <exception cref="ResourceAccessException">The database call failed.</exception>
    Task<OrderDto?> FindAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Lists orders with their lines, newest first.</summary>
    /// <param name="skip">Number of orders to skip.</param>
    /// <param name="take">Maximum number of orders to return.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One page of orders.</returns>
    /// <exception cref="ResourceAccessException">The database call failed.</exception>
    Task<IReadOnlyList<OrderDto>> ListAsync(int skip, int take, CancellationToken cancellationToken);
}

/// <summary>
/// All order I/O. Takes and returns Dtos; Entities stay inside this namespace. No business logic.
/// </summary>
/// <param name="dbFactory">Creates one short-lived context per call.</param>
public sealed class OrderAccessor(IDbContextFactory<AppDbContext> dbFactory) : IOrderAccessor
{
    /// <inheritdoc />
    public async Task SaveAsync(OrderDto order, CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            db.Orders.Add(order.ToEntity());
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (IsDatabaseFailure(ex))
        {
            throw new ResourceAccessException($"Could not save order '{order.Id}'.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<OrderDto?> FindAsync(Guid orderId, CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            var order = await db.Orders
                .AsNoTracking()
                .Include(o => o.Lines)
                .SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            return order?.ToDto();
        }
        catch (Exception ex) when (IsDatabaseFailure(ex))
        {
            throw new ResourceAccessException($"Could not read order '{orderId}'.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrderDto>> ListAsync(int skip, int take, CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            var orders = await db.Orders
                .AsNoTracking()
                .Include(o => o.Lines)
                .OrderByDescending(o => o.CreatedAt)
                .ThenBy(o => o.Id)
                .Skip(skip)
                .Take(take)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            return orders.Select(o => o.ToDto()).ToList();
        }
        catch (Exception ex) when (IsDatabaseFailure(ex))
        {
            throw new ResourceAccessException("Could not list orders.", ex);
        }
    }

    /// <summary>Infrastructure failures that should surface as <see cref="ResourceAccessException"/>.</summary>
    /// <param name="ex">The caught exception.</param>
    /// <returns><see langword="true"/> if the exception is a database failure.</returns>
    private static bool IsDatabaseFailure(Exception ex) =>
        ex is DbUpdateException or NpgsqlException or TimeoutException;
}
