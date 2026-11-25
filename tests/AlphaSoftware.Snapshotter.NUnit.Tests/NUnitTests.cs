// The same checks as the MSTest and TUnit projects. Snapshotter is told nothing about NUnit.
[TestFixture]
public class NUnitTests
{
    [Test]
    public Task Object() =>
        Verify(new { Framework = "NUnit", Answer = 42 });

    [TestCase(1, "one")]
    [TestCase(2, "two")]
    public Task Parameters(int number, string word) =>
        Verify($"{number} is {word}")
            .UseParameters(number, word);
}
