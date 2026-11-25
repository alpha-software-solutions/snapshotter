using System.Runtime.CompilerServices;

public class BinaryTests
{
    static readonly byte[] bytes = Enumerable.Range(0, 16).Select(value => (byte) value).ToArray();

    static string SourceDirectory([CallerFilePath] string path = "") =>
        Path.GetDirectoryName(path)!;

    [Fact]
    public Task Bytes() =>
        Verify(bytes, "bin");

    [Fact]
    public Task Stream() =>
        Verify(new MemoryStream(bytes), "bin");

    [Fact]
    public Task TextFile() =>
        VerifyFile(Path.Combine(SourceDirectory(), "Inputs", "notes.txt"));

    [Fact]
    public Task BinaryFile() =>
        VerifyFile(Path.Combine(SourceDirectory(), "Inputs", "data.bin"));
}
