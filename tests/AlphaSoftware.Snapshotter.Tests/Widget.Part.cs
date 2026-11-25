// A file name with more than one dot: the type name is the part before the first dot.
public class Widget
{
    [Fact]
    public Task FromPartialFile() =>
        Verify("Widget.Part.cs");
}
