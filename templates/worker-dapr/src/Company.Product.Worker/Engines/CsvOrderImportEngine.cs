using System.Globalization;
using Company.Product.Worker.Managers.Exceptions;
using Company.Product.Worker.Managers.Models;

namespace Company.Product.Worker.Engines;

/// <summary>Reads order import files.</summary>
public interface ICsvOrderImportEngine
{
    /// <summary>
    /// Parses a CSV order import. The first non-empty line is the header
    /// <c>order_ref,customer_name,sku,quantity,unit_price</c>, and each following line is one order line.
    /// Lines with the same <c>order_ref</c> make one order. Numbers use invariant culture (<c>12.50</c>).
    /// Fields are split on commas, so quoted fields containing commas aren't supported.
    /// </summary>
    /// <param name="csv">File content.</param>
    /// <returns>The orders, in the order they first appear in the file.</returns>
    /// <exception cref="BusinessValidationException">The file is empty, has the wrong header, or has invalid lines. Every problem is reported.</exception>
    IReadOnlyList<ImportOrderDto> Parse(string csv);
}

/// <summary>Pure CSV parsing and validation. No I/O.</summary>
public sealed class CsvOrderImportEngine : ICsvOrderImportEngine
{
    /// <summary>Required header columns, in order.</summary>
    public static readonly IReadOnlyList<string> ExpectedHeader = ["order_ref", "customer_name", "sku", "quantity", "unit_price"];

    /// <inheritdoc />
    public IReadOnlyList<ImportOrderDto> Parse(string csv)
    {
        var lines = csv.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var headerIndex = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line));
        if (headerIndex < 0)
        {
            throw Invalid("file", "The file is empty.");
        }

        var header = lines[headerIndex].Split(',').Select(column => column.Trim().ToLowerInvariant()).ToList();
        if (!header.SequenceEqual(ExpectedHeader))
        {
            throw Invalid($"line {headerIndex + 1}", $"Expected the header '{string.Join(",", ExpectedHeader)}'.");
        }

        var errors = new Dictionary<string, List<string>>();
        var orders = new Dictionary<string, PendingOrder>(StringComparer.OrdinalIgnoreCase);
        var references = new List<string>();

        for (var i = headerIndex + 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            var key = $"line {i + 1}";
            var fields = lines[i].Split(',').Select(field => field.Trim()).ToArray();
            if (fields.Length != ExpectedHeader.Count)
            {
                Add(errors, key, $"Expected {ExpectedHeader.Count} fields, found {fields.Length}.");
                continue;
            }

            var (reference, customerName, sku) = (fields[0], fields[1], fields[2]);

            if (reference.Length == 0)
            {
                Add(errors, key, "order_ref is required.");
            }

            if (customerName.Length == 0)
            {
                Add(errors, key, "customer_name is required.");
            }

            if (sku.Length == 0)
            {
                Add(errors, key, "sku is required.");
            }

            if (!int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0)
            {
                Add(errors, key, "quantity must be a whole number greater than zero.");
            }

            if (!decimal.TryParse(fields[4], NumberStyles.Number, CultureInfo.InvariantCulture, out var unitPrice) || unitPrice < 0)
            {
                Add(errors, key, "unit_price must be a number of zero or more.");
            }

            if (errors.ContainsKey(key))
            {
                continue;
            }

            if (!orders.TryGetValue(reference, out var order))
            {
                order = new PendingOrder(customerName);
                orders[reference] = order;
                references.Add(reference);
            }
            else if (!string.Equals(order.CustomerName, customerName, StringComparison.Ordinal))
            {
                Add(errors, key, $"customer_name differs from earlier lines of order '{reference}'.");
                continue;
            }

            order.Lines.Add(new ImportOrderLineDto(sku, quantity, unitPrice));
        }

        if (errors.Count > 0)
        {
            throw new BusinessValidationException(errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        }

        if (references.Count == 0)
        {
            throw Invalid("file", "The file has a header but no order lines.");
        }

        return references
            .Select(reference => new ImportOrderDto(reference, orders[reference].CustomerName, orders[reference].Lines))
            .ToList();
    }

    /// <summary>Records a problem for a line.</summary>
    /// <param name="errors">Errors so far.</param>
    /// <param name="key">Line key, such as <c>line 3</c>.</param>
    /// <param name="message">What is wrong.</param>
    private static void Add(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var messages))
        {
            messages = [];
            errors[key] = messages;
        }

        messages.Add(message);
    }

    /// <summary>Creates the exception for a single problem.</summary>
    /// <param name="key">Where the problem is.</param>
    /// <param name="message">What is wrong.</param>
    /// <returns>The exception to throw.</returns>
    private static BusinessValidationException Invalid(string key, string message) =>
        new(new Dictionary<string, string[]> { [key] = [message] });

    /// <summary>An order being assembled from its lines.</summary>
    /// <param name="customerName">Customer from the order's first line.</param>
    private sealed class PendingOrder(string customerName)
    {
        /// <summary>Customer from the order's first line.</summary>
        public string CustomerName { get; } = customerName;

        /// <summary>Lines so far.</summary>
        public List<ImportOrderLineDto> Lines { get; } = [];
    }
}
