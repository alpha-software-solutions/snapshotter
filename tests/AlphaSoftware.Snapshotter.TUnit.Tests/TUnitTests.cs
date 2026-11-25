// The same checks as the NUnit and MSTest projects. Snapshotter is told nothing about TUnit.
public class TUnitTests
{
    [Test]
    public Task Object() =>
        Verify(new { Framework = "TUnit", Answer = 42 });

    [Test]
    [Arguments(1, "one")]
    [Arguments(2, "two")]
    public Task Parameters(int number, string word) =>
        Verify($"{number} is {word}")
            .UseParameters(number, word);
}
