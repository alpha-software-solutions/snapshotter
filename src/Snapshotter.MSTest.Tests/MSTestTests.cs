// The same checks as the NUnit and TUnit projects. Snapshotter is told nothing about MSTest.
[TestClass]
public class MSTestTests
{
    [TestMethod]
    public Task Object() =>
        Verify(new { Framework = "MSTest", Answer = 42 });

    [TestMethod]
    [DataRow(1, "one")]
    [DataRow(2, "two")]
    public Task Parameters(int number, string word) =>
        Verify($"{number} is {word}")
            .UseParameters(number, word);
}
