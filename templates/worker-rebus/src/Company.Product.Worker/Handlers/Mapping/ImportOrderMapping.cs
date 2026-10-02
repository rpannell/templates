using Company.Product.Worker.Handlers.Models;
using Company.Product.Worker.Managers.Models;

namespace Company.Product.Worker.Handlers.Mapping;

/// <summary>Message contract &lt;-&gt; Manager Dto. Contracts never go past the Handlers namespace.</summary>
internal static class ImportOrderMapping
{
    /// <summary>Maps the command to the Dto the Manager works with.</summary>
    /// <param name="command">The received command.</param>
    /// <returns>The order Dto.</returns>
    public static ImportOrderDto ToDto(this ImportOrderCommand command) => new(
        command.Reference,
        command.CustomerName,
        command.Lines.Select(line => new ImportOrderLineDto(line.Sku, line.Quantity, line.UnitPrice)).ToList());
}
