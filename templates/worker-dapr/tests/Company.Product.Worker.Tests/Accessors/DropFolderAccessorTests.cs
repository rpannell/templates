using Company.Product.Worker.Accessors.Files;
using Company.Product.Worker.Utilities.Options;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Company.Product.Worker.Tests.Accessors;

/// <summary>Runs against a real temporary folder.</summary>
public sealed class DropFolderAccessorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "drop-tests-" + Guid.NewGuid().ToString("N"));
    private readonly TimeProvider _time = Substitute.For<TimeProvider>();
    private readonly DropFolderAccessor _accessor;

    public DropFolderAccessorTests()
    {
        var options = Substitute.For<IOptionsMonitor<DropFolderOptions>>();
        options.CurrentValue.Returns(new DropFolderOptions { Enabled = true, Path = _root, MinimumFileAge = TimeSpan.FromSeconds(5) });

        // "Now" is an hour ahead, so freshly written test files count as old enough.
        _time.GetUtcNow().Returns(DateTimeOffset.UtcNow.AddHours(1));
        _accessor = new DropFolderAccessor(options, _time);
    }

    [Fact]
    public async Task Claims_file_by_moving_it_to_processing()
    {
        Drop("orders.csv", "content");

        var file = await _accessor.ClaimNextAsync(CancellationToken.None);

        Assert.NotNull(file);
        Assert.Equal("content", file.Content);
        Assert.True(File.Exists(Path.Combine(_root, "processing", "orders.csv")));
        Assert.False(File.Exists(Path.Combine(_root, "incoming", "orders.csv")));
    }

    [Fact]
    public async Task Skips_temporary_and_too_new_files()
    {
        Drop("upload.csv.tmp", "partial");
        _time.GetUtcNow().Returns(DateTimeOffset.UtcNow);   // files are now younger than MinimumFileAge
        Drop("fresh.csv", "just written");

        Assert.Null(await _accessor.ClaimNextAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Complete_fail_and_release_move_the_file()
    {
        Drop("a.csv", "a");
        Drop("b.csv", "b");
        Drop("c.csv", "c");

        var a = (await _accessor.ClaimNextAsync(CancellationToken.None))!;
        var b = (await _accessor.ClaimNextAsync(CancellationToken.None))!;
        var c = (await _accessor.ClaimNextAsync(CancellationToken.None))!;

        await _accessor.CompleteAsync(a, CancellationToken.None);
        await _accessor.FailAsync(b, "bad header", CancellationToken.None);
        await _accessor.ReleaseAsync(c, CancellationToken.None);

        Assert.Single(Directory.GetFiles(Path.Combine(_root, "processed"), "*-a.csv"));
        Assert.Single(Directory.GetFiles(Path.Combine(_root, "failed"), "*-b.csv"));
        Assert.Equal("bad header", File.ReadAllText(Directory.GetFiles(Path.Combine(_root, "failed"), "*.error.txt").Single()));
        Assert.True(File.Exists(Path.Combine(_root, "incoming", "c.csv")));
        Assert.Empty(Directory.GetFiles(Path.Combine(_root, "processing")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void Drop(string name, string content)
    {
        var incoming = Directory.CreateDirectory(Path.Combine(_root, "incoming")).FullName;
        File.WriteAllText(Path.Combine(incoming, name), content);
    }
}
