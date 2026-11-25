public class ParameterTests
{
    // The parameter names come from this method, found by reflection.
    [Theory]
    [InlineData(1, "one")]
    [InlineData(2, "two")]
    public Task UseParameters(int number, string word) =>
        Verify($"{number} is {word}")
            .UseParameters(number, word);

    [Theory]
    [InlineData(new[] { 1, 2, 3 }, 1.5)]
    public Task FormatsCollectionsAndNumbers(int[] values, double factor) =>
        Verify(values.Select(value => value * factor))
            .UseParameters(values, factor);

    [Theory]
    [InlineData(3)]
    public Task UseTextForParameters(int number) =>
        Verify(number)
            .UseTextForParameters($"case{number}");

    [Fact]
    public async Task UseParametersOnAMethodWithoutParametersExplains()
    {
        var exception = await Assert.ThrowsAsync<SnapshotterException>(
            () => Verify("value").UseParameters("value").ToTask());
        Assert.Contains("'ParameterTests.UseParametersOnAMethodWithoutParametersExplains'", exception.Message);
        Assert.Contains("UseTextForParameters", exception.Message);
    }
}
