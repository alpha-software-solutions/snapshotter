public class HelperTests
{
    [Theory]
    [InlineData("/src/OrderTests.cs", "OrderTests")]
    [InlineData(@"C:\src\Widget.Part.cs", "Widget")]
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
    public void Clean() =>
        Assert.Equal("a-b-c-d-e x,y=z", Naming.Clean("a/b\\c#d:e x,y=z"));

    [Fact]
    public void Diff()
    {
        var diff = TextDiff.Compact(
            "line 1\nline 2\nline 3\nline 4\nline 5\nline 6\nline 7",
            "line 1\nline 2\nline three\nline 4\nline 5\nline 6\nline 7\nline 8");
        Assert.Equal("2 line 2\n- line 3\n+ line three\n4 line 4\n\n7 line 7\n+ line 8\n  [EOF]", diff);
    }

    [Fact]
    public void DiffAtStart() =>
        Assert.Equal("  [BOF]\n- a\n+ b\n2 c", TextDiff.Compact("a\nc", "b\nc"));

    [Theory]
    [InlineData(2d, "2.0")]
    [InlineData(1.5d, "1.5")]
    [InlineData(1e20, "1E+20")]
    public void Double(double value, string expected) =>
        Assert.Equal(expected, Formatting.Double(value));

    [Fact]
    public void TypeNames()
    {
        Assert.Equal("Dictionary<string, int?>", Formatting.TypeName(typeof(Dictionary<string, int?>)));
        Assert.Equal("Environment.SpecialFolder", Formatting.TypeName(typeof(Environment.SpecialFolder)));
        Assert.Equal("int[]", Formatting.TypeName(typeof(int[])));
        Assert.Equal("dynamic", Formatting.TypeName(new { A = 1 }.GetType()));
    }

    [Fact]
    public void StackTraceCleaning() =>
        Assert.Equal(
            "at Outer.Inner.Method(String value)\nat Program.Main()",
            Serializer.CleanStackTrace(
                "   at Outer+Inner.Method(String value) in C:\\src\\File.cs:line 10\r\n   at Program.<Run>d__0.MoveNext()\r\n--- End of stack trace from previous location ---\r\n   at Program.Main()"));
}
