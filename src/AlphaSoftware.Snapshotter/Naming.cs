using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace AlphaSoftware.Snapshotter;

/// <summary>
/// Where the verification was called from, as recorded by the compiler.
/// </summary>
sealed class CallSite(string sourceFile, string memberName, Assembly assembly)
{
    public string SourceFile { get; } = sourceFile;
    public string MemberName { get; } = memberName;
    public Assembly Assembly { get; } = assembly;
}

sealed class SnapshotLocation(string directory, string name)
{
    public string Directory { get; } = directory;
    public string Name { get; } = name;

    public string VerifiedPath(string extension) =>
        Path.Combine(Directory, $"{Name}.verified.{extension}");

    public string ReceivedPath(string extension) =>
        Path.Combine(Directory, $"{Name}.received.{extension}");
}

static class Naming
{
    static readonly char[] invalidFileNameChars = Path.GetInvalidFileNameChars();

    public static SnapshotLocation Resolve(SnapshotSettings settings, CallSite site)
    {
        var directory = ResolveDirectory(settings, site.SourceFile);
        var typeName = TypeNameFromFile(site.SourceFile);
        var methodName = CleanMemberName(site.MemberName);

        if (settings.FileName != null)
        {
            return new(directory, Sanitize(settings.FileName));
        }

        var name = new StringBuilder();
        name.Append(Sanitize(settings.TypeName ?? typeName));
        name.Append('.');
        name.Append(Sanitize(settings.MethodName ?? methodName));

        if (settings.ParametersText != null)
        {
            name.Append('_');
            name.Append(Sanitize(settings.ParametersText));
        }
        else if (settings.Parameters is { Length: > 0 } values)
        {
            // Looked up with the names from the call site, not any overrides: they identify the test method.
            var parameterNames = ParameterNames.Resolve(site.Assembly, typeName, methodName, values.Length);
            for (var index = 0; index < values.Length; index++)
            {
                name.Append('_');
                name.Append(parameterNames[index]);
                name.Append('=');
                name.Append(Sanitize(FormatValue(values[index])));
            }
        }

        return new(directory, name.ToString());
    }

    static string ResolveDirectory(SnapshotSettings settings, string sourceFile)
    {
        if (settings.Directory != null && Path.IsPathRooted(settings.Directory))
        {
            return settings.Directory;
        }

        var sourceDirectory = string.IsNullOrEmpty(sourceFile) ? null : Path.GetDirectoryName(sourceFile);
        if (sourceDirectory == null || !Directory.Exists(sourceDirectory))
        {
            throw new SnapshotterException(
                $"""
                 The test source path recorded by the compiler, '{sourceFile}', does not exist on this machine.
                 Snapshots are stored next to the test source file, so tests have to run where they were built.
                 If the build maps source paths (deterministic builds, ContinuousIntegrationBuild), set
                 <DeterministicSourcePaths>false</DeterministicSourcePaths>, which the Snapshotter package does for
                 projects that reference it directly. Or store snapshots elsewhere with an absolute UseDirectory(...).
                 """);
        }

        return settings.Directory == null
            ? sourceDirectory
            : Path.GetFullPath(Path.Combine(sourceDirectory, settings.Directory));
    }

    /// <summary>
    /// The source file name up to its first dot: <c>OrderTests.cs</c> and <c>OrderTests.Part.cs</c> both
    /// give <c>OrderTests</c>.
    /// </summary>
    internal static string TypeNameFromFile(string sourceFile)
    {
        // Paths recorded on another OS keep their own separators, so split on both.
        var fileName = sourceFile.Substring(sourceFile.LastIndexOfAny(['/', '\\']) + 1);
        var dot = fileName.IndexOf('.');
        return dot > 0 ? fileName.Substring(0, dot) : fileName;
    }

    /// <summary>
    /// Makes a compiler supplied member name usable in a file name: top level statements report
    /// <c>&lt;Main&gt;$</c> and constructors <c>.ctor</c>.
    /// </summary>
    internal static string CleanMemberName(string memberName)
    {
        if (string.IsNullOrWhiteSpace(memberName))
        {
            throw new SnapshotterException("The calling member name is empty. Name the snapshot with UseMethodName(...) or UseFileName(...).");
        }

        var open = memberName.IndexOf('<');
        if (open >= 0)
        {
            var close = memberName.IndexOf('>', open + 1);
            if (close > open + 1)
            {
                return memberName.Substring(open + 1, close - open - 1);
            }
        }

        return memberName.TrimStart('.');
    }

    internal static string FormatValue(object? value) =>
        value switch
        {
            null => "null",
            string text => text,
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            IEnumerable items => string.Join(",", items.Cast<object?>().Select(FormatValue)),
            _ => value.ToString() ?? ""
        };

    internal static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(Array.IndexOf(invalidFileNameChars, character) >= 0 || character is '/' or '\\' ? '-' : character);
        }

        return builder.ToString();
    }
}

static class ParameterNames
{
    const BindingFlags flags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>
    /// Without a test framework nothing says which parameters the test has, so they are read from the test
    /// method: a method named <paramref name="methodName" /> on a type named <paramref name="typeName" />.
    /// </summary>
    public static IReadOnlyList<string> Resolve(Assembly assembly, string typeName, string methodName, int valueCount)
    {
        var candidates = LoadableTypes(assembly)
            .Where(type => WithoutArity(type.Name) == typeName)
            .SelectMany(type => type.GetMethods(flags))
            .Where(method => method.Name == methodName)
            .Select(method => method.GetParameters().Select(parameter => parameter.Name ?? $"arg{parameter.Position}").ToArray())
            .Where(names => names.Length >= valueCount)
            .ToList();

        var exact = candidates.Where(names => names.Length == valueCount).ToList();
        if (exact.Count > 0)
        {
            candidates = exact;
        }

        var distinct = candidates
            .GroupBy(names => string.Join(",", names))
            .Select(group => group.First())
            .ToList();

        if (distinct.Count == 1)
        {
            return distinct[0];
        }

        var problem = distinct.Count == 0 ? "No method" : "More than one method";
        throw new SnapshotterException(
            $"""
             {problem} named '{typeName}.{methodName}' with {valueCount} or more parameters was found in '{assembly.GetName().Name}', so the values passed to UseParameters(...) cannot be named.
             The type is taken to be the source file name, and the method the calling member. Keep the test in a class named after its
             file, or name the snapshot with UseTextForParameters(...) instead.
             """);
    }

    static string WithoutArity(string name)
    {
        var tick = name.IndexOf('`');
        return tick > 0 ? name.Substring(0, tick) : name;
    }

    static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type != null)!;
        }
    }
}
