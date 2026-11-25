using System.Text;

// What happens when a snapshot is new, differs or is gone. Each test works in its own temporary directory.
public sealed class FailureTests : IDisposable
{
    readonly string directory = Path.Combine(Path.GetTempPath(), "SnapshotterFailureTests", Guid.NewGuid().ToString("N"));

    public FailureTests() => System.IO.Directory.CreateDirectory(directory);

    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(directory, true);
        }
        catch (IOException)
        {
        }
    }

    VerifySettings Settings(string name)
    {
        var settings = new VerifySettings();
        settings.UseDirectory(directory);
        settings.UseFileName(name);
        settings.DisableDiff();
        return settings;
    }

    string InDirectory(string file) => System.IO.Path.Combine(directory, file);

    string[] Files() =>
        System.IO.Directory.GetFiles(directory).Select(System.IO.Path.GetFileName).OrderBy(file => file, StringComparer.Ordinal).ToArray()!;

    string Clean(string message) => message.Replace(directory, "{Directory}");

    [Fact]
    public async Task New()
    {
        var exception = await Assert.ThrowsAsync<VerifyException>(async () => await Verify(new { Value = 1 }, Settings("New")));

        // Only the received file: the verified one is created by accepting it.
        Assert.Single(Files());
        Assert.StartsWith("New.", Files()[0]);
        await Verify(Clean(exception.Message)).ScrubLinesWithReplace(line => line.Replace(Naming(), "{Runtime}"));
    }

    [Fact]
    public async Task NotEqual()
    {
        File.WriteAllText(InDirectory("NotEqual.verified.txt"), "line 1\nline 2\nline 3\nline 4\nline 5\nline 6\nline 7");
        var exception = await Assert.ThrowsAsync<VerifyException>(async () =>
            await Verify("line 1\nline 2\nline three\nline 4\nline 5\nline 6\nline 7\nline 8", Settings("NotEqual")));
        await Verify(Clean(exception.Message)).ScrubLinesWithReplace(line => line.Replace(Naming(), "{Runtime}"));
    }

    [Fact]
    public async Task Equal()
    {
        File.WriteAllText(InDirectory("Equal.verified.txt"), "same", new UTF8Encoding(true));
        File.WriteAllText(InDirectory($"Equal{Suffix()}.received.txt"), "stale");
        await Verify("same", Settings("Equal"));

        // The stale received file is gone and nothing new is written.
        Assert.Equal(["Equal.verified.txt"], Files());
    }

    [Fact]
    public async Task VerifiedWithoutBom()
    {
        File.WriteAllText(InDirectory("NoBom.verified.txt"), "same", new UTF8Encoding(false));
        await Verify("same", Settings("NoBom"));
    }

    [Fact]
    public async Task ReceivedHasBomAndLineFeeds()
    {
        await Assert.ThrowsAsync<VerifyException>(async () => await Verify("a\r\nb", Settings("Bom")));
        var bytes = File.ReadAllBytes(InDirectory($"Bom{Suffix()}.received.txt"));
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'a', (byte)'\n', (byte)'b' }, bytes);
    }

    [Fact]
    public async Task TrailingNewlineMatters()
    {
        File.WriteAllText(InDirectory("Trailing.verified.txt"), "text\n");
        await Assert.ThrowsAsync<VerifyException>(async () => await Verify("text", Settings("Trailing")));
    }

    [Fact]
    public async Task CarriageReturnInVerifiedFileFails()
    {
        File.WriteAllText(InDirectory("Crlf.verified.txt"), "a\r\nb");
        var exception = await Assert.ThrowsAsync<Exception>(async () => await Verify("a\nb", Settings("Crlf")));
        Assert.StartsWith("The verified file must use \\n line endings, but it contains a \\r.", exception.Message);
    }

    [Fact]
    public async Task EmptyVerifiedFileIsNew()
    {
        File.WriteAllText(InDirectory("Empty.verified.txt"), "");
        var exception = await Assert.ThrowsAsync<VerifyException>(async () => await Verify("content", Settings("Empty")));
        Assert.Contains("\nNew:\n", exception.Message);
        Assert.False(File.Exists(InDirectory("Empty.verified.txt")));
    }

    [Fact]
    public async Task Delete()
    {
        File.WriteAllText(InDirectory("Delete.verified.txt"), "kept");
        File.WriteAllText(InDirectory("Delete#extra.verified.txt"), "no longer produced");
        var exception = await Assert.ThrowsAsync<VerifyException>(async () => await Verify("kept", Settings("Delete")));
        Assert.Contains("Delete:\n  - Delete#extra.verified.txt", exception.Message);
    }

    [Fact]
    public async Task AutoVerify()
    {
        var settings = Settings("Auto");
        settings.AutoVerify();
        await Verify("accepted", settings);
        Assert.Equal(["Auto.verified.txt"], Files());
        Assert.Equal("accepted", File.ReadAllText(InDirectory("Auto.verified.txt")));
    }

    [Fact]
    public async Task AutoVerifyThrowException()
    {
        var settings = Settings("AutoThrow");
        settings.AutoVerify(throwException: true);
        await Assert.ThrowsAsync<VerifyException>(async () => await Verify("accepted", settings));
        Assert.Equal(["AutoThrow.verified.txt"], Files());
    }

    [Fact]
    public async Task AutoVerifyDeletesStaleFiles()
    {
        File.WriteAllText(InDirectory("AutoDelete.verified.txt"), "kept");
        File.WriteAllText(InDirectory("AutoDelete#extra.verified.txt"), "stale");
        var settings = Settings("AutoDelete");
        settings.AutoVerify();
        await Verify("kept", settings);
        Assert.Equal(["AutoDelete.verified.txt"], Files());
    }

    [Fact]
    public async Task BinaryMismatch()
    {
        File.WriteAllBytes(InDirectory("Binary.verified.bin"), [1, 2, 3]);
        var exception = await Assert.ThrowsAsync<VerifyException>(async () => await Verify(new byte[] { 1, 2, 4 }, Settings("Binary")));
        Assert.Contains("NotEqual:", exception.Message);
        Assert.DoesNotContain("FileContent:", exception.Message);
    }

    [Fact]
    public async Task Callbacks()
    {
        var calls = new List<string>();
        var settings = Settings("Callbacks");
        settings.OnVerify(() => calls.Add("before"), () => calls.Add("after"));
        settings.OnFirstVerify((pair, text, autoVerify) =>
        {
            calls.Add($"first {System.IO.Path.GetFileName(pair.VerifiedPath)} {text} {autoVerify}");
            return Task.CompletedTask;
        });
        await Assert.ThrowsAsync<VerifyException>(async () => await Verify("value", settings));
        Assert.Equal(["before", "first Callbacks.verified.txt value False", "after"], calls);
    }

    [Fact]
    public async Task CustomStringComparer()
    {
        File.WriteAllText(InDirectory("Comparer.verified.txt"), "VALUE");
        var settings = Settings("Comparer");
        settings.UseStringComparer((received, verified, _) =>
            Task.FromResult(string.Equals(received, verified, StringComparison.OrdinalIgnoreCase) ? CompareResult.Equal : CompareResult.NotEqual("differs")));
        await Verify("value", settings);
    }

    [Fact]
    public void ConfigureAfterStartThrows()
    {
        var task = Verify("x", Settings("Started"));
        _ = task.ToTask().ContinueWith(_ => { }, TaskScheduler.Default);
        Assert.Throws<InvalidOperationException>(() => { task.UseMethodName("late"); });
    }

    // Received files of multi-targeted projects carry the runtime, so every framework shares one verified file.
    static string Suffix() => "." + Naming();

    static string Naming() =>
        System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework")
            ? "Net4_8"
            : $"DotNet{Environment.Version.Major}_{Environment.Version.Minor}";
}
