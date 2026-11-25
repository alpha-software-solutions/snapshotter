using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace AlphaSoftware.Snapshotter;

/// <summary>
/// Where <c>AddScrubber</c> puts a scrubber in its level's list.
/// </summary>
public enum ScrubberLocation
{
    /// <summary>
    /// Before the scrubbers already added, so several <c>First</c> scrubbers run in reverse order.
    /// </summary>
    First,

    /// <summary>
    /// After the scrubbers already added.
    /// </summary>
    Last
}

/// <summary>
/// The Guid forms <c>ScrubInlineGuids</c> looks for.
/// </summary>
[Flags]
public enum GuidFormats
{
    /// <summary>
    /// <c>173535ae-995b-4cc6-a74e-8cd4be57039c</c>, also inside braces or parentheses.
    /// </summary>
    Dashed = 1,

    /// <summary>
    /// <c>173535ae995b4cc6a74e8cd4be57039c</c>. Also matches 32 character hex hashes such as MD5.
    /// </summary>
    Undashed = 2,

    /// <summary>
    /// Both forms.
    /// </summary>
    All = Dashed | Undashed
}

/// <summary>
/// The scrubbers of one level (global, or one verification), optionally for one extension.
/// </summary>
sealed class ScrubberSet
{
    public List<Func<string, bool>> LineRemovals { get; private set; } = [];
    public List<Func<string, string?>> LineReplaces { get; private set; } = [];
    public List<InlineScrubber> Inline { get; private set; } = [];
    public List<Action<StringBuilder, Counter, IReadOnlyDictionary<string, object>>> Legacy { get; private set; } = [];
    public bool RemoveEmptyLines { get; set; }

    public bool IsEmpty =>
        LineRemovals.Count == 0 && LineReplaces.Count == 0 && Inline.Count == 0 && Legacy.Count == 0 && !RemoveEmptyLines;

    public void AddLegacy(Action<StringBuilder, Counter, IReadOnlyDictionary<string, object>> scrubber, ScrubberLocation location)
    {
        if (location == ScrubberLocation.First)
        {
            Legacy.Insert(0, scrubber);
        }
        else
        {
            Legacy.Add(scrubber);
        }
    }

    public ScrubberSet Clone() =>
        new()
        {
            LineRemovals = [.. LineRemovals],
            LineReplaces = [.. LineReplaces],
            Inline = [.. Inline],
            Legacy = [.. Legacy],
            RemoveEmptyLines = RemoveEmptyLines
        };
}

/// <summary>
/// Replaces matches inside a line. Text another inline scrubber produced is never scanned again.
/// </summary>
abstract class InlineScrubber
{
    /// <summary>
    /// The longest text it can match, or null when unbounded. Longer scrubbers run first.
    /// </summary>
    public abstract int? MaxLength { get; }

    public abstract IEnumerable<(int Start, int Length, string Replacement)> Find(string line, ScrubContext context);
}

sealed class ScrubContext(Counter counter, bool scrubGuids, bool scrubDates, IReadOnlyDictionary<string, object> values)
{
    public Counter Counter { get; } = counter;
    public bool ScrubGuids { get; } = scrubGuids;
    public bool ScrubDates { get; } = scrubDates;
    public IReadOnlyDictionary<string, object> Values { get; } = values;
}

sealed class ReplaceScrubber(string find, string replacement, StringComparison comparison, bool wordBoundary) : InlineScrubber
{
    public override int? MaxLength => find.Length;

    public override IEnumerable<(int Start, int Length, string Replacement)> Find(string line, ScrubContext context)
    {
        if (find.Length == 0)
        {
            yield break;
        }

        var index = 0;
        while ((index = line.IndexOf(find, index, comparison)) >= 0)
        {
            var end = index + find.Length;
            if (!wordBoundary || (!IsWordChar(line, index - 1) && !IsWordChar(line, end)))
            {
                yield return (index, find.Length, replacement);
                index = end;
            }
            else
            {
                index++;
            }
        }
    }

    internal static bool IsWordChar(string text, int index) =>
        index >= 0 && index < text.Length && char.IsLetterOrDigit(text[index]);
}

sealed class GuidScrubber(GuidFormats formats) : InlineScrubber
{
    static readonly Regex dashed = new(
        "(?<![0-9A-Za-z])[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}(?![0-9A-Za-z])",
        RegexOptions.CultureInvariant);

    static readonly Regex undashed = new("(?<![0-9A-Za-z])[0-9A-Fa-f]{32}(?![0-9A-Za-z])", RegexOptions.CultureInvariant);

    public override int? MaxLength => null;

    public override IEnumerable<(int Start, int Length, string Replacement)> Find(string line, ScrubContext context)
    {
        if (!context.ScrubGuids)
        {
            return [];
        }

        var matches = new List<(int, int, string)>();
        if (formats.HasFlag(GuidFormats.Dashed))
        {
            matches.AddRange(dashed.Matches(line).Cast<Match>()
                .Select(match => (match.Index, match.Length, context.Counter.NextString(Guid.Parse(match.Value)))));
        }

        if (formats.HasFlag(GuidFormats.Undashed))
        {
            matches.AddRange(undashed.Matches(line).Cast<Match>()
                .Select(match => (match.Index, match.Length, context.Counter.NextString(Guid.ParseExact(match.Value, "N")))));
        }

        return matches.OrderBy(match => match.Item1);
    }
}

enum InlineDateKind
{
    DateTime,
    DateTimeOffset,
    Date
}

sealed class DateScrubber : InlineScrubber
{
    readonly string format;
    readonly CultureInfo? culture;
    readonly InlineDateKind kind;
    readonly Dictionary<string, (int Min, int Max)> lengths = new();

    public DateScrubber(string format, CultureInfo? culture, InlineDateKind kind)
    {
        this.format = format;
        this.culture = culture;
        this.kind = kind;
    }

    public override int? MaxLength => Lengths(culture ?? CultureInfo.CurrentCulture).Max;

    (int Min, int Max) Lengths(CultureInfo current)
    {
        lock (lengths)
        {
            if (lengths.TryGetValue(current.Name, out var cached))
            {
                return cached;
            }

            var min = int.MaxValue;
            var max = 0;
            for (var month = 1; month <= 12; month++)
            {
                foreach (var day in new[] { 1, 9, 10, 28 })
                {
                    foreach (var time in new[] { new TimeSpan(0, 0, 0), new TimeSpan(0, 1, 1, 1, 1), new TimeSpan(0, 23, 59, 59, 999) })
                    {
                        var length = new DateTimeOffset(new DateTime(2000, month, day) + time, TimeSpan.FromHours(10)).ToString(format, current).Length;
                        min = Math.Min(min, length);
                        max = Math.Max(max, length);
                    }
                }
            }

            return lengths[current.Name] = (min, max);
        }
    }

    public override IEnumerable<(int Start, int Length, string Replacement)> Find(string line, ScrubContext context)
    {
        if (!context.ScrubDates)
        {
            yield break;
        }

        var current = culture ?? CultureInfo.CurrentCulture;
        var (min, max) = Lengths(current);
        var index = 0;
        while (index <= line.Length - min)
        {
            var matched = false;
            for (var length = Math.Min(max, line.Length - index); length >= min; length--)
            {
                var candidate = line.Substring(index, length);
                if (TryReplace(candidate, current, context.Counter, out var replacement))
                {
                    yield return (index, length, replacement);
                    index += length;
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                index++;
            }
        }
    }

    bool TryReplace(string candidate, CultureInfo current, Counter counter, out string replacement)
    {
        switch (kind)
        {
            case InlineDateKind.DateTimeOffset when DateTimeOffset.TryParseExact(candidate, format, current, DateTimeStyles.None, out var offset):
                replacement = counter.NextString(offset);
                return true;
            case InlineDateKind.DateTime when DateTime.TryParseExact(candidate, format, current, DateTimeStyles.None, out var dateTime):
                replacement = counter.NextString(dateTime);
                return true;
#if NET6_0_OR_GREATER
            case InlineDateKind.Date when DateOnly.TryParseExact(candidate, format, current, DateTimeStyles.None, out var date):
                replacement = counter.NextString(date);
                return true;
#else
            case InlineDateKind.Date when DateTime.TryParseExact(candidate, format, current, DateTimeStyles.None, out var date):
                replacement = counter.NextString(date);
                return true;
#endif
            default:
                replacement = candidate;
                return false;
        }
    }
}

/// <summary>
/// Runs the text scrubbers over a whole document, in the fixed order Verify uses.
/// </summary>
static class TextScrubber
{
    public static string Scrub(string text, IReadOnlyList<ScrubberSet> levels, ScrubContext context, DirectoryScrubber? directories)
    {
        text = Newlines.Normalize(text);

        var removals = levels.SelectMany(level => level.LineRemovals).ToList();
        var replaces = levels.SelectMany(level => level.LineReplaces).ToList();
        var removeEmpty = levels.Any(level => level.RemoveEmptyLines);
        if (removals.Count > 0 || replaces.Count > 0 || removeEmpty)
        {
            text = ScrubLines(text, removals, replaces, removeEmpty);
        }

        var inline = levels
            .SelectMany((level, levelIndex) => level.Inline.Select((scrubber, index) => (scrubber, levelIndex, index)))
            .OrderBy(entry => entry.scrubber.MaxLength.HasValue ? 1 : 0)
            .ThenByDescending(entry => entry.scrubber.MaxLength ?? 0)
            .ThenBy(entry => entry.levelIndex)
            .ThenBy(entry => entry.index)
            .Select(entry => entry.scrubber)
            .ToList();
        if (inline.Count > 0)
        {
            text = ScrubInline(text, inline, context);
        }

        var legacy = levels.SelectMany(level => level.Legacy).ToList();
        if (legacy.Count > 0)
        {
            var builder = new StringBuilder(text);
            foreach (var scrubber in legacy)
            {
                scrubber(builder, context.Counter, context.Values);
            }

            text = builder.ToString();
        }

        if (directories != null)
        {
            text = directories.Replace(text);
        }

        return Newlines.Normalize(text);
    }

    static string ScrubLines(string text, List<Func<string, bool>> removals, List<Func<string, string?>> replaces, bool removeEmpty)
    {
        var endsWithNewline = text.EndsWith("\n", StringComparison.Ordinal);
        var body = endsWithNewline ? text.Substring(0, text.Length - 1) : text;
        var lines = new List<string>();
        foreach (var line in body.Split('\n'))
        {
            if (removals.Any(remove => remove(line)) || (removeEmpty && string.IsNullOrWhiteSpace(line)))
            {
                continue;
            }

            string? current = line;
            foreach (var replace in replaces)
            {
                current = replace(current);
                if (current == null)
                {
                    break;
                }
            }

            if (current != null)
            {
                lines.AddRange(Newlines.Normalize(current).Split('\n'));
            }
        }

        var result = string.Join("\n", lines);
        return endsWithNewline && !removeEmpty ? result + "\n" : result;
    }

    static string ScrubInline(string text, List<InlineScrubber> scrubbers, ScrubContext context)
    {
        // Segments of the document; frozen ones were produced by a scrubber and are not scanned again.
        var segments = new List<(string Text, bool Frozen)> { (text, false) };
        foreach (var scrubber in scrubbers)
        {
            var next = new List<(string Text, bool Frozen)>();
            foreach (var segment in segments)
            {
                if (segment.Frozen)
                {
                    next.Add(segment);
                    continue;
                }

                var lines = segment.Text.Split('\n');
                for (var index = 0; index < lines.Length; index++)
                {
                    if (index > 0)
                    {
                        next.Add(("\n", false));
                    }

                    var line = lines[index];
                    var position = 0;
                    foreach (var (start, length, replacement) in scrubber.Find(line, context))
                    {
                        if (start < position)
                        {
                            continue;
                        }

                        if (start > position)
                        {
                            next.Add((line.Substring(position, start - position), false));
                        }

                        next.Add((replacement, true));
                        position = start + length;
                    }

                    if (position < line.Length)
                    {
                        next.Add((line.Substring(position), false));
                    }
                }
            }

            segments = next;
        }

        return string.Concat(segments.Select(segment => segment.Text));
    }
}

/// <summary>
/// Replaces well known directories with <c>{SolutionDirectory}</c>, <c>{ProjectDirectory}</c> and the like.
/// </summary>
sealed class DirectoryScrubber
{
    readonly List<(string Path, string Token)> candidates;

    DirectoryScrubber(List<(string Path, string Token)> candidates) =>
        this.candidates = candidates;

    public static DirectoryScrubber Create(Assembly? assembly)
    {
        var list = new List<(string, string)>();

        void Add(string? path, string token)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var trimmed = path!.TrimEnd('/', '\\');
            // Never a drive or file system root: that would replace the start of every path.
            if (trimmed.Length <= 3)
            {
                return;
            }

            if (list.All(existing => !SamePath(existing.Item1, trimmed)))
            {
                list.Add((trimmed, token));
            }
        }

        var paths = ProjectInfo.For(assembly);
        if (VerifierSettings.ScrubProjectDirectoryEnabled)
        {
            Add(paths.ProjectDirectory, "{ProjectDirectory}");
        }

        if (VerifierSettings.ScrubSolutionDirectoryEnabled)
        {
            Add(paths.SolutionDirectory, "{SolutionDirectory}");
        }

        Add(AppDomain.CurrentDomain.BaseDirectory, "{CurrentDirectory}");
        Add(Environment.CurrentDirectory, "{CurrentDirectory}");
        Add(Path.GetTempPath(), "{TempPath}");
        if (VerifierSettings.ScrubUserProfileEnabled)
        {
            Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "{UserProfile}");
        }

        return new(list.OrderByDescending(candidate => candidate.Item1.Length).ToList());
    }

    static bool SamePath(string a, string b) =>
        a.Length == b.Length && IndexOfPath(a, b, 0) == 0;

    public string Replace(string text)
    {
        var segments = new List<(string Text, bool Frozen)> { (text, false) };
        foreach (var (path, token) in candidates)
        {
            var next = new List<(string Text, bool Frozen)>();
            foreach (var segment in segments)
            {
                if (segment.Frozen)
                {
                    next.Add(segment);
                    continue;
                }

                var value = segment.Text;
                var position = 0;
                var search = 0;
                int index;
                while ((index = IndexOfPath(value, path, search)) >= 0)
                {
                    var end = index + path.Length;
                    if (ReplaceScrubber.IsWordChar(value, index - 1) || ReplaceScrubber.IsWordChar(value, end))
                    {
                        search = index + 1;
                        continue;
                    }

                    if (end < value.Length && value[end] is '/' or '\\')
                    {
                        end++;
                    }

                    if (index > position)
                    {
                        next.Add((value.Substring(position, index - position), false));
                    }

                    next.Add((token, true));
                    position = search = end;
                }

                if (position < value.Length)
                {
                    next.Add((value.Substring(position), false));
                }
            }

            segments = next;
        }

        return string.Concat(segments.Select(segment => segment.Text));
    }

    // Ordinal, except that / and \ are the same character.
    static int IndexOfPath(string text, string path, int start)
    {
        for (var index = start; index <= text.Length - path.Length; index++)
        {
            var match = true;
            for (var offset = 0; offset < path.Length; offset++)
            {
                var a = text[index + offset];
                var b = path[offset];
                if (a == b || (a is '/' or '\\' && b is '/' or '\\'))
                {
                    continue;
                }

                match = false;
                break;
            }

            if (match)
            {
                return index;
            }
        }

        return -1;
    }
}

static class Newlines
{
    public static string Normalize(string text) =>
        text.IndexOf('\r') < 0 ? text : text.Replace("\r\n", "\n").Replace('\r', '\n');
}
