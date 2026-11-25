using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;

namespace AlphaSoftware.Snapshotter;

/// <summary>
/// One file a verification produced, before it is named.
/// </summary>
sealed class ProducedTarget(string extension, string? text, byte[]? bytes, string? name)
{
    public string Extension { get; } = extension;
    public string? Text { get; set; } = text;
    public byte[]? Bytes { get; } = bytes;
    public string? Name { get; } = name;
    public bool IsText => Text != null;
}

sealed class Produced(List<ProducedTarget> targets, object? original)
{
    public List<ProducedTarget> Targets { get; } = targets;
    public object? Original { get; } = original;
}

enum Outcome
{
    Equal,
    New,
    NotEqual
}

sealed class Comparison(ProducedTarget target, string verifiedPath, string receivedPath)
{
    public ProducedTarget Target { get; } = target;
    public string VerifiedPath { get; } = verifiedPath;
    public string ReceivedPath { get; } = receivedPath;
    public Outcome Outcome { get; set; }
    public string? VerifiedText { get; set; }
    public string? Message { get; set; }
    public bool AutoVerified { get; set; }
    public FilePair Pair => new(Target.Extension, ReceivedPath, VerifiedPath, Target.IsText);
}

static class Engine
{
    static readonly ConcurrentDictionary<string, string> prefixes =
        new(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);

    public static async Task<VerifyResult> Run(VerifySettings settings, CallSite site, Func<Serializer, Task<Produced>> produce)
    {
        VerifierSettings.Started(site.Description);
        var effective = EffectiveSettings.Create(settings, site.Assembly);
        var paths = Naming.Resolve(effective, site);

        var prefix = Path.Combine(paths.Directory, paths.ReceivedStem);
        if (effective.RequireUniquePrefix && !prefixes.TryAdd(prefix, site.Description))
        {
            throw new Exception(
                $"""
                 The prefix has already been used: {prefix}. This is mostly caused one of the following:
                  * A conflicting combination of the following APIs: VerifierSettings.DerivePathInfo, UseDirectory, UseTypeName, UseMethodName, UseFileName, UseParameters, UseTextForParameters
                  * Multiple calls to Verify or Throws in the same test method. Give each one a name with UseMethodName(...), or pass a parameterized test's arguments with UseParameters(...).
                 If that's not the case, and having multiple identical prefixes is acceptable, then call VerifierSettings.DisableRequireUniquePrefix() to disable this uniqueness validation.
                 """);
        }

        var handlers = effective.VerifyHandlers.ToList();
        foreach (var handler in handlers)
        {
            handler.Before();
        }

        try
        {
            var counter = new Counter(effective.Named, effective.DateCounting);
            using (Counter.Start(counter))
            {
                var produced = await produce(new Serializer(effective, counter)).ConfigureAwait(false);
                Scrub(produced, effective, counter, site);
                return await Compare(produced, effective, paths).ConfigureAwait(false);
            }
        }
        finally
        {
            foreach (var handler in handlers)
            {
                handler.After();
            }
        }
    }

    static void Scrub(Produced produced, EffectiveSettings settings, Counter counter, CallSite site)
    {
        DirectoryScrubber? directories = null;
        foreach (var target in produced.Targets.Where(target => target.IsText))
        {
            if (settings.ScrubbersDisabled)
            {
                target.Text = Newlines.Normalize(target.Text!);
                continue;
            }

            directories ??= DirectoryScrubber.Create(site.Assembly);
            var context = new ScrubContext(counter, settings.ScrubGuids, settings.ScrubDates, settings.Context);
            target.Text = TextScrubber.Scrub(target.Text!, settings.ScrubberLevels(target.Extension), context, directories);
        }
    }

    static async Task<VerifyResult> Compare(Produced produced, EffectiveSettings settings, SnapshotPaths paths)
    {
        Directory.CreateDirectory(paths.Directory);
        var comparisons = Name(produced.Targets, paths);

        DeleteStaleReceived(paths);
        var existing = ExistingVerified(paths);

        foreach (var comparison in comparisons)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(comparison.VerifiedPath)!);
            if (comparison.Target.IsText)
            {
                await CompareText(comparison, settings).ConfigureAwait(false);
            }
            else
            {
                await CompareBytes(comparison, settings).ConfigureAwait(false);
            }

            existing.RemoveWhere(path => string.Equals(path, comparison.VerifiedPath, StringComparison.OrdinalIgnoreCase));
        }

        var deleted = existing.OrderBy(path => path, StringComparer.Ordinal).ToList();
        var failed = comparisons.Where(comparison => comparison.Outcome != Outcome.Equal).ToList();
        if (failed.Count == 0 && deleted.Count == 0)
        {
            return Result(comparisons, produced);
        }

        foreach (var comparison in failed)
        {
            comparison.AutoVerified = ShouldAutoVerify(settings, paths, comparison.VerifiedPath);
        }

        var deletedAutoVerified = deleted.ToDictionary(path => path, path => ShouldAutoVerify(settings, paths, path));

        foreach (var comparison in failed)
        {
            if (comparison.Outcome == Outcome.New)
            {
                foreach (var handler in settings.FirstVerifyHandlers)
                {
                    await handler(comparison.Pair, comparison.Target.Text, comparison.AutoVerified).ConfigureAwait(false);
                }
            }
            else
            {
                foreach (var handler in settings.MismatchHandlers)
                {
                    await handler(comparison.Pair, comparison.Message, comparison.AutoVerified).ConfigureAwait(false);
                }
            }
        }

        foreach (var path in deleted)
        {
            foreach (var handler in settings.DeleteHandlers)
            {
                await handler(path, deletedAutoVerified[path]).ConfigureAwait(false);
            }
        }

        var message = Messages.Build(paths.Directory, comparisons, deleted);

        foreach (var comparison in failed.Where(comparison => comparison.AutoVerified))
        {
            File.Copy(comparison.ReceivedPath, comparison.VerifiedPath, overwrite: true);
            File.Delete(comparison.ReceivedPath);
        }

        foreach (var path in deleted.Where(path => deletedAutoVerified[path]))
        {
            File.Delete(path);
        }

        if (settings.Instance.DiffEnabled)
        {
            foreach (var comparison in failed.Where(comparison => !comparison.AutoVerified))
            {
                DiffTools.Launch(comparison.ReceivedPath, comparison.VerifiedPath, comparison.Target.IsText);
            }
        }

        var allAccepted = failed.All(comparison => comparison.AutoVerified) && deleted.All(path => deletedAutoVerified[path]);
        if (allAccepted && !settings.Instance.AutoVerifyThrows && !VerifierSettings.AutoVerifyThrows)
        {
            return Result(comparisons, produced);
        }

        throw new VerifyException(message);
    }

    static VerifyResult Result(List<Comparison> comparisons, Produced produced) =>
        new(comparisons.Select(comparison => (comparison.VerifiedPath, comparison.Target.Text)).ToList(), produced.Original);

    static bool ShouldAutoVerify(EffectiveSettings settings, SnapshotPaths paths, string verifiedPath)
    {
        if (VerifierSettings.AutoVerifyRule is { } global)
        {
            return global(paths.TypeName, paths.MethodName, verifiedPath);
        }

        return settings.Instance.AutoVerifyRule?.Invoke(verifiedPath) == true;
    }

    // Targets are grouped by name and extension; a group of more than one is numbered #00, #01, ...
    static List<Comparison> Name(List<ProducedTarget> targets, SnapshotPaths paths)
    {
        var groups = targets
            .GroupBy(target => (Name: target.Name ?? "", Extension: target.Extension.ToLowerInvariant()))
            .ToDictionary(group => group.Key, group => group.ToList());

        var comparisons = new List<Comparison>();
        foreach (var target in targets)
        {
            var group = groups[(target.Name ?? "", target.Extension.ToLowerInvariant())];
            var index = group.IndexOf(target).ToString("00");
            var numbered = group.Count > 1;
            string verified;
            string received;
            if (paths.UniqueDirectory)
            {
                var file = CleanTargetName(target.Name ?? "target") + (numbered ? "#" + index : "");
                verified = Path.Combine(paths.Directory, $"{file}.verified.{target.Extension}");
                received = Path.Combine(paths.Directory, $"{file}{paths.ReceivedStem}.received.{target.Extension}");
            }
            else
            {
                var suffix = target.Name == null
                    ? numbered ? "#" + index : ""
                    : "#" + CleanTargetName(target.Name) + (numbered ? "." + index : "");
                verified = Path.Combine(paths.Directory, $"{paths.VerifiedStem}{suffix}.verified.{target.Extension}");
                received = Path.Combine(paths.Directory, $"{paths.ReceivedStem}{suffix}.received.{target.Extension}");
            }

            comparisons.Add(new(target, verified, received));
        }

        return comparisons;
    }

    static string CleanTargetName(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            builder.Append(character is '"' or '<' or '>' or '|' or ':' or '*' or '?' || character < ' ' ? '-' : character);
        }

        return builder.ToString();
    }

    static void DeleteStaleReceived(SnapshotPaths paths)
    {
        var patterns = paths.UniqueDirectory
            ? new[] { "*.received.*" }
            : [$"{paths.ReceivedStem}.received.*", $"{paths.ReceivedStem}#*.received.*"];
        foreach (var pattern in patterns)
        {
            foreach (var file in SafeGetFiles(paths.Directory, pattern))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Left over from an earlier run; failing to remove it changes nothing.
                }
            }
        }
    }

    static HashSet<string> ExistingVerified(SnapshotPaths paths)
    {
        var patterns = paths.UniqueDirectory
            ? new[] { "*.verified.*" }
            : [$"{paths.VerifiedStem}.verified.*", $"{paths.VerifiedStem}#*.verified.*"];
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pattern in patterns)
        {
            foreach (var file in SafeGetFiles(paths.Directory, pattern))
            {
                files.Add(file);
            }
        }

        return files;
    }

    static string[] SafeGetFiles(string directory, string pattern)
    {
        try
        {
            return Directory.Exists(directory) ? Directory.GetFiles(directory, pattern) : [];
        }
        catch (ArgumentException)
        {
            return [];
        }
    }

    static async Task CompareText(Comparison comparison, EffectiveSettings settings)
    {
        var received = comparison.Target.Text!;
        var verifiedPath = comparison.VerifiedPath;
        if (File.Exists(verifiedPath) && new FileInfo(verifiedPath).Length == 0)
        {
            // An empty file is a placeholder a diff tool created, not a snapshot.
            File.Delete(verifiedPath);
        }

        if (!File.Exists(verifiedPath))
        {
            comparison.Outcome = Outcome.New;
            WriteText(comparison.ReceivedPath, received);
            return;
        }

        var verified = ReadText(verifiedPath);
        if (verified.IndexOf('\r') >= 0)
        {
            if (!VerifierSettings.FixNewlines)
            {
                WriteText(comparison.ReceivedPath, received);
                throw new Exception(
                    $"""
                     The verified file must use \n line endings, but it contains a \r.
                     Path: {verifiedPath}
                     This is usually git checking the file out with CRLF line endings. Add this line to .gitattributes:
                       *.verified.{comparison.Target.Extension} text eol=lf working-tree-encoding=UTF-8
                     then check the files out again with `git rm --cached -r .` and `git reset --hard`.
                     Or call VerifierSettings.FixNewlinesOnRead() to treat \r\n as \n.
                     """);
            }

            verified = Newlines.Normalize(verified);
        }

        comparison.VerifiedText = verified;
        if (received == verified ||
            (VerifierSettings.TolerateTrailingNewline && verified.Length == received.Length + 1 && verified.EndsWith("\n", StringComparison.Ordinal) && verified.StartsWith(received, StringComparison.Ordinal)))
        {
            comparison.Outcome = Outcome.Equal;
            return;
        }

        if (settings.StringComparer(comparison.Target.Extension) is { } compare)
        {
            var result = await compare(received, verified, settings.Context).ConfigureAwait(false);
            if (result.IsEqual)
            {
                comparison.Outcome = Outcome.Equal;
                return;
            }

            comparison.Message = result.Message;
        }

        comparison.Outcome = Outcome.NotEqual;
        WriteText(comparison.ReceivedPath, received);
    }

    static async Task CompareBytes(Comparison comparison, EffectiveSettings settings)
    {
        var received = comparison.Target.Bytes!;
        if (!File.Exists(comparison.VerifiedPath))
        {
            comparison.Outcome = Outcome.New;
            File.WriteAllBytes(comparison.ReceivedPath, received);
            return;
        }

        var verified = File.ReadAllBytes(comparison.VerifiedPath);
        if (verified.AsSpan().SequenceEqual(received))
        {
            comparison.Outcome = Outcome.Equal;
            return;
        }

        if (verified.Length > 0 && settings.StreamComparer(comparison.Target.Extension) is { } compare)
        {
            using var receivedStream = new MemoryStream(received);
            using var verifiedStream = new MemoryStream(verified);
            var result = await compare(receivedStream, verifiedStream, settings.Context).ConfigureAwait(false);
            if (result.IsEqual)
            {
                comparison.Outcome = Outcome.Equal;
                return;
            }

            comparison.Message = result.Message;
        }

        comparison.Outcome = Outcome.NotEqual;
        File.WriteAllBytes(comparison.ReceivedPath, received);
    }

    static string ReadText(string path)
    {
        // Reads with or without a byte order mark.
        var bytes = File.ReadAllBytes(path);
        var encoding = VerifierSettings.Encoding;
        var preamble = new UTF8Encoding(true).GetPreamble();
        if (bytes.Length >= preamble.Length && bytes.AsSpan(0, preamble.Length).SequenceEqual(preamble))
        {
            return Encoding.UTF8.GetString(bytes, preamble.Length, bytes.Length - preamble.Length);
        }

        var encodingPreamble = encoding.GetPreamble();
        var offset = encodingPreamble.Length > 0 && bytes.AsSpan().StartsWith(encodingPreamble) ? encodingPreamble.Length : 0;
        return encoding.GetString(bytes, offset, bytes.Length - offset);
    }

    static void WriteText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, VerifierSettings.Encoding);
        writer.Write(text);
    }
}
