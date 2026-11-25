// The same checks as the NUnit and MSTest projects. Snapshotter references nothing from TUnit.
public class TUnitTests
{
    [Test]
    public Task Object() =>
        Verify(new { Framework = "TUnit", Answer = 42 });

    [Test]
    public async Task Async()
    {
        await Task.Yield();
        await Verify("after an await");
    }

    [Test]
    [Arguments(1, "one")]
    [Arguments(2, "two")]
    public Task Parameters(int number, string word) =>
        Verify($"{number} is {word}")
            .UseParameters(number, word);

    // TUnit's TestContext has the arguments, so they are named without UseParameters, as with Verify.TUnit.
    [Test]
    [Arguments(1, "one")]
    [Arguments(2, "two")]
    public Task ParametersFromContext(int number, string word) =>
        Verify($"{number} is {word}");
}
