public class HelperTests
{
    [Theory]
    [InlineData("/src/OrderTests.cs", "OrderTests")]
    [InlineData(@"C:\src\Widget.Part.cs", "Widget")]
    [InlineData("/src/Page.razor.cs", "Page")]
    [InlineData("/src/NoExtension", "NoExtension")]
    public void TypeNameFromFile(string sourceFile, string expected) =>
        Assert.Equal(expected, Naming.TypeNameFromFile(sourceFile));

    [Theory]
    [InlineData("Test", "Test")]
    [InlineData("<Main>$", "Main")]
    [InlineData(".ctor", "ctor")]
    public void CleanMemberName(string memberName, string expected) =>
        Assert.Equal(expected, Naming.CleanMemberName(memberName));

    [Fact]
    public void CleanMemberNameRejectsEmpty() =>
        Assert.Throws<SnapshotterException>(() => Naming.CleanMemberName(""));

    [Fact]
    public void FormatValue()
    {
        Assert.Equal("null", Naming.FormatValue(null));
        Assert.Equal("true", Naming.FormatValue(true));
        Assert.Equal("1.5", Naming.FormatValue(1.5));
        Assert.Equal("1,2", Naming.FormatValue(new[] { 1, 2 }));
    }

    [Fact]
    public void Sanitize() =>
        Assert.Equal("a-b-c", Naming.Sanitize(@"a/b\c"));
}
