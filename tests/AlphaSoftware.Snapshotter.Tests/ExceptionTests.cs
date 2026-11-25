public class ExceptionTests
{
    public class OrderRejectedException(string message, int orderId) : Exception(message)
    {
        public int OrderId { get; } = orderId;
    }

    [Fact]
    public Task Throws() =>
        Verifier.Throws(() => throw new InvalidOperationException("boom"));

    [Fact]
    public Task ThrowsAsync() =>
        Verifier.ThrowsAsync(async () =>
        {
            await Task.Yield();
            throw new ArgumentException("bad argument", "name");
        });

    [Fact]
    public Task CustomProperties() =>
        Verifier.Throws(() => throw new OrderRejectedException("rejected", 42));

    [Fact]
    public Task InnerAndData() =>
        Verifier.Throws(() =>
        {
            var exception = new InvalidOperationException("outer", new FormatException("inner"));
            exception.Data["attempt"] = 3;
            throw exception;
        });

    [Fact]
    public Task Aggregate() =>
        Verifier.Throws(() => throw new AggregateException(new TimeoutException("first"), new ArgumentNullException("second")));

    [Fact]
    public async Task NothingThrown()
    {
        var exception = await Assert.ThrowsAsync<SnapshotterException>(() => Verifier.Throws(() => { }).ToTask());
        Assert.Contains("expected the action to throw", exception.Message);
    }
}
