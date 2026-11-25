// Each test works in its own temporary directory, so the files it creates on purpose never mix with the
// committed snapshots.
public sealed class FailureTests : IDisposable
{
    readonly string directory = Path.Combine(Path.GetTempPath(), "snapshotter-tests", Guid.NewGuid().ToString("N"));

    public FailureTests() =>
        Directory.CreateDirectory(directory);

    public void Dispose() =>
        Directory.Delete(directory, recursive: true);

    string PathOf(string file) =>
        Path.Combine(directory, file);

    [Fact]
    public async Task Mismatch()
    {
        File.WriteAllText(PathOf("FailureTests.Mismatch.verified.txt"), "line\nexpected\n");

        var exception = await Assert.ThrowsAsync<SnapshotMismatchException>(
            () => Verify("line\nactual").UseDirectory(directory).ToTask());

        Assert.Equal(PathOf("FailureTests.Mismatch.received.txt"), exception.ReceivedPath);
        Assert.Equal("line\nactual\n", File.ReadAllText(exception.ReceivedPath));
        Assert.Contains("does not match", exception.Message);
        Assert.Contains("First difference, line 2:", exception.Message);
        Assert.Contains("- verified: expected", exception.Message);
        Assert.Contains("+ received: actual", exception.Message);
        Assert.Contains("SNAPSHOTTER_ACCEPT", exception.Message);
    }

    [Fact]
    public async Task New()
    {
        var exception = await Assert.ThrowsAsync<SnapshotMismatchException>(
            () => Verify(new { Value = 1 }).UseDirectory(directory).ToTask());

        Assert.False(File.Exists(exception.VerifiedPath));
        Assert.True(File.Exists(exception.ReceivedPath));
        Assert.Contains("has no verified file yet", exception.Message);
        Assert.Contains("\"Value\": 1", exception.Message);
    }

    [Fact]
    public async Task MatchRemovesStaleReceivedFile()
    {
        File.WriteAllText(PathOf("FailureTests.MatchRemovesStaleReceivedFile.verified.txt"), "same");
        File.WriteAllText(PathOf("FailureTests.MatchRemovesStaleReceivedFile.received.txt"), "old");

        await Verify("same").UseDirectory(directory);

        Assert.False(File.Exists(PathOf("FailureTests.MatchRemovesStaleReceivedFile.received.txt")));
    }

    [Fact]
    public async Task LineEndingsByteOrderMarkAndFinalNewlineDoNotMatter()
    {
        File.WriteAllText(PathOf("FailureTests.LineEndingsByteOrderMarkAndFinalNewlineDoNotMatter.verified.txt"), "\uFEFFa\r\nb\r\n\r\n");

        await Verify("a\nb").UseDirectory(directory);
    }

    [Fact]
    public async Task AutoAcceptWritesVerifiedFile()
    {
        await Verify("accepted").UseDirectory(directory).AutoAccept();

        Assert.Equal("accepted\n", File.ReadAllText(PathOf("FailureTests.AutoAcceptWritesVerifiedFile.verified.txt")));
        Assert.False(File.Exists(PathOf("FailureTests.AutoAcceptWritesVerifiedFile.received.txt")));
    }

    [Fact]
    public async Task BinaryMismatch()
    {
        File.WriteAllBytes(PathOf("FailureTests.BinaryMismatch.verified.bin"), [1, 2, 3]);

        var exception = await Assert.ThrowsAsync<SnapshotMismatchException>(
            () => Verify(new byte[] { 1, 2, 4 }, "bin").UseDirectory(directory).ToTask());

        Assert.Equal(new byte[] { 1, 2, 4 }, File.ReadAllBytes(exception.ReceivedPath));
        Assert.Contains("3 bytes verified, 3 received", exception.Message);
    }

    [Fact]
    public async Task SameNameTwiceInOneTest()
    {
        await Verify("first").UseDirectory(directory).AutoAccept();

        var exception = await Assert.ThrowsAsync<SnapshotterException>(
            () => Verify("second").UseDirectory(directory).ToTask());
        Assert.Contains("already verified in this run", exception.Message);
    }

    [Fact]
    public async Task ConfiguringAfterStartingFails()
    {
        var snapshot = Verify("value").UseDirectory(directory).AutoAccept();
        await snapshot;

        Assert.Throws<InvalidOperationException>(() => { snapshot.UseMethodName("Late"); });
    }
}
