// The same checks as the NUnit and TUnit projects. Snapshotter references nothing from MSTest.
[TestClass]
public class MSTestTests
{
    [TestMethod]
    public Task Object() =>
        Verify(new { Framework = "MSTest", Answer = 42 });

    [TestMethod]
    public async Task Async()
    {
        await Task.Yield();
        await Verify("after an await");
    }

    // MSTest has no ambient arguments, so, as with Verify.MSTest, they are passed in.
    [TestMethod]
    [DataRow(1, "one")]
    [DataRow(2, "two")]
    public Task Parameters(int number, string word) =>
        Verify($"{number} is {word}")
            .UseParameters(number, word);
}
