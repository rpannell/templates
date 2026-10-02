using Company.Product.Worker.Accessors.Exceptions;
using Company.Product.Worker.Handlers.Setup;
using Company.Product.Worker.Managers.Exceptions;
using NSubstitute;
using Rebus.Retry.FailFast;

namespace Company.Product.Worker.Tests.Handlers;

public class PermanentFailureFailFastCheckerTests
{
    private readonly IFailFastChecker _inner = Substitute.For<IFailFastChecker>();
    private readonly PermanentFailureFailFastChecker _checker;

    public PermanentFailureFailFastCheckerTests()
    {
        _checker = new PermanentFailureFailFastChecker(_inner);
    }

    private static BusinessValidationException Invalid() =>
        new(new Dictionary<string, string[]> { ["field"] = ["bad"] });

    [Fact]
    public void Business_validation_failure_fails_fast()
    {
        Assert.True(_checker.ShouldFailFast("id", Invalid()));
    }

    [Fact]
    public void Business_validation_failure_is_found_inside_wrappers()
    {
        Assert.True(_checker.ShouldFailFast("id", new AggregateException(Invalid())));
        Assert.True(_checker.ShouldFailFast("id", new InvalidOperationException("wrapper", Invalid())));
    }

    [Fact]
    public void Transient_failure_is_retried()
    {
        var transient = new ResourceAccessException("api down", new HttpRequestException());

        Assert.False(_checker.ShouldFailFast("id", transient));
    }

    [Fact]
    public void Defaults_of_the_wrapped_checker_still_apply()
    {
        var exception = new InvalidOperationException("whatever");
        _inner.ShouldFailFast("id", exception).Returns(true);

        Assert.True(_checker.ShouldFailFast("id", exception));
    }
}
