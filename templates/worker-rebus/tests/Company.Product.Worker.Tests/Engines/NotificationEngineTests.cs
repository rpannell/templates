using Company.Product.Worker.Engines;
using Company.Product.Worker.Managers.Models;

namespace Company.Product.Worker.Tests.Engines;

public class NotificationEngineTests
{
    [Fact]
    public void Order_placed_message_names_the_order_and_formats_the_total_with_two_decimals()
    {
        var order = new OrderDto(Guid.Parse("0192f0a0-0000-7000-8000-000000000001"), "Contoso", 2, 522.4m, DateTimeOffset.UnixEpoch);

        var notification = new NotificationEngine().OrderPlaced(order);

        Assert.Equal("Order 0192f0a0-0000-7000-8000-000000000001 placed", notification.Subject);
        Assert.Equal("Contoso placed an order with 2 line(s) totaling 522.40.", notification.Body);
    }
}
