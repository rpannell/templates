using Company.Product.Worker.Engines;
using Company.Product.Worker.Managers.Exceptions;

namespace Company.Product.Worker.Tests.Engines;

public class CsvOrderImportEngineTests
{
    private const string Header = "order_ref,customer_name,sku,quantity,unit_price";

    private readonly CsvOrderImportEngine _engine = new();

    [Fact]
    public void Groups_lines_by_order_reference_in_file_order()
    {
        var csv = $"""
            {Header}
            A-1,Contoso,WIDGET-1,10,25.00
            B-7,Fabrikam,GADGET-2,1,99.99
            A-1,Contoso,GADGET-2,3,99.99
            """;

        var orders = _engine.Parse(csv);

        Assert.Equal(new[] { "A-1", "B-7" }, orders.Select(o => o.Reference));
        Assert.Equal(2, orders[0].Lines.Count);
        Assert.Equal(10, orders[0].Lines[0].Quantity);
        Assert.Equal(99.99m, orders[1].Lines[0].UnitPrice);
    }

    [Fact]
    public void Accepts_windows_line_endings_and_blank_lines()
    {
        var csv = "\r\n" + Header + "\r\nA-1,Contoso,WIDGET-1,1,1.50\r\n\r\n";

        var order = Assert.Single(_engine.Parse(csv));

        Assert.Equal(1.50m, order.Lines[0].UnitPrice);
    }

    [Fact]
    public void Rejects_wrong_header()
    {
        var exception = Assert.Throws<BusinessValidationException>(() => _engine.Parse("ref,customer\nA,B"));

        Assert.Contains("line 1", exception.Errors.Keys);
    }

    [Fact]
    public void Reports_every_bad_line_with_its_line_number()
    {
        var csv = $"""
            {Header}
            A-1,Contoso,WIDGET-1,0,25.00
            A-2,,WIDGET-1,1,abc
            A-3,Contoso,WIDGET-1,1
            """;

        var exception = Assert.Throws<BusinessValidationException>(() => _engine.Parse(csv));

        Assert.Equal(new[] { "line 2", "line 3", "line 4" }, exception.Errors.Keys.Order());
        Assert.Equal(2, exception.Errors["line 3"].Length);
    }

    [Fact]
    public void Rejects_customer_change_within_an_order()
    {
        var csv = $"""
            {Header}
            A-1,Contoso,WIDGET-1,1,1
            A-1,Fabrikam,WIDGET-2,1,1
            """;

        var exception = Assert.Throws<BusinessValidationException>(() => _engine.Parse(csv));

        Assert.Contains("line 3", exception.Errors.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData(Header)]
    public void Rejects_files_without_order_lines(string csv)
    {
        Assert.Throws<BusinessValidationException>(() => _engine.Parse(csv));
    }
}
