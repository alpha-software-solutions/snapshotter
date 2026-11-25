public class ParameterTests
{
    // UseParameters needs the parameter names; Snapshotter reads them from this method by reflection.
    [Theory]
    [InlineData(1, "one")]
    [InlineData(2, "two")]
    public Task UseParameters(int number, string word) =>
        Verify($"{number} is {word}")
            .UseParameters(number, word);

    [Theory]
    [InlineData(3)]
    public Task UseTextForParameters(int number) =>
        Verify(number)
            .UseTextForParameters($"case{number}");

    [Fact]
    public async Task UseParametersWithNoMatchingMethodExplains()
    {
        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => Verify("value")
                .UseParameters("value")
                .ToTask());
        // The method has no parameters, so there is nothing to name the value after.
        Assert.Contains("ParameterTests.UseParametersWithNoMatchingMethodExplains", exception.Message);
        Assert.Contains("UseTextForParameters", exception.Message);
    }
}
