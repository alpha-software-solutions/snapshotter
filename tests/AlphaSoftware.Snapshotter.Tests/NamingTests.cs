// Every snapshot here is named <file name>.<calling member>, with no help from the test framework.
public class NamingTests
{
    [Fact]
    public Task Object() =>
        Verify(new { Name = "Ada", Languages = new[] { "C#", "F#" } });

    [Fact]
    public Task Text() =>
        Verify("plain text");

    [Fact]
    public async Task AsyncMethod()
    {
        await Task.Yield();
        await Verify("from an async method");
    }

    [Fact]
    public Task LambdaUsesEnclosingMethod()
    {
        Func<Task> run = () => Verify("from a lambda");
        return run();
    }

    [Fact]
    public Task TaskResult() =>
        Verify(Task.FromResult(new { Answer = 42 }));

    [Fact]
    public Task ExplicitMethodName() =>
        Verify("renamed").UseMethodName("Renamed");

    [Fact]
    public Task ExplicitTypeName() =>
        Verify("retyped").UseTypeName("OtherType");

    [Fact]
    public Task ExplicitFileName() =>
        Verify("whole name").UseFileName("Custom");

    [Fact]
    public Task Extension() =>
        Verify("# Markdown").UseExtension("md");

    [Fact]
    public Task Subdirectory() =>
        Verify("in a subdirectory").UseDirectory("Snapshots");
}
