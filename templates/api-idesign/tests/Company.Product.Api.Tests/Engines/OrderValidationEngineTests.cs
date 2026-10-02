using Company.Product.Api.Engines;
using Company.Product.Api.Managers.Exceptions;
using Company.Product.Api.Managers.Models;

namespace Company.Product.Api.Tests.Engines;

public class OrderValidationEngineTests
{
    private readonly OrderValidationEngine _engine = new();

    [Fact]
    public void Validate_accepts_valid_order()
    {
        var order = new CreateOrderDto("Contoso", [new CreateOrderLineDto("A-1", 1, 10m)]);

        var exception = Record.Exception(() => _engine.Validate(order));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_requires_customer_name()
    {
        var order = new CreateOrderDto(" ", [new CreateOrderLineDto("A-1", 1, 10m)]);

        var exception = Assert.Throws<BusinessValidationException>(() => _engine.Validate(order));

        Assert.Contains("CustomerName", exception.Errors.Keys);
    }

    [Fact]
    public void Validate_requires_at_least_one_line()
    {
        var order = new CreateOrderDto("Contoso", []);

        var exception = Assert.Throws<BusinessValidationException>(() => _engine.Validate(order));

        Assert.Contains("Lines", exception.Errors.Keys);
    }

    [Theory]
    [InlineData("", 1, 10, "Lines[0].Sku")]
    [InlineData("A-1", 0, 10, "Lines[0].Quantity")]
    [InlineData("A-1", 1, -1, "Lines[0].UnitPrice")]
    public void Validate_rejects_bad_line(string sku, int quantity, int unitPrice, string expectedKey)
    {
        var order = new CreateOrderDto("Contoso", [new CreateOrderLineDto(sku, quantity, unitPrice)]);

        var exception = Assert.Throws<BusinessValidationException>(() => _engine.Validate(order));

        Assert.Contains(expectedKey, exception.Errors.Keys);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, OrderValidationEngine.MaxPageSize + 1)]
    public void ValidatePage_rejects_out_of_range(int skip, int take)
    {
        Assert.Throws<BusinessValidationException>(() => _engine.ValidatePage(skip, take));
    }

    [Fact]
    public void ValidatePage_accepts_valid_window()
    {
        var exception = Record.Exception(() => _engine.ValidatePage(0, OrderValidationEngine.MaxPageSize));

        Assert.Null(exception);
    }
}
