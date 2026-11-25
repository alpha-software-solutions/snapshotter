// A file name with more than one dot: the type name is the part before the first dot, the same
// rule the engine's MSBuild files use to nest snapshots under their source file.
public class Widget
{
    [Fact]
    public Task FromPartialFile() =>
        Verify("Widget.Part.cs");
}
