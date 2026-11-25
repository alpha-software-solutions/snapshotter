using System.Text;

namespace AlphaSoftware.Snapshotter;

/// <summary>
/// The text of a <see cref="VerifyException" />, in Verify's layout: the directory, the files by outcome, then their
/// content and a diff.
/// </summary>
static class Messages
{
    public static string Build(string directory, List<Comparison> comparisons, List<string> deleted)
    {
        var builder = new StringBuilder();
        builder.Append("Directory: ").Append(directory).Append('\n');

        var created = comparisons.Where(comparison => comparison.Outcome == Outcome.New).ToList();
        var notEqual = comparisons.Where(comparison => comparison.Outcome == Outcome.NotEqual).ToList();
        var equal = comparisons.Where(comparison => comparison.Outcome == Outcome.Equal).ToList();

        AppendPairs(builder, "New", created, directory);
        AppendPairs(builder, "NotEqual", notEqual, directory);
        if (deleted.Count > 0)
        {
            builder.Append("Delete:\n");
            foreach (var path in deleted)
            {
                builder.Append("  - ").Append(Relative(directory, path)).Append('\n');
            }
        }

        AppendPairs(builder, "Equal", equal, directory);

        var textNew = created.Where(comparison => comparison.Target.IsText).ToList();
        var contentNotEqual = notEqual.Where(comparison => comparison.Target.IsText || comparison.Message != null).ToList();
        if (VerifierSettings.OmitContent || (textNew.Count == 0 && contentNotEqual.Count == 0))
        {
            return builder.ToString().TrimEnd('\n');
        }

        builder.Append("\nFileContent:\n\n");
        if (textNew.Count > 0)
        {
            builder.Append("New:\n\n");
            foreach (var comparison in textNew)
            {
                builder.Append("Received: ").Append(Relative(directory, comparison.ReceivedPath)).Append('\n');
                builder.Append(comparison.Target.Text).Append("\n\n");
            }
        }

        if (contentNotEqual.Count > 0)
        {
            builder.Append("NotEqual:\n\n");
            foreach (var comparison in contentNotEqual)
            {
                builder.Append("Received: ").Append(Relative(directory, comparison.ReceivedPath)).Append('\n');
                builder.Append("Verified: ").Append(Relative(directory, comparison.VerifiedPath)).Append('\n');
                if (comparison.Message != null)
                {
                    builder.Append("Compare Result:\n").Append(comparison.Message).Append("\n\n");
                    continue;
                }

                var diff = TextDiff.Compact(comparison.VerifiedText ?? "", comparison.Target.Text ?? "");
                if (diff.Length == 0)
                {
                    builder.Append(comparison.Target.Text).Append('\n');
                    builder.Append("Verified: ").Append(Relative(directory, comparison.VerifiedPath)).Append('\n');
                    builder.Append(comparison.VerifiedText).Append("\n\n");
                    continue;
                }

                builder.Append("Diff:\n").Append(diff).Append("\n\n");
            }
        }

        return builder.ToString().TrimEnd('\n');
    }

    static void AppendPairs(StringBuilder builder, string title, List<Comparison> comparisons, string directory)
    {
        if (comparisons.Count == 0 || (title == "Equal" && comparisons.Count == 0))
        {
            return;
        }

        builder.Append(title).Append(":\n");
        foreach (var comparison in comparisons)
        {
            builder.Append("  - Received: ").Append(Relative(directory, comparison.ReceivedPath)).Append('\n');
            builder.Append("    Verified: ").Append(Relative(directory, comparison.VerifiedPath)).Append('\n');
        }
    }

    static string Relative(string directory, string path)
    {
        var prefix = directory.TrimEnd('/', '\\') + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? path.Substring(prefix.Length) : path;
    }
}

/// <summary>
/// A line diff from the verified text to the received text: changed lines as <c>- old</c> and <c>+ new</c>, the
/// unchanged lines next to a change with their line number, and <c>[BOF]</c> / <c>[EOF]</c> when a change touches
/// the start or the end.
/// </summary>
static class TextDiff
{
    enum Kind
    {
        Same,
        Removed,
        Added
    }

    public static string Compact(string verified, string received)
    {
        var oldLines = verified.Split('\n');
        var newLines = received.Split('\n');
        if (oldLines.LongLength * newLines.LongLength > 4_000_000)
        {
            return "";
        }

        var operations = Diff(oldLines, newLines);
        if (operations.All(operation => operation.Kind == Kind.Same))
        {
            return "";
        }

        // Unchanged lines adjacent to a change are shown as context.
        var shown = new bool[operations.Count];
        for (var index = 0; index < operations.Count; index++)
        {
            if (operations[index].Kind == Kind.Same)
            {
                continue;
            }

            shown[index] = true;
            if (index > 0)
            {
                shown[index - 1] = true;
            }

            if (index < operations.Count - 1)
            {
                shown[index + 1] = true;
            }
        }

        var builder = new StringBuilder();
        var previousShown = -1;
        for (var index = 0; index < operations.Count; index++)
        {
            if (!shown[index])
            {
                continue;
            }

            if (previousShown >= 0 && index > previousShown + 1)
            {
                builder.Append('\n');
            }

            if (index == 0 && operations[index].Kind != Kind.Same)
            {
                builder.Append("  [BOF]\n");
            }

            var (kind, text, number) = operations[index];
            builder.Append(kind switch
            {
                Kind.Removed => "- " + text,
                Kind.Added => "+ " + text,
                _ => $"{number} {text}"
            }).Append('\n');

            if (index == operations.Count - 1 && kind != Kind.Same)
            {
                builder.Append("  [EOF]\n");
            }

            previousShown = index;
        }

        return builder.ToString().TrimEnd('\n');
    }

    static List<(Kind Kind, string Text, int Number)> Diff(string[] oldLines, string[] newLines)
    {
        // Longest common subsequence, then walk it to produce removals before additions.
        var lengths = new int[oldLines.Length + 1, newLines.Length + 1];
        for (var i = oldLines.Length - 1; i >= 0; i--)
        {
            for (var j = newLines.Length - 1; j >= 0; j--)
            {
                lengths[i, j] = oldLines[i] == newLines[j]
                    ? lengths[i + 1, j + 1] + 1
                    : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
            }
        }

        var result = new List<(Kind, string, int)>();
        int a = 0, b = 0;
        while (a < oldLines.Length || b < newLines.Length)
        {
            if (a < oldLines.Length && b < newLines.Length && oldLines[a] == newLines[b])
            {
                result.Add((Kind.Same, newLines[b], b + 1));
                a++;
                b++;
            }
            else if (b >= newLines.Length || (a < oldLines.Length && lengths[a + 1, b] >= lengths[a, b + 1]))
            {
                result.Add((Kind.Removed, oldLines[a], a + 1));
                a++;
            }
            else
            {
                result.Add((Kind.Added, newLines[b], b + 1));
                b++;
            }
        }

        return result;
    }
}
