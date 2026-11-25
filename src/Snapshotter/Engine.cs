using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Snapshotter;

static class Engine
{
    static readonly UTF8Encoding utf8 = new(encoderShouldEmitUTF8Identifier: false);

    // Snapshots verified in this process, to catch two verifications writing the same file. Only passing
    // verifications are recorded, so a test framework that retries a failed test is not tripped up.
    static readonly ConcurrentDictionary<string, string> verified =
        new(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    public static async Task Run(SnapshotSettings settings, CallSite site, Func<SnapshotSettings, Task<Payload>> produce)
    {
        var location = Naming.Resolve(settings, site);
        var key = Path.Combine(location.Directory, location.Name);
        if (verified.TryGetValue(key, out var firstCaller))
        {
            throw new SnapshotterException(
                $"""
                 The snapshot '{location.Name}' was already verified in this run (from {firstCaller}). Each verification needs its own name:
                 give it one with UseMethodName(...) or UseTextForParameters(...), or pass a parameterized test's arguments with UseParameters(...).
                 """);
        }

        var payload = await produce(settings).ConfigureAwait(false);

        var matched = payload.Text != null
            ? CompareText(location, payload.Text, payload.Extension, settings)
            : CompareBytes(location, payload.Bytes!, payload.Extension, settings);

        if (matched)
        {
            verified.TryAdd(key, site.MemberName);
        }
    }

    static bool CompareText(SnapshotLocation location, string received, string extension, SnapshotSettings settings)
    {
        var verifiedPath = location.VerifiedPath(extension);
        var receivedPath = location.ReceivedPath(extension);
        var verifiedText = File.Exists(verifiedPath) ? Text.Normalize(File.ReadAllText(verifiedPath)) : null;

        if (verifiedText != null && TrimEnd(verifiedText) == TrimEnd(received))
        {
            DeleteIfExists(receivedPath);
            return true;
        }

        if (ShouldAccept(settings))
        {
            WriteText(verifiedPath, received);
            DeleteIfExists(receivedPath);
            return true;
        }

        WriteText(receivedPath, received);
        DiffTool.Launch(settings, receivedPath, verifiedPath);
        throw new SnapshotMismatchException(
            Messages.TextMismatch(location.Name, verifiedPath, receivedPath, verifiedText, received),
            verifiedPath,
            receivedPath);
    }

    static bool CompareBytes(SnapshotLocation location, byte[] received, string extension, SnapshotSettings settings)
    {
        var verifiedPath = location.VerifiedPath(extension);
        var receivedPath = location.ReceivedPath(extension);
        var verifiedBytes = File.Exists(verifiedPath) ? File.ReadAllBytes(verifiedPath) : null;

        if (verifiedBytes != null && verifiedBytes.AsSpan().SequenceEqual(received))
        {
            DeleteIfExists(receivedPath);
            return true;
        }

        if (ShouldAccept(settings))
        {
            File.WriteAllBytes(verifiedPath, received);
            DeleteIfExists(receivedPath);
            return true;
        }

        File.WriteAllBytes(receivedPath, received);
        DiffTool.Launch(settings, receivedPath, verifiedPath);
        throw new SnapshotMismatchException(
            Messages.BinaryMismatch(location.Name, verifiedPath, receivedPath, verifiedBytes, received),
            verifiedPath,
            receivedPath);
    }

    static bool ShouldAccept(SnapshotSettings settings)
    {
        if (settings.AutoAccept)
        {
            return true;
        }

        var value = Environment.GetEnvironmentVariable("SNAPSHOTTER_ACCEPT");
        return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    // Editors commonly add or remove a final newline; that alone is not a difference.
    static string TrimEnd(string text) =>
        text.TrimEnd('\n');

    static void WriteText(string path, string text)
    {
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text.EndsWith("\n", StringComparison.Ordinal) ? text : text + "\n", utf8);
    }

    static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static class Messages
{
    const int previewLines = 40;

    public static string TextMismatch(string name, string verifiedPath, string receivedPath, string? verified, string received)
    {
        var builder = new StringBuilder();
        if (verified == null)
        {
            builder.AppendLine($"Snapshot '{name}' has no verified file yet.");
            AppendPaths(builder, verifiedPath, receivedPath);
            builder.AppendLine();
            builder.AppendLine("Received:");
            AppendPreview(builder, received);
        }
        else
        {
            builder.AppendLine($"Snapshot '{name}' does not match the verified file.");
            AppendPaths(builder, verifiedPath, receivedPath);
            builder.AppendLine();
            AppendFirstDifference(builder, verified, received);
        }

        AppendHowToAccept(builder);
        return builder.ToString();
    }

    public static string BinaryMismatch(string name, string verifiedPath, string receivedPath, byte[]? verified, byte[] received)
    {
        var builder = new StringBuilder();
        builder.AppendLine(verified == null
            ? $"Snapshot '{name}' has no verified file yet."
            : $"Snapshot '{name}' does not match the verified file ({verified.Length} bytes verified, {received.Length} received).");
        AppendPaths(builder, verifiedPath, receivedPath);
        AppendHowToAccept(builder);
        return builder.ToString();
    }

    static void AppendPaths(StringBuilder builder, string verifiedPath, string receivedPath)
    {
        builder.AppendLine($"  Verified: {verifiedPath}");
        builder.AppendLine($"  Received: {receivedPath}");
    }

    static void AppendPreview(StringBuilder builder, string text)
    {
        var lines = text.TrimEnd('\n').Split('\n');
        foreach (var line in lines.Take(previewLines))
        {
            builder.AppendLine($"  {line}");
        }

        if (lines.Length > previewLines)
        {
            builder.AppendLine($"  ... {lines.Length - previewLines} more lines");
        }
    }

    static void AppendFirstDifference(StringBuilder builder, string verified, string received)
    {
        var verifiedLines = verified.TrimEnd('\n').Split('\n');
        var receivedLines = received.TrimEnd('\n').Split('\n');
        var count = Math.Max(verifiedLines.Length, receivedLines.Length);
        for (var index = 0; index < count; index++)
        {
            var expected = index < verifiedLines.Length ? verifiedLines[index] : null;
            var actual = index < receivedLines.Length ? receivedLines[index] : null;
            if (expected == actual)
            {
                continue;
            }

            builder.AppendLine($"First difference, line {index + 1}:");
            builder.AppendLine($"  - verified: {expected ?? "(no line)"}");
            builder.AppendLine($"  + received: {actual ?? "(no line)"}");
            return;
        }
    }

    static void AppendHowToAccept(StringBuilder builder)
    {
        builder.AppendLine();
        builder.Append("If the received result is right, rename the .received file to .verified, or rerun with SNAPSHOTTER_ACCEPT=1.");
    }
}

static class DiffTool
{
    static readonly string[] buildServerVariables =
    [
        "CI", "TF_BUILD", "GITHUB_ACTIONS", "GITLAB_CI", "TEAMCITY_VERSION", "JENKINS_URL", "BUILDKITE",
        "APPVEYOR", "TRAVIS", "CIRCLECI", "BITBUCKET_BUILD_NUMBER"
    ];

    public static void Launch(SnapshotSettings settings, string receivedPath, string verifiedPath)
    {
        var command = settings.DiffTool ?? Environment.GetEnvironmentVariable("SNAPSHOTTER_DIFF_TOOL");
        if (string.IsNullOrWhiteSpace(command) || IsBuildServer())
        {
            return;
        }

        try
        {
            // Most diff tools refuse to open a file that does not exist.
            if (!File.Exists(verifiedPath))
            {
                File.WriteAllText(verifiedPath, "");
            }

            command = command!.Replace("{received}", receivedPath).Replace("{verified}", verifiedPath);
            var windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = windows ? "cmd.exe" : "/bin/sh",
                Arguments = windows ? $"/c \"{command}\"" : $"-c \"{command.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch
        {
            // A diff tool is a convenience. Failing to start one must not hide the mismatch.
        }
    }

    static bool IsBuildServer() =>
        buildServerVariables.Any(name => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)));
}
