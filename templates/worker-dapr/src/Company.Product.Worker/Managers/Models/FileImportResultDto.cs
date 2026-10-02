namespace Company.Product.Worker.Managers.Models;

/// <summary>Outcome of importing one file.</summary>
/// <param name="FileName">The file or object key.</param>
/// <param name="OrdersCreated">Orders created through the orders API.</param>
public sealed record FileImportResultDto(string FileName, int OrdersCreated);
