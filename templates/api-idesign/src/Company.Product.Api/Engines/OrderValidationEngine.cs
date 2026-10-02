using Company.Product.Api.Managers.Exceptions;
using Company.Product.Api.Managers.Models;

namespace Company.Product.Api.Engines;

/// <summary>Business rules for orders.</summary>
public interface IOrderValidationEngine
{
    /// <summary>Checks an order against the business rules.</summary>
    /// <param name="order">The order to check.</param>
    /// <exception cref="BusinessValidationException">The order breaks one or more rules. Every failure is reported, not just the first.</exception>
    void Validate(CreateOrderDto order);

    /// <summary>Checks a paging window.</summary>
    /// <param name="skip">Number of items to skip. Cannot be negative.</param>
    /// <param name="take">Page size. Must be between 1 and <see cref="OrderValidationEngine.MaxPageSize"/>.</param>
    /// <exception cref="BusinessValidationException">The window is out of range.</exception>
    void ValidatePage(int skip, int take);
}

/// <summary>Pure business rules for orders. No I/O.</summary>
public sealed class OrderValidationEngine : IOrderValidationEngine
{
    /// <summary>Maximum number of lines on one order.</summary>
    public const int MaxLines = 100;

    /// <summary>Maximum page size for list operations.</summary>
    public const int MaxPageSize = 200;

    /// <inheritdoc />
    public void Validate(CreateOrderDto order)
    {
        var errors = new ValidationErrors();

        if (string.IsNullOrWhiteSpace(order.CustomerName))
        {
            errors.Add(nameof(order.CustomerName), "Customer name is required.");
        }

        if (order.Lines is null || order.Lines.Count == 0)
        {
            errors.Add(nameof(order.Lines), "An order needs at least one line.");
        }
        else
        {
            if (order.Lines.Count > MaxLines)
            {
                errors.Add(nameof(order.Lines), $"An order can have at most {MaxLines} lines.");
            }

            for (var i = 0; i < order.Lines.Count; i++)
            {
                var line = order.Lines[i];
                var prefix = $"{nameof(order.Lines)}[{i}]";

                if (string.IsNullOrWhiteSpace(line.Sku))
                {
                    errors.Add($"{prefix}.{nameof(line.Sku)}", "SKU is required.");
                }

                if (line.Quantity <= 0)
                {
                    errors.Add($"{prefix}.{nameof(line.Quantity)}", "Quantity must be greater than zero.");
                }

                if (line.UnitPrice < 0)
                {
                    errors.Add($"{prefix}.{nameof(line.UnitPrice)}", "Unit price cannot be negative.");
                }
            }
        }

        errors.ThrowIfAny();
    }

    /// <inheritdoc />
    public void ValidatePage(int skip, int take)
    {
        var errors = new ValidationErrors();

        if (skip < 0)
        {
            errors.Add(nameof(skip), "Skip cannot be negative.");
        }

        if (take is < 1 or > MaxPageSize)
        {
            errors.Add(nameof(take), $"Take must be between 1 and {MaxPageSize}.");
        }

        errors.ThrowIfAny();
    }

    /// <summary>Collects every rule failure so the caller sees them all at once.</summary>
    private sealed class ValidationErrors
    {
        private readonly Dictionary<string, List<string>> _errors = [];

        /// <summary>Records a failure for a field.</summary>
        /// <param name="key">Field name, using request-body paths such as <c>Lines[0].Sku</c>.</param>
        /// <param name="message">What is wrong.</param>
        public void Add(string key, string message)
        {
            if (!_errors.TryGetValue(key, out var messages))
            {
                messages = [];
                _errors[key] = messages;
            }

            messages.Add(message);
        }

        /// <summary>Throws <see cref="BusinessValidationException"/> if any failures were recorded.</summary>
        public void ThrowIfAny()
        {
            if (_errors.Count > 0)
            {
                throw new BusinessValidationException(
                    _errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
            }
        }
    }
}
