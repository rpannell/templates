using Company.Product.Worker.Accessors.Exceptions;
using Company.Product.Worker.Accessors.Files;
using Company.Product.Worker.Accessors.Http;
using Company.Product.Worker.Accessors.Storage;
using Company.Product.Worker.Engines;
using Company.Product.Worker.Managers;
using Company.Product.Worker.Managers.Exceptions;
using Company.Product.Worker.Managers.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Company.Product.Worker.Tests.Managers;

public class FileImportManagerTests
{
    private const string Csv = "order_ref,customer_name,sku,quantity,unit_price\nA-1,Contoso,WIDGET-1,2,5.00\nB-2,Fabrikam,GADGET-2,1,9.99";

    private readonly IObjectStorageAccessor _objectStorage = Substitute.For<IObjectStorageAccessor>();
    private readonly IDropFolderAccessor _dropFolder = Substitute.For<IDropFolderAccessor>();
    private readonly IOrdersApiAccessor _ordersApi = Substitute.For<IOrdersApiAccessor>();
    private readonly FileImportManager _manager;

    public FileImportManagerTests()
    {
        // Real engines: they're pure, so the tests cover the whole use case except I/O.
        _manager = new FileImportManager(
            new ObjectNotificationEngine(),
            new CsvOrderImportEngine(),
            _objectStorage,
            _dropFolder,
            _ordersApi,
            NullLogger<FileImportManager>.Instance);

        _ordersApi.CreateAsync(Arg.Any<ImportOrderDto>(), Arg.Any<CancellationToken>())
            .Returns(call => new OrderDto(Guid.NewGuid(), call.Arg<ImportOrderDto>().CustomerName, 1, 10m, DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public async Task Notification_reads_each_object_and_creates_its_orders()
    {
        const string notification = """
            {"Records":[{"eventName":"ObjectCreated:Put","s3":{"bucket":{"name":"imports"},"object":{"key":"orders.csv"}}}]}
            """;
        _objectStorage.ReadTextAsync("orders.csv", Arg.Any<CancellationToken>()).Returns(Csv);

        var results = await _manager.ImportFromNotificationAsync(notification, CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Equal(2, result.OrdersCreated);
        await _ordersApi.Received(2).CreateAsync(Arg.Any<ImportOrderDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Drop_folder_returns_false_when_nothing_is_ready()
    {
        _dropFolder.ClaimNextAsync(Arg.Any<CancellationToken>()).Returns((DropFileDto?)null);

        Assert.False(await _manager.ImportNextDropFileAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Valid_drop_file_is_imported_and_completed()
    {
        var file = new DropFileDto("orders.csv", "/drop/processing/orders.csv", Csv);
        _dropFolder.ClaimNextAsync(Arg.Any<CancellationToken>()).Returns(file);

        Assert.True(await _manager.ImportNextDropFileAsync(CancellationToken.None));

        await _dropFolder.Received(1).CompleteAsync(file, Arg.Any<CancellationToken>());
        await _ordersApi.Received(2).CreateAsync(Arg.Any<ImportOrderDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invalid_drop_file_goes_to_failed_without_calling_the_api()
    {
        var file = new DropFileDto("bad.csv", "/drop/processing/bad.csv", "not,a,valid,header");
        _dropFolder.ClaimNextAsync(Arg.Any<CancellationToken>()).Returns(file);

        Assert.True(await _manager.ImportNextDropFileAsync(CancellationToken.None));

        await _dropFolder.Received(1).FailAsync(file, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _ordersApi.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public async Task Transient_failure_releases_the_file_and_rethrows()
    {
        var file = new DropFileDto("orders.csv", "/drop/processing/orders.csv", Csv);
        _dropFolder.ClaimNextAsync(Arg.Any<CancellationToken>()).Returns(file);
        _ordersApi.CreateAsync(Arg.Any<ImportOrderDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ResourceAccessException("API down", new HttpRequestException()));

        await Assert.ThrowsAsync<ResourceAccessException>(() => _manager.ImportNextDropFileAsync(CancellationToken.None));

        await _dropFolder.Received(1).ReleaseAsync(file, Arg.Any<CancellationToken>());
        await _dropFolder.DidNotReceiveWithAnyArgs().FailAsync(default!, default!, default);
    }

    [Fact]
    public async Task Unknown_notification_format_is_rejected()
    {
        await Assert.ThrowsAsync<BusinessValidationException>(
            () => _manager.ImportFromNotificationAsync("""{"unexpected":true}""", CancellationToken.None));
    }
}
