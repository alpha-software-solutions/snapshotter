using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace AlphaSoftware.Snapshotter;

/// <summary>
/// Where <c>Verify</c> was called from: what the compiler recorded, and what the stack and the test framework
/// (if any) add.
/// </summary>
sealed class CallSite
{
    public CallSite(string sourceFile, string memberName, Assembly assembly)
    {
        SourceFile = sourceFile;
        MemberName = memberName;
        Assembly = assembly;
        var located = TestMethodLocator.Find(memberName);
        Type = located?.Type;
        Method = located?.Method;
        FrameworkArguments = TestFrameworks.CurrentArguments(Method);
    }

    public string SourceFile { get; }
    public string MemberName { get; }
    public Assembly Assembly { get; }
    public Type? Type { get; }
    public MethodBase? Method { get; }
    public object?[]? FrameworkArguments { get; }

    public string Description => $"{TypeName}.{MethodName}";

    /// <summary>
    /// The test class: <c>Outer.Inner</c> for nested classes. Without a type from the stack, the source file name
    /// up to its first dot.
    /// </summary>
    public string TypeName =>
        Type == null
            ? Naming.TypeNameFromFile(SourceFile)
            : Type.IsNested && Type.DeclaringType != null ? $"{Type.DeclaringType.Name}.{Type.Name}" : Type.Name;

    public string MethodName => Naming.CleanMemberName(Method?.Name ?? MemberName);
}

/// <summary>
/// The directory and file name stems of a verification's snapshots.
/// </summary>
sealed class SnapshotPaths(string directory, string verifiedStem, string receivedStem, bool uniqueDirectory, string typeName, string methodName)
{
    public string Directory { get; } = directory;
    public string VerifiedStem { get; } = verifiedStem;
    public string ReceivedStem { get; } = receivedStem;
    public bool UniqueDirectory { get; } = uniqueDirectory;
    public string TypeName { get; } = typeName;
    public string MethodName { get; } = methodName;
}

static class Naming
{
    public static SnapshotPaths Resolve(EffectiveSettings settings, CallSite site)
    {
        var instance = settings.Instance;
        var info = ProjectInfo.For(site.Assembly);

        var derived = default(PathInfo);
        if (VerifierSettings.PathInfoDeriver is { } derive && site.Type != null && site.Method is MethodInfo method)
        {
            derived = derive(site.SourceFile, info.ProjectDirectory ?? "", site.Type, method);
        }

        var typeName = instance.TypeName ?? derived.TypeName ?? site.TypeName;
        var methodName = instance.MethodName ?? derived.MethodName ?? site.MethodName;
        var directory = ResolveDirectory(instance.Directory ?? derived.Directory, site.SourceFile);

        var uniqueness = UniquenessText(settings.Unique, site.Assembly);
        // Every framework of a multi-targeted project shares one verified file; received files say which one ran.
        var receivedUniqueness = info.MultiTarget
            ? UniquenessText(settings.Unique | Snapshotter.Uniqueness.RuntimeAndVersion, site.Assembly)
            : uniqueness;
        string verifiedStem;
        string receivedStem;
        if (instance.FileName != null)
        {
            verifiedStem = receivedStem = instance.FileName;
        }
        else
        {
            var prefix = typeName.Length == 0 ? methodName : $"{typeName}.{methodName}";
            var (verifiedParameters, receivedParameters) = ParameterText(settings, site);
            verifiedStem = prefix + verifiedParameters;
            receivedStem = prefix + receivedParameters;
        }

        verifiedStem += uniqueness;
        receivedStem += receivedUniqueness;

        if (instance.UniqueDirectory)
        {
            var receivedSuffix = receivedUniqueness.Length > uniqueness.Length && receivedUniqueness.StartsWith(uniqueness, StringComparison.Ordinal)
                ? receivedUniqueness.Substring(uniqueness.Length)
                : "";
            return new(Path.Combine(directory, verifiedStem), "", receivedSuffix, true, typeName, methodName);
        }

        return new(directory, verifiedStem, receivedStem, false, typeName, methodName);
    }

    static string ResolveDirectory(string? configured, string sourceFile)
    {
        if (configured != null && Path.IsPathRooted(configured))
        {
            return Path.GetFullPath(configured);
        }

        var sourceDirectory = string.IsNullOrEmpty(sourceFile) ? null : Path.GetDirectoryName(sourceFile);
        if (sourceDirectory == null || !System.IO.Directory.Exists(sourceDirectory))
        {
            throw new Exception(
                $"""
                 The test source path recorded by the compiler, '{sourceFile}', does not exist on this machine.
                 Snapshots are stored next to the test source file, so tests have to run where they were built.
                 If the build maps source paths (deterministic builds, ContinuousIntegrationBuild), set
                 <DeterministicSourcePaths>false</DeterministicSourcePaths>, which the AlphaSoftware.Snapshotter package
                 does for projects that reference it. Or store snapshots elsewhere with an absolute UseDirectory(...).
                 """);
        }

        return configured == null ? sourceDirectory : Path.GetFullPath(Path.Combine(sourceDirectory, configured));
    }

    static (string Verified, string Received) ParameterText(EffectiveSettings settings, CallSite site)
    {
        var instance = settings.Instance;
        if (instance.ParametersText != null)
        {
            var text = "_" + instance.ParametersText;
            return (instance.IgnoreAllParametersForVerified ? "" : text, text);
        }

        var values = instance.Parameters ?? site.FrameworkArguments;
        var parameters = site.Method?.GetParameters() ?? [];
        if (values == null || values.Length == 0)
        {
            return ("", "");
        }

        if (site.Method == null)
        {
            throw new Exception(
                $"The test method that called Verify could not be found, so the values passed to UseParameters(...) cannot be named. Name the snapshot with UseTextForParameters(...) instead. Called from {site.MemberName} in {site.SourceFile}.");
        }

        if (parameters.Length == 0)
        {
            return ("", "");
        }

        if (values.Length > parameters.Length)
        {
            throw new Exception($"The number of passed in parameters ({values.Length}) must not exceed the number of parameters for the method ({parameters.Length}).");
        }

        var ignored = settings.IgnoredParameters;
        var ignoreAll = instance.IgnoreAllParametersForVerified || ignored?.Contains("*") == true;
        var verified = new StringBuilder();
        var received = new StringBuilder();
        for (var index = 0; index < values.Length; index++)
        {
            var name = parameters[index].Name ?? $"arg{index}";
            var part = $"_{name}={ParameterValue(values[index], settings.Named, nested: false)}";
            received.Append(part);
            if (!ignoreAll && ignored?.Contains(name) != true)
            {
                verified.Append(part);
            }
        }

        return (verified.ToString(), received.ToString());
    }

    internal static string ParameterValue(object? value, NamedValues named, bool nested)
    {
        if (value == null)
        {
            return "null";
        }

        if (named.TryGet(value, out var name))
        {
            return name;
        }

        foreach (var (type, convert) in VerifierSettings.ParameterNames)
        {
            if (type == value.GetType())
            {
                return convert(value);
            }
        }

        foreach (var (type, convert) in VerifierSettings.ParameterNames)
        {
            if (type.IsInstanceOfType(value))
            {
                return convert(value);
            }
        }

        switch (value)
        {
            case string text:
                return Clean(text);
            case bool boolean:
                return boolean ? "True" : "False";
            case Guid guid:
                return guid.ToString("D");
            case DateTime dateTime:
                return Formatting.Date(dateTime, "T", "-") + dateTime.Kind switch
                {
                    DateTimeKind.Utc => "Utc",
                    DateTimeKind.Local => "Local",
                    _ => ""
                };
            case DateTimeOffset offset:
                return Formatting.Date(offset.DateTime, "T", "-") + Formatting.Offset(offset.Offset);
#if NET6_0_OR_GREATER
            case DateOnly date:
                return Formatting.Date(date);
            case TimeOnly time:
                return time.ToString("h-mm-tt", CultureInfo.InvariantCulture);
#endif
        }

        var type1 = value.GetType();
        if (type1.IsGenericType && type1.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
        {
            var key = type1.GetProperty("Key")!.GetValue(value);
            var item = type1.GetProperty("Value")!.GetValue(value);
            return $"{ParameterValue(key, named, true)}={ParameterValue(item, named, true)}";
        }

        if (value is IDictionary dictionary)
        {
            var entries = new List<string>();
            foreach (DictionaryEntry entry in dictionary)
            {
                entries.Add($"{ParameterValue(entry.Key, named, true)}={ParameterValue(entry.Value, named, true)}");
            }

            return Wrap(entries, nested);
        }

        if (value is IEnumerable enumerable)
        {
            var items = enumerable.Cast<object?>().Select(item => ParameterValue(item, named, true)).ToList();
            return Wrap(items, nested);
        }

        if (value is IFormattable formattable)
        {
            return Clean(formattable.ToString(null, CultureInfo.InvariantCulture));
        }

        var description = value.ToString() ?? throw new Exception($"{value.GetType().FullName} returned a null for `ToString()`.");
        return Clean(description);
    }

    static string Wrap(List<string> items, bool nested)
    {
        if (items.Count == 0)
        {
            return "[]";
        }

        var joined = string.Join(",", items);
        return nested ? $"[{joined}]" : joined;
    }

    /// <summary>
    /// Replaces characters that can't be in a file name, and <c>#</c>, with <c>-</c>.
    /// </summary>
    internal static string Clean(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(character is '"' or '\\' or '/' or '<' or '>' or '|' or ':' or '*' or '?' or '#' || character < ' ' ? '-' : character);
        }

        return builder.ToString();
    }

    /// <summary>
    /// The source file name up to its first dot: <c>OrderTests.cs</c> and <c>OrderTests.Part.cs</c> both give
    /// <c>OrderTests</c>.
    /// </summary>
    internal static string TypeNameFromFile(string sourceFile)
    {
        var fileName = sourceFile.Substring(sourceFile.LastIndexOfAny(['/', '\\']) + 1);
        var dot = fileName.IndexOf('.');
        return dot > 0 ? fileName.Substring(0, dot) : fileName;
    }

    /// <summary>
    /// Top level statements report <c>&lt;Main&gt;$</c> and constructors <c>.ctor</c>.
    /// </summary>
    internal static string CleanMemberName(string memberName)
    {
        if (string.IsNullOrWhiteSpace(memberName))
        {
            throw new Exception("The calling member name is empty. Name the snapshot with UseMethodName(...) or UseFileName(...).");
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

    static string UniquenessText(Uniqueness unique, Assembly assembly)
    {
        var builder = new StringBuilder();

        void Add(string segment)
        {
            var text = "." + segment;
            if (builder.ToString().IndexOf(text, StringComparison.Ordinal) < 0)
            {
                builder.Append(text);
            }
        }

        if (unique.HasFlag(Snapshotter.Uniqueness.TargetFrameworkAndVersion))
        {
            Add(TargetFramework(assembly, withVersion: true));
        }
        else if (unique.HasFlag(Snapshotter.Uniqueness.TargetFramework))
        {
            Add(TargetFramework(assembly, withVersion: false));
        }

        if (unique.HasFlag(Snapshotter.Uniqueness.AssemblyConfiguration))
        {
            Add(assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration
                ?? throw new Exception($"UniqueForAssemblyConfiguration needs an AssemblyConfigurationAttribute on {assembly.GetName().Name}."));
        }

        if (unique.HasFlag(Snapshotter.Uniqueness.Architecture))
        {
            Add(RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant());
        }

        if (unique.HasFlag(Snapshotter.Uniqueness.OSPlatform))
        {
            Add(OperatingSystem());
        }

        if (unique.HasFlag(Snapshotter.Uniqueness.RuntimeAndVersion))
        {
            Add(RuntimeAndVersion());
        }
        else if (unique.HasFlag(Snapshotter.Uniqueness.Runtime))
        {
            Add(Runtime());
        }

        return builder.ToString();
    }

    static string OperatingSystem()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "Windows";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Linux";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "OSX";
        }

        throw new Exception($"UniqueForOSPlatform does not know this operating system: {RuntimeInformation.OSDescription}.");
    }

    internal static string Runtime()
    {
        var description = RuntimeInformation.FrameworkDescription;
        if (description.StartsWith(".NET Framework", StringComparison.Ordinal))
        {
            return "Net";
        }

        return description.StartsWith("Mono", StringComparison.Ordinal) ? "Mono" : "DotNet";
    }

    internal static string RuntimeAndVersion()
    {
        var runtime = Runtime();
        Version version;
        if (runtime == "Net")
        {
            // ".NET Framework 4.8.9310.0"
            var parts = RuntimeInformation.FrameworkDescription.Substring(".NET Framework".Length).Trim().Split('.');
            version = new(int.Parse(parts[0], CultureInfo.InvariantCulture), parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0);
        }
        else
        {
            version = Environment.Version;
        }

        return $"{runtime}{version.Major}_{version.Minor}";
    }

    static string TargetFramework(Assembly assembly, bool withVersion)
    {
        var name = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName
                   ?? throw new Exception($"{assembly.GetName().Name} has no TargetFrameworkAttribute.");
        // ".NETCoreApp,Version=v10.0", ".NETFramework,Version=v4.8", ".NETStandard,Version=v2.0"
        var runtime = name.StartsWith(".NETFramework", StringComparison.Ordinal) ? "Net" : "DotNet";
        if (!withVersion)
        {
            return runtime;
        }

        var index = name.IndexOf("Version=v", StringComparison.Ordinal);
        var version = index < 0 ? new Version(0, 0) : new Version(name.Substring(index + "Version=v".Length));
        return $"{runtime}{version.Major}_{version.Minor}";
    }
}
