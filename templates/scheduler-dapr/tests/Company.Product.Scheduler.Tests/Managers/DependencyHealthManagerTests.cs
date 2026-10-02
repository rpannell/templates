using Company.Product.Scheduler.Accessors.Http;
using Company.Product.Scheduler.Managers;
using Company.Product.Scheduler.Managers.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Company.Product.Scheduler.Tests.Managers;

public class DependencyHealthManagerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Returns_the_accessor_result(bool healthy)
    {
        var ordersApi = Substitute.For<IOrdersApiAccessor>();
        var expected = new DependencyHealthDto("orders-api", healthy, "HTTP 200", TimeSpan.FromMilliseconds(12));
        ordersApi.CheckHealthAsync(Arg.Any<CancellationToken>()).Returns(expected);
        var manager = new DependencyHealthManager(ordersApi, NullLogger<DependencyHealthManager>.Instance);

        var result = await manager.CheckOrdersApiAsync(CancellationToken.None);

        Assert.Same(expected, result);
    }
}
