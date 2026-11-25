// The same checks as the MSTest and TUnit projects. Snapshotter references nothing from NUnit.
[TestFixture]
public class NUnitTests
{
    [Test]
    public Task Object() =>
        Verify(new { Framework = "NUnit", Answer = 42 });

    [Test]
    public async Task Async()
    {
        await Task.Yield();
        await Verify("after an await");
    }

    [TestCase(1, "one")]
    [TestCase(2, "two")]
    public Task Parameters(int number, string word) =>
        Verify($"{number} is {word}")
            .UseParameters(number, word);

    // NUnit's TestContext has the arguments, so they are named without UseParameters, as with Verify.NUnit.
    [TestCase(1, "one")]
    [TestCase(2, "two")]
    public Task ParametersFromContext(int number, string word) =>
        Verify($"{number} is {word}");
}
