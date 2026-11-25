public class ParameterTests
{
    [Theory]
    [InlineData(1, "one")]
    [InlineData(2, "two")]
    public Task UseParameters(int number, string word) =>
        Verify($"{number} is {word}")
            .UseParameters(number, word);

    // xUnit v3's TestContext has the arguments, so they are named without UseParameters, as with Verify.XunitV3.
    [Theory]
    [InlineData(1, "one")]
    [InlineData(2, "two")]
    public Task FromTestContext(int number, string word) =>
        Verify($"{number} is {word}");

    [Theory]
    [InlineData("The<Value", null, true, 1.1)]
    public Task ValuesAreCleaned(string text, string? missing, bool flag, double value) =>
        Verify("cleaned")
            .UseParameters(text, missing, flag, value);

    [Theory]
    [InlineData("a", "b", "c")]
    public Task Subset(string arg1, string arg2, string arg3) =>
        Verify(arg3)
            .UseParameters(arg1, arg2);

    [Fact]
    public Task Collections() =>
        Run([1, 2, 3], new Dictionary<string, int> { ["value1"] = 10, ["value2"] = 11 });

    static Task Run(int[] values, Dictionary<string, int> map) =>
        Verify("collections")
            .UseParameters(values, map);

    [Fact]
    public Task Dates() =>
        RunDates(new DateTime(2000, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTimeOffset(2020, 1, 1, 2, 3, 4, TimeSpan.Zero));

    static Task RunDates(DateTime date, DateTimeOffset offset) =>
        Verify("dates")
            .UseParameters(date, offset);

    [Theory]
    [InlineData("case one")]
    public Task UseTextForParameters(string value) =>
        Verify(value)
            .UseTextForParameters("Custom");

    [Theory]
    [InlineData("One")]
    [InlineData("Two")]
    public Task IgnoreParametersForVerified(string arg) =>
        Verify("shared by every case")
            .IgnoreParametersForVerified(arg);

    [Theory]
    [InlineData("A", 1)]
    [InlineData("A", 2)]
    public Task IgnoreParameters(string kept, int ignored) =>
        Verify($"kept {kept}")
            .UseParameters(kept, ignored)
            .IgnoreParameters(nameof(ignored));

    [Fact]
    public async Task TooManyValuesThrow()
    {
        var exception = await Assert.ThrowsAsync<Exception>(async () => await Single("x"));
        Assert.Equal("The number of passed in parameters (2) must not exceed the number of parameters for the method (1).", exception.Message);
    }

    static Task Single(string value) =>
        Verify(value).UseParameters(value, "extra");

    [Fact]
    public void UseTextForParametersWithUseParametersThrows()
    {
        var settings = new VerifySettings();
        settings.UseParameters(1);
        var exception = Assert.Throws<Exception>(() => settings.UseTextForParameters("x"));
        Assert.Equal("UseTextForParameters is not compatible with UseParameters.", exception.Message);
    }
}
