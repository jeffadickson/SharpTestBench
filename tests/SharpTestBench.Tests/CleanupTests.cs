using SharpTestBench;
using Xunit;

public sealed class CleanupUnitTests
{
    [Fact]
    public async Task ReverseOrderAndIdempotence()
    {
        var order = new List<int>();
        var scope = new CleanupScope();
        scope.Defer(() => order.Add(1));
        var nested = scope.CreateScope();
        nested.Defer(() => order.Add(2));
        scope.Defer(() => order.Add(3));
        var close = scope.CloseAsync(Xunit.TestContext.Current.CancellationToken);
        Assert.Same(close, scope.CloseAsync(Xunit.TestContext.Current.CancellationToken));
        await close;
        Assert.Equal(new[] { 3, 2, 1 }, order);
        Assert.Throws<InvalidOperationException>(() => scope.Defer(() => { }));
    }

    [Fact]
    public async Task AllFailuresPreservedAndRemainingCallbacksAttempted()
    {
        var scope = new CleanupScope();
        int calls = 0;
        scope.Defer(() => calls++);
        scope.Defer(() => throw new FormatException("first registered"));
        scope.Defer(() => throw new InvalidOperationException("last registered"));
        var error = await Assert.ThrowsAsync<AggregateException>(() => scope.CloseAsync(Xunit.TestContext.Current.CancellationToken));
        Assert.Equal(1, calls);
        Assert.IsType<InvalidOperationException>(error.InnerExceptions[0]);
        Assert.IsType<FormatException>(error.InnerExceptions[1]);
    }

    [Fact]
    public async Task CancellationStillAttemptsEveryCallback()
    {
        var scope = new CleanupScope();
        int calls = 0;
        scope.Defer(() => calls++);
        scope.Defer(token => Task.FromCanceled(token));
        await Assert.ThrowsAsync<AggregateException>(() => scope.CloseAsync(new CancellationToken(true)));
        Assert.Equal(1, calls);
    }
}
