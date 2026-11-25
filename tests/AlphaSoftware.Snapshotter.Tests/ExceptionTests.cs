public class ExceptionTests
{
    [Fact]
    public Task ActionThrows() =>
        Throws(() => throw new InvalidOperationException("outer"))
            .IgnoreStackTrace();

    [Fact]
    public Task FuncThrows() =>
        Throws(() => int.Parse("x"))
            .IgnoreStackTrace()
            .IgnoreMember("Message");

    [Fact]
    public Task TaskThrows() =>
        ThrowsTask(async () =>
            {
                await Task.Yield();
                throw new ArgumentException("bad argument", "theParam");
            })
            .IgnoreStackTrace();

    [Fact]
    public Task ValueTaskThrows() =>
        ThrowsValueTask(() => throw new NotSupportedException("value task"))
            .IgnoreStackTrace();

    [Fact]
    public Task InnerExceptionAndData()
    {
        var exception = new InvalidOperationException("outer", new ArgumentNullException("p", "inner msg"))
        {
            HelpLink = "http://help"
        };
        exception.Data["key"] = "val";
        return Verify(exception);
    }

    [Fact]
    public Task CustomProperties() =>
        Verify(new OrderException("Order failed", 42));

    [Fact]
    public Task StackTrace() =>
        Throws(Thrower.Throw)
            .UniqueForRuntime();

    [Fact]
    public Task Aggregate() =>
        Verify(new AggregateException(new Exception("first"), new Exception("second")))
            .IgnoreMember("Message");

    [Fact]
    public async Task DidNotThrow()
    {
        var exception = await Assert.ThrowsAsync<Exception>(async () => await Throws(() => { }));
        Assert.Equal("Did not throw.", exception.Message);
    }

    public class OrderException(string message, int orderId) : Exception(message)
    {
        public int OrderId { get; } = orderId;
    }

    static class Thrower
    {
        public static void Throw() => throw new InvalidOperationException("with a stack trace");
    }
}
