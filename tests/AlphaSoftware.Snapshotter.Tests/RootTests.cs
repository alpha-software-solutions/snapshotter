// Simple values at the root are written as text, without the serializer, and are not scrubbed.
public class RootTests
{
    [Fact]
    public Task Integer() => Verify(5);

    [Fact]
    public Task Double() => Verify(2d);

    [Fact]
    public Task Decimal() => Verify(1.1m);

    [Fact]
    public Task Boolean() => Verify(true);

    [Fact]
    public Task Null() => Verify((object?)null);

    [Fact]
    public Task EmptyString() => Verify("");

    [Fact]
    public Task StringWithNewlines() => Verify("hello\r\nworld\rnext");

    [Fact]
    public Task StringWithTrailingNewline() => Verify("hello\n");

    [Fact]
    public Task Guid() => Verify(new Guid("ebced679-45d3-4653-8791-3d969c4a986c"));

    [Fact]
    public Task DateTime() => Verify(new DateTime(2020, 1, 2, 3, 4, 5));

    [Fact]
    public Task DateTimeOffset() => Verify(new DateTimeOffset(2020, 10, 1, 10, 15, 30, TimeSpan.Zero));

    [Fact]
    public Task Enum() => Verify(DayOfWeek.Friday);

    [Fact]
    public Task TaskResult() => Verify(Task.FromResult("from a task"));

    [Fact]
    public Task Text() => Verify("<p>Hello</p>", "html");

    [Fact]
    public async Task TextForABinaryExtensionThrows()
    {
        var exception = await Assert.ThrowsAsync<Exception>(async () => await Verify("text", "png"));
        Assert.StartsWith("Don't pass text for a binary extension.", exception.Message);
    }

    [Fact]
    public async Task Result()
    {
        var result = await Verify(new { Value = 1 });
        Assert.Equal("{\n  Value: 1\n}", result.Text);
    }
}
