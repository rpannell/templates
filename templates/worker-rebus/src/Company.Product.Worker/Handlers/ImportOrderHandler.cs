using Company.Product.Worker.Handlers.Mapping;
using Company.Product.Worker.Handlers.Models;
using Company.Product.Worker.Managers;
using Rebus.Handlers;

namespace Company.Product.Worker.Handlers;

/// <summary>COMMAND handler: creates the order in <see cref="ImportOrderCommand"/> through the orders API.</summary>
/// <param name="manager">Order import use cases.</param>
public sealed class ImportOrderHandler(IOrderImportManager manager) : IHandleMessages<ImportOrderCommand>
{
    /// <summary>
    /// Handles one command. A transient failure (API down) propagates, so Rebus redelivers it. An invalid order throws
    /// <c>BusinessValidationException</c>, which fails fast to the error queue without further attempts.
    /// </summary>
    /// <param name="message">The command.</param>
    /// <returns>A task that completes when the order is created.</returns>
    public Task Handle(ImportOrderCommand message) => manager.ImportAsync(message.ToDto(), CancellationToken.None);
}
