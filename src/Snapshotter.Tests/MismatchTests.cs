public class MismatchTests
{
    // The committed MismatchTests.Mismatch.verified.txt holds "expected", so this must fail. A failure is
    // an ordinary exception, which is how any test framework, or a console app, sees it.
    [Fact]
    public async Task Mismatch()
    {
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => Verify("actual").ToTask());
        Assert.Contains("MismatchTests.Mismatch", exception.Message);
        foreach (var received in Directory.EnumerateFiles(AttributeReader.GetProjectDirectory(), "MismatchTests.Mismatch.*received.*"))
        {
            File.Delete(received);
        }
    }
}
