public class HelperTests
{
    [Theory]
    [InlineData("/src/OrderTests.cs", "OrderTests")]
    [InlineData(@"C:\src\Widget.Part.cs", "Widget")]
    [InlineData("/src/Page.razor.cs", "Page")]
    [InlineData("/src/NoExtension", "NoExtension")]
    public void GetTypeName(string sourceFile, string expected) =>
        Assert.Equal(expected, Verifier.GetTypeName(sourceFile));

    [Theory]
    [InlineData("Test", "Test")]
    [InlineData("<Main>$", "Main")]
    [InlineData(".ctor", "ctor")]
    public void CleanMethodName(string methodName, string expected) =>
        Assert.Equal(expected, Verifier.CleanMethodName(methodName));

    [Fact]
    public void CleanMethodNameRejectsEmpty() =>
        Assert.ThrowsAny<Exception>(() => Verifier.CleanMethodName(""));
}
