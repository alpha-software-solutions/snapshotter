// Snapshot files are named <file name>.<calling member>, with no test framework involved.
public class NamingTests
{
    [Fact]
    public Task Object() =>
        Verify(new
        {
            Name = "Ada",
            Languages = new[] { "C#", "F#" }
        });

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
    public Task Json() =>
        VerifyJson("""{"b": 2, "a": 1}""");

    [Fact]
    public Task Throws() =>
        Verifier.Throws(() => throw new InvalidOperationException("boom"))
            .IgnoreStackTrace();

    [Fact]
    public Task ThrowsTask() =>
        Verifier.ThrowsTask(() => throw new ArgumentException("bad argument", "name"))
            .IgnoreStackTrace();

    [Fact]
    public Task CombinationOfInputs() =>
        Verifier.Combination().Verify(
            (int a, string b) => $"{a}{b}",
            [1, 2],
            ["x", "y"]);

    [Fact]
    public Task ExplicitMethodName() =>
        Verify("renamed").UseMethodName("Renamed");

    [Fact]
    public Task ExplicitTypeName() =>
        Verify("retyped").UseTypeName("OtherType");
}
