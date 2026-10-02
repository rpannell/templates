using Company.Product.Worker.Engines;
using Company.Product.Worker.Managers.Exceptions;

namespace Company.Product.Worker.Tests.Engines;

public class ObjectNotificationEngineTests
{
    private readonly ObjectNotificationEngine _engine = new();

    [Fact]
    public void Parses_amazon_s3_notification_and_unescapes_key()
    {
        const string json = """
            {"Records":[{"eventName":"ObjectCreated:Put","s3":{"bucket":{"name":"imports"},"object":{"key":"daily/orders+2026-01-15.csv","size":120}}}]}
            """;

        var created = Assert.Single(_engine.ParseCreatedObjects(json));

        Assert.Equal("imports", created.Container);
        Assert.Equal("daily/orders 2026-01-15.csv", created.Key);
        Assert.Equal(120, created.Size);
    }

    [Fact]
    public void Parses_minio_notification()
    {
        const string json = """
            {"EventName":"s3:ObjectCreated:Put","Key":"imports/a.csv","Records":[{"eventName":"s3:ObjectCreated:Put","s3":{"bucket":{"name":"imports"},"object":{"key":"a.csv","size":5}}}]}
            """;

        var created = Assert.Single(_engine.ParseCreatedObjects(json));

        Assert.Equal("a.csv", created.Key);
    }

    [Fact]
    public void Ignores_s3_events_that_are_not_object_created()
    {
        const string json = """
            {"Records":[{"eventName":"ObjectRemoved:Delete","s3":{"bucket":{"name":"imports"},"object":{"key":"a.csv"}}}]}
            """;

        Assert.Empty(_engine.ParseCreatedObjects(json));
    }

    [Fact]
    public void Parses_azure_event_grid_array()
    {
        const string json = """
            [{"eventType":"Microsoft.Storage.BlobCreated","subject":"/blobServices/default/containers/imports/blobs/in/a%20b.csv",
              "data":{"url":"https://account.blob.core.windows.net/imports/in/a%20b.csv","contentLength":42}}]
            """;

        var created = Assert.Single(_engine.ParseCreatedObjects(json));

        Assert.Equal("imports", created.Container);
        Assert.Equal("in/a b.csv", created.Key);
        Assert.Equal(42, created.Size);
    }

    [Fact]
    public void Parses_azure_event_grid_cloud_event()
    {
        const string json = """
            {"specversion":"1.0","type":"Microsoft.Storage.BlobCreated","source":"/subscriptions/x","id":"1",
             "data":{"url":"https://account.blob.core.windows.net/imports/a.csv"}}
            """;

        var created = Assert.Single(_engine.ParseCreatedObjects(json));

        Assert.Equal("a.csv", created.Key);
        Assert.Null(created.Size);
    }

    [Fact]
    public void Ignores_other_event_grid_event_types()
    {
        const string json = """
            [{"eventType":"Microsoft.Storage.BlobDeleted","data":{"url":"https://account.blob.core.windows.net/imports/a.csv"}}]
            """;

        Assert.Empty(_engine.ParseCreatedObjects(json));
    }

    [Fact]
    public void Parses_google_cloud_storage_notification()
    {
        const string json = """
            {"kind":"storage#object","bucket":"imports","name":"in/a.csv","size":"77"}
            """;

        var created = Assert.Single(_engine.ParseCreatedObjects(json));

        Assert.Equal("imports", created.Container);
        Assert.Equal("in/a.csv", created.Key);
        Assert.Equal(77, created.Size);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"hello":"world"}""")]
    [InlineData("""{"Records":[{"eventName":"ObjectCreated:Put"}]}""")]
    public void Rejects_unusable_notifications(string json)
    {
        Assert.Throws<BusinessValidationException>(() => _engine.ParseCreatedObjects(json));
    }
}
