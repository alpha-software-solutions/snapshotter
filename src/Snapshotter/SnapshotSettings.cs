using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Snapshotter;

/// <summary>
/// Controls how a snapshot is named, serialized and scrubbed.
/// </summary>
/// <remarks>
/// <see cref="Global" /> holds the defaults. Every verification starts from a copy of it, so set global
/// options once, before any test runs (a module initializer is a good place), and per call options on
/// the value <c>Verify(...)</c> returns.
/// </remarks>
public sealed class SnapshotSettings
{
    /// <summary>
    /// The defaults every verification starts from.
    /// </summary>
    public static SnapshotSettings Global { get; } = new();

    /// <summary>
    /// The directory snapshots are stored in. Relative paths are resolved against the directory of the
    /// test source file. Defaults to that directory.
    /// </summary>
    public string? Directory { get; set; }

    /// <summary>
    /// The whole snapshot name, replacing <c>{type}.{method}</c> and any parameters.
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Replaces the type part of the name, which defaults to the source file name up to its first dot.
    /// </summary>
    public string? TypeName { get; set; }

    /// <summary>
    /// Replaces the method part of the name, which defaults to the calling member.
    /// </summary>
    public string? MethodName { get; set; }

    /// <summary>
    /// Appended to the name as <c>_{text}</c>. Takes precedence over <see cref="Parameters" />.
    /// </summary>
    public string? ParametersText { get; set; }

    /// <summary>
    /// The arguments of a parameterized test, appended to the name as <c>_{name}={value}</c>.
    /// The names are read from the test method.
    /// </summary>
    public object?[]? Parameters { get; set; }

    /// <summary>
    /// The file extension of text snapshots made from strings. Defaults to <c>txt</c>.
    /// </summary>
    public string? Extension { get; set; }

    /// <summary>
    /// Replace GUID values with <c>Guid_1</c>, <c>Guid_2</c>, ... Defaults to true.
    /// </summary>
    public bool ScrubGuids { get; set; } = true;

    /// <summary>
    /// Replace date and time values with <c>DateTime_1</c>, <c>DateTime_2</c>, ... Defaults to true.
    /// </summary>
    public bool ScrubDates { get; set; } = true;

    /// <summary>
    /// Also replace GUIDs that appear inside larger strings and in text snapshots. Defaults to false.
    /// </summary>
    public bool ScrubInlineGuids { get; set; }

    /// <summary>
    /// Leave out members whose value is null. Defaults to false.
    /// </summary>
    public bool IgnoreNullValues { get; set; }

    /// <summary>
    /// Write the result straight to the verified file instead of failing. Also turned on by setting the
    /// <c>SNAPSHOTTER_ACCEPT</c> environment variable to <c>1</c> or <c>true</c>.
    /// </summary>
    public bool AutoAccept { get; set; }

    /// <summary>
    /// A command that opens a diff tool when a snapshot does not match, with <c>{received}</c> and
    /// <c>{verified}</c> placeholders, for example <c>code --diff "{received}" "{verified}"</c>.
    /// Also read from the <c>SNAPSHOTTER_DIFF_TOOL</c> environment variable. Never run on a build server.
    /// </summary>
    public string? DiffTool { get; set; }

    /// <summary>
    /// Functions applied, in order, to the final text of every text snapshot.
    /// </summary>
    public IList<Func<string, string>> Scrubbers { get; } = new List<Func<string, string>>();

    /// <summary>
    /// Member names left out of every object and JSON snapshot, whatever type declares them.
    /// </summary>
    public ISet<string> IgnoredMembers { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Converters added to the JSON serializer.
    /// </summary>
    public IList<JsonConverter> Converters { get; } = new List<JsonConverter>();

    internal List<KeyValuePair<Type, string>> IgnoredTypeMembers { get; } = [];

    /// <summary>
    /// Leave out the member with this name, on every type.
    /// </summary>
    public void IgnoreMember(string name) =>
        IgnoredMembers.Add(name);

    /// <summary>
    /// Leave out one member of <typeparamref name="T" /> and of types derived from it.
    /// </summary>
    public void IgnoreMember<T>(Expression<Func<T, object?>> member) =>
        IgnoredTypeMembers.Add(new(typeof(T), GetMemberName(member)));

    /// <summary>
    /// Add a function that rewrites the final text of text snapshots.
    /// </summary>
    public void AddScrubber(Func<string, string> scrubber) =>
        Scrubbers.Add(scrubber);

    /// <summary>
    /// Remove every line that contains any of <paramref name="values" /> (ordinal comparison).
    /// </summary>
    public void ScrubLinesContaining(params string[] values) =>
        ScrubLines(line => values.Any(value => line.IndexOf(value, StringComparison.Ordinal) >= 0));

    /// <summary>
    /// Remove every line for which <paramref name="remove" /> returns true.
    /// </summary>
    public void ScrubLines(Func<string, bool> remove) =>
        AddScrubber(text => string.Join("\n", text.Split('\n').Where(line => !remove(line))));

    /// <summary>
    /// A copy that can be changed without affecting this instance.
    /// </summary>
    public SnapshotSettings Clone()
    {
        var clone = new SnapshotSettings
        {
            Directory = Directory,
            FileName = FileName,
            TypeName = TypeName,
            MethodName = MethodName,
            ParametersText = ParametersText,
            Parameters = Parameters,
            Extension = Extension,
            ScrubGuids = ScrubGuids,
            ScrubDates = ScrubDates,
            ScrubInlineGuids = ScrubInlineGuids,
            IgnoreNullValues = IgnoreNullValues,
            AutoAccept = AutoAccept,
            DiffTool = DiffTool
        };
        foreach (var scrubber in Scrubbers)
        {
            clone.Scrubbers.Add(scrubber);
        }

        foreach (var member in IgnoredMembers)
        {
            clone.IgnoredMembers.Add(member);
        }

        foreach (var converter in Converters)
        {
            clone.Converters.Add(converter);
        }

        clone.IgnoredTypeMembers.AddRange(IgnoredTypeMembers);
        return clone;
    }

    static string GetMemberName(LambdaExpression expression)
    {
        var body = expression.Body;
        // Value type members are boxed to object, which adds a conversion around the member access.
        if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            body = unary.Operand;
        }

        if (body is MemberExpression { Member: PropertyInfo or FieldInfo } member)
        {
            return member.Member.Name;
        }

        throw new ArgumentException($"Expected a property or field access, such as `_ => _.Name`, but got `{expression}`.", nameof(expression));
    }
}
