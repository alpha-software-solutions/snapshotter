using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace AlphaSoftware.Snapshotter;

[Flags]
enum Uniqueness
{
    None = 0,
    TargetFramework = 1,
    TargetFrameworkAndVersion = 2,
    AssemblyConfiguration = 4,
    Architecture = 8,
    OSPlatform = 16,
    Runtime = 32,
    RuntimeAndVersion = 64
}

/// <summary>
/// The settings of one verification: how its snapshot is named, serialized, scrubbed and compared.
/// </summary>
/// <remarks>
/// Pass an instance to <c>Verify(target, settings)</c>, or use the same methods on the value <c>Verify</c> returns.
/// Defaults for every verification are set on <see cref="VerifierSettings" />.
/// </remarks>
public sealed class VerifySettings
{
    public VerifySettings()
    {
    }

    /// <summary>
    /// A copy of <paramref name="settings" />, for example class level settings a test adds to.
    /// </summary>
    public VerifySettings(VerifySettings? settings)
    {
        if (settings == null)
        {
            return;
        }

        StrictJson = settings.StrictJson;
        IgnoreEmptyCollections = settings.IgnoreEmptyCollections;
        SortDictionaries = settings.SortDictionaries;
        IncludeObsoletesValue = settings.IncludeObsoletesValue;
        ScrubGuidsValue = settings.ScrubGuidsValue;
        ScrubDateTimesValue = settings.ScrubDateTimesValue;
        ScrubNumericIdsValue = settings.ScrubNumericIdsValue;
        DateCounting = settings.DateCounting;
        RequireUniquePrefix = settings.RequireUniquePrefix;
        ExtraSettings.AddRange(settings.ExtraSettings);
        Members = settings.Members.Clone();
        Scrubbers = settings.Scrubbers.Clone();
        foreach (var pair in settings.ExtensionScrubbers)
        {
            ExtensionScrubbers[pair.Key] = pair.Value.Clone();
        }

        Named = settings.Named.Clone();
        Unique = settings.Unique;
        IgnoredParameters = settings.IgnoredParameters == null ? null : new(settings.IgnoredParameters, StringComparer.Ordinal);
        FirstVerifyHandlers.AddRange(settings.FirstVerifyHandlers);
        MismatchHandlers.AddRange(settings.MismatchHandlers);
        DeleteHandlers.AddRange(settings.DeleteHandlers);
        VerifyHandlers.AddRange(settings.VerifyHandlers);

        Directory = settings.Directory;
        TypeName = settings.TypeName;
        MethodName = settings.MethodName;
        FileName = settings.FileName;
        Parameters = settings.Parameters;
        ParametersText = settings.ParametersText;
        IgnoreAllParametersForVerified = settings.IgnoreAllParametersForVerified;
        UniqueDirectory = settings.UniqueDirectory;
        DiffEnabled = settings.DiffEnabled;
        ScrubbersDisabled = settings.ScrubbersDisabled;
        AutoVerifyRule = settings.AutoVerifyRule;
        AutoVerifyThrows = settings.AutoVerifyThrows;
        StringComparers.AddRange(settings.StringComparers);
        StreamComparers.AddRange(settings.StreamComparers);
        AppendedValues.AddRange(settings.AppendedValues);
        AppendedFiles.AddRange(settings.AppendedFiles);
        foreach (var pair in settings.Context)
        {
            Context[pair.Key] = pair.Value;
        }
    }

    // Shared with VerifierSettings, which keeps its global state in an instance of this class.
    internal bool? StrictJson;
    internal bool? IgnoreEmptyCollections;
    internal bool? SortDictionaries;
    internal bool? IncludeObsoletesValue;
    internal bool? ScrubGuidsValue;
    internal bool? ScrubDateTimesValue;
    internal bool? ScrubNumericIdsValue;
    internal bool? DateCounting;
    internal bool? RequireUniquePrefix;
    internal List<Action<SerializerSettings>> ExtraSettings { get; } = [];
    internal MemberRules Members { get; private set; } = new();
    internal ScrubberSet Scrubbers { get; private set; } = new();
    internal Dictionary<string, ScrubberSet> ExtensionScrubbers { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal NamedValues Named { get; private set; } = new();
    internal Uniqueness Unique;
    internal HashSet<string>? IgnoredParameters;
    internal List<FirstVerify> FirstVerifyHandlers { get; } = [];
    internal List<VerifyMismatch> MismatchHandlers { get; } = [];
    internal List<VerifyDelete> DeleteHandlers { get; } = [];
    internal List<(Action Before, Action After)> VerifyHandlers { get; } = [];

    // Only meaningful per verification.
    internal string? Directory;
    internal string? TypeName;
    internal string? MethodName;
    internal string? FileName;
    internal object?[]? Parameters;
    internal string? ParametersText;
    internal bool IgnoreAllParametersForVerified;
    internal bool UniqueDirectory;
    internal bool DiffEnabled = true;
    internal bool ScrubbersDisabled;
    internal AutoVerify? AutoVerifyRule;
    internal bool AutoVerifyThrows;
    internal List<(string? Extension, StringCompare Compare)> StringComparers { get; } = [];
    internal List<(string? Extension, StreamCompare Compare)> StreamComparers { get; } = [];
    internal List<KeyValuePair<string, object?>> AppendedValues { get; } = [];
    internal List<Target> AppendedFiles { get; } = [];

    /// <summary>
    /// Values for scrubbers, comparers and converters of this verification.
    /// </summary>
    public Dictionary<string, object> Context { get; } = new();

    internal ScrubberSet ScrubbersFor(string? extension)
    {
        if (extension == null)
        {
            return Scrubbers;
        }

        if (!ExtensionScrubbers.TryGetValue(extension, out var set))
        {
            ExtensionScrubbers[extension] = set = new();
        }

        return set;
    }

    #region Naming

    /// <summary>
    /// Store snapshots in <paramref name="directory" />. A relative path is relative to the test source file's
    /// directory.
    /// </summary>
    public void UseDirectory(string directory)
    {
        ValidatePath(directory);
        Directory = directory;
    }

    /// <summary>
    /// Replace <c>{TypeName}.{MethodName}{_Parameters}</c> with <paramref name="fileName" />.
    /// </summary>
    public void UseFileName(string fileName)
    {
        ValidateFileName(fileName);
        if (TypeName != null || MethodName != null)
        {
            throw new Exception("UseFileName is not compatible with UseMethodName or UseTypeName.");
        }

        FileName = fileName;
    }

    /// <summary>
    /// Replace the type part of the snapshot name.
    /// </summary>
    public void UseTypeName(string name)
    {
        ValidateFileName(name);
        if (FileName != null)
        {
            throw new Exception("UseTypeName is not compatible with UseFileName.");
        }

        TypeName = name;
    }

    /// <summary>
    /// Replace the method part of the snapshot name. Use it to verify more than once in one test.
    /// </summary>
    public void UseMethodName(string name)
    {
        ValidateFileName(name);
        if (FileName != null)
        {
            throw new Exception("UseMethodName is not compatible with UseFileName.");
        }

        MethodName = name;
    }

    /// <summary>
    /// Store each target as a file in a directory named after the test.
    /// </summary>
    public void UseUniqueDirectory() => UniqueDirectory = true;

    /// <summary>
    /// The arguments of a parameterized test, added to the name as <c>_{name}={value}</c>. The names come from the
    /// test method. With NUnit, xUnit v3 and TUnit the arguments are found without this call.
    /// </summary>
    public void UseParameters(params object?[] parameters)
    {
        if (ParametersText != null)
        {
            throw new Exception("UseParameters is not compatible with UseTextForParameters.");
        }

        if (FileName != null)
        {
            throw new Exception("UseParameters is not compatible with UseFileName.");
        }

        Parameters = parameters;
    }

    /// <summary>
    /// Use <paramref name="parametersText" /> as the parameter part of the name, instead of argument values.
    /// </summary>
    public void UseTextForParameters(string parametersText)
    {
        ValidateFileName(parametersText);
        if (Parameters != null)
        {
            throw new Exception("UseTextForParameters is not compatible with UseParameters.");
        }

        ParametersText = parametersText;
    }

    /// <summary>
    /// Leave the named parameters (all of them when none are named) out of the verified file name, so all cases
    /// share one verified file. Received files keep every parameter.
    /// </summary>
    public void IgnoreParameters(params string[] parameterNames)
    {
        IgnoredParameters ??= new(StringComparer.Ordinal);
        if (parameterNames.Length == 0)
        {
            IgnoredParameters.Clear();
            IgnoredParameters.Add("*");
            return;
        }

        foreach (var name in parameterNames)
        {
            IgnoredParameters.Add(name);
        }
    }

    /// <summary>
    /// Uses <paramref name="parameters" /> (if any) and leaves all parameters out of the verified file name.
    /// </summary>
    public void IgnoreParametersForVerified(params object?[] parameters)
    {
        if (parameters.Length > 0)
        {
            UseParameters(parameters);
        }

        IgnoreAllParametersForVerified = true;
    }

    /// <summary>
    /// A separate snapshot per runtime: <c>.DotNet</c>, <c>.Net</c> (.NET Framework) or <c>.Mono</c>.
    /// </summary>
    public void UniqueForRuntime() => Unique |= Uniqueness.Runtime;

    /// <summary>
    /// A separate snapshot per runtime and version, such as <c>.DotNet10_0</c> or <c>.Net4_8</c>.
    /// </summary>
    public void UniqueForRuntimeAndVersion() => Unique |= Uniqueness.RuntimeAndVersion;

    /// <summary>
    /// A separate snapshot per target framework of the test assembly: <c>.DotNet</c> or <c>.Net</c>.
    /// </summary>
    public void UniqueForTargetFramework() => Unique |= Uniqueness.TargetFramework;

    /// <summary>
    /// A separate snapshot per target framework and version of the test assembly, such as <c>.DotNet10_0</c>.
    /// </summary>
    public void UniqueForTargetFrameworkAndVersion() => Unique |= Uniqueness.TargetFrameworkAndVersion;

    /// <summary>
    /// A separate snapshot per build configuration: <c>.Debug</c> or <c>.Release</c>.
    /// </summary>
    public void UniqueForAssemblyConfiguration() => Unique |= Uniqueness.AssemblyConfiguration;

    /// <summary>
    /// A separate snapshot per process architecture: <c>.x64</c>, <c>.x86</c>, <c>.arm64</c>.
    /// </summary>
    public void UniqueForArchitecture() => Unique |= Uniqueness.Architecture;

    /// <summary>
    /// A separate snapshot per operating system: <c>.Windows</c>, <c>.Linux</c> or <c>.OSX</c>.
    /// </summary>
    public void UniqueForOSPlatform() => Unique |= Uniqueness.OSPlatform;

    /// <summary>
    /// Allow two verifications in one run to use the same file names.
    /// </summary>
    public void DisableRequireUniquePrefix() => RequireUniquePrefix = false;

    #endregion

    #region Serialization

    /// <summary>
    /// Write objects as real JSON, with quoted names and strings, in <c>.json</c> files.
    /// </summary>
    public void UseStrictJson() => StrictJson = true;

    /// <summary>
    /// Change the serializer settings: add converters, or keep default and null values.
    /// </summary>
    public void AddExtraSettings(Action<SerializerSettings> action) => ExtraSettings.Add(action);

    /// <summary>
    /// Write empty collections as <c>[]</c> instead of leaving them out.
    /// </summary>
    public void DontIgnoreEmptyCollections() => IgnoreEmptyCollections = false;

    /// <summary>
    /// Keep dictionaries in their own order instead of sorting them by key.
    /// </summary>
    public void DontSortDictionaries() => SortDictionaries = false;

    /// <summary>
    /// Also write members marked <c>[Obsolete]</c>.
    /// </summary>
    public void IncludeObsoletes() => IncludeObsoletesValue = true;

    /// <summary>
    /// Leave the stack trace out of exceptions.
    /// </summary>
    public void IgnoreStackTrace() => IgnoreMember("StackTrace");

    /// <summary>
    /// Leave out members whose getter throws <typeparamref name="T" />. <see cref="NotImplementedException" /> and
    /// <see cref="NotSupportedException" /> are always left out.
    /// </summary>
    public void IgnoreMembersThatThrow<T>()
        where T : Exception =>
        Members.IgnoredThrows.Add(exception => exception is T);

    /// <summary>
    /// Leave out members whose getter throws an exception <paramref name="item" /> matches.
    /// </summary>
    public void IgnoreMembersThatThrow(Func<Exception, bool> item) => Members.IgnoredThrows.Add(item);

    /// <summary>
    /// Leave out members whose getter throws a <typeparamref name="T" /> that <paramref name="item" /> matches.
    /// </summary>
    public void IgnoreMembersThatThrow<T>(Func<T, bool> item)
        where T : Exception =>
        Members.IgnoredThrows.Add(exception => exception is T typed && item(typed));

    /// <summary>
    /// Leave out members with this name, on every type. Also applies to dictionary keys and JSON properties.
    /// </summary>
    public void IgnoreMember(string name) => Members.IgnoredNames.Add(name);

    /// <summary>
    /// Leave out members with these names, on every type.
    /// </summary>
    public void IgnoreMembers(params string[] names)
    {
        foreach (var name in names)
        {
            IgnoreMember(name);
        }
    }

    /// <summary>
    /// Leave out the member <paramref name="name" /> of <paramref name="declaringType" /> and derived types.
    /// </summary>
    public void IgnoreMember(Type declaringType, string name) => Members.IgnoredTypeMembers.Add((declaringType, name));

    /// <summary>
    /// Leave out these members of <paramref name="declaringType" />.
    /// </summary>
    public void IgnoreMembers(Type declaringType, params string[] names)
    {
        foreach (var name in names)
        {
            IgnoreMember(declaringType, name);
        }
    }

    /// <summary>
    /// Leave out the member <paramref name="name" /> of <typeparamref name="T" />.
    /// </summary>
    public void IgnoreMember<T>(string name) => IgnoreMember(typeof(T), name);

    /// <summary>
    /// Leave out these members of <typeparamref name="T" />.
    /// </summary>
    public void IgnoreMembers<T>(params string[] names) => IgnoreMembers(typeof(T), names);

    /// <summary>
    /// Leave out one member of <typeparamref name="T" />: <c>IgnoreMember&lt;User&gt;(_ =&gt; _.Password)</c>.
    /// </summary>
    public void IgnoreMember<T>(Expression<Func<T, object?>> expression) =>
        IgnoreMember(typeof(T), MemberRules.MemberName(expression));

    /// <summary>
    /// Leave out these members of <typeparamref name="T" />.
    /// </summary>
    public void IgnoreMembers<T>(params Expression<Func<T, object?>>[] expressions)
    {
        foreach (var expression in expressions)
        {
            IgnoreMember(expression);
        }
    }

    /// <summary>
    /// Leave out members whose name <paramref name="predicate" /> matches. Also applies to dictionary keys.
    /// </summary>
    public void IgnoreMembers(Func<string, bool> predicate) => Members.IgnoredNamePredicates.Add(predicate);

    /// <summary>
    /// Leave out members <paramref name="predicate" /> matches.
    /// </summary>
    public void IgnoreMembers(Func<MemberInfo, bool> predicate) => Members.IgnoredMemberPredicates.Add(predicate);

    /// <summary>
    /// Leave out every member whose type is, derives from or implements <typeparamref name="T" />.
    /// </summary>
    public void IgnoreMembersWithType<T>() => IgnoreMembersWithType(typeof(T));

    /// <summary>
    /// Leave out every member whose type is, derives from or implements <paramref name="type" />. An open generic
    /// type matches its constructed forms.
    /// </summary>
    public void IgnoreMembersWithType(Type type) => Members.IgnoredTypes.Add(type);

    /// <summary>
    /// Write members with this name, on every type, as <c>{Scrubbed}</c>.
    /// </summary>
    public void ScrubMember(string name) => Members.ScrubbedNames.Add(name);

    /// <summary>
    /// Write members with these names as <c>{Scrubbed}</c>.
    /// </summary>
    public void ScrubMembers(params string[] names)
    {
        foreach (var name in names)
        {
            ScrubMember(name);
        }
    }

    /// <summary>
    /// Write the member <paramref name="name" /> of <paramref name="declaringType" /> as <c>{Scrubbed}</c>.
    /// </summary>
    public void ScrubMember(Type declaringType, string name) => Members.ScrubbedTypeMembers.Add((declaringType, name));

    /// <summary>
    /// Write these members of <paramref name="declaringType" /> as <c>{Scrubbed}</c>.
    /// </summary>
    public void ScrubMembers(Type declaringType, params string[] names)
    {
        foreach (var name in names)
        {
            ScrubMember(declaringType, name);
        }
    }

    /// <summary>
    /// Write the member <paramref name="name" /> of <typeparamref name="T" /> as <c>{Scrubbed}</c>.
    /// </summary>
    public void ScrubMember<T>(string name) => ScrubMember(typeof(T), name);

    /// <summary>
    /// Write these members of <typeparamref name="T" /> as <c>{Scrubbed}</c>.
    /// </summary>
    public void ScrubMembers<T>(params string[] names) => ScrubMembers(typeof(T), names);

    /// <summary>
    /// Write one member of <typeparamref name="T" /> as <c>{Scrubbed}</c>.
    /// </summary>
    public void ScrubMember<T>(Expression<Func<T, object?>> expression) =>
        ScrubMember(typeof(T), MemberRules.MemberName(expression));

    /// <summary>
    /// Write these members of <typeparamref name="T" /> as <c>{Scrubbed}</c>.
    /// </summary>
    public void ScrubMembers<T>(params Expression<Func<T, object?>>[] expressions)
    {
        foreach (var expression in expressions)
        {
            ScrubMember(expression);
        }
    }

    /// <summary>
    /// Write members whose name <paramref name="predicate" /> matches as <c>{Scrubbed}</c>.
    /// </summary>
    public void ScrubMembers(Func<string, bool> predicate) => Members.ScrubbedNamePredicates.Add(predicate);

    /// <summary>
    /// Write members <paramref name="predicate" /> matches as <c>{Scrubbed}</c>.
    /// </summary>
    public void ScrubMembers(Func<MemberInfo, bool> predicate) => Members.ScrubbedMemberPredicates.Add(predicate);

    /// <summary>
    /// Write every member of type <typeparamref name="T" /> as <c>{Scrubbed}</c>.
    /// </summary>
    public void ScrubMembersWithType<T>() => ScrubMembersWithType(typeof(T));

    /// <summary>
    /// Write every member of type <paramref name="type" /> as <c>{Scrubbed}</c>.
    /// </summary>
    public void ScrubMembersWithType(Type type) => Members.ScrubbedTypes.Add(type);

    /// <summary>
    /// Leave out values of <typeparamref name="T" /> that <paramref name="shouldIgnore" /> matches, wherever they
    /// appear.
    /// </summary>
    public void IgnoreInstance<T>(Func<T, bool> shouldIgnore) =>
        Members.IgnoredInstances.Add((typeof(T), value => shouldIgnore((T)value)));

    /// <summary>
    /// Write values of <typeparamref name="T" /> that <paramref name="shouldScrub" /> matches as <c>{Scrubbed}</c>.
    /// </summary>
    public void ScrubInstance<T>(Func<T, bool> shouldScrub) =>
        Members.ScrubbedInstances.Add((typeof(T), value => shouldScrub((T)value)));

    #endregion

    #region Scrubbing

    /// <summary>
    /// Rewrite the text of every text target.
    /// </summary>
    public void AddScrubber(Action<StringBuilder> scrubber, ScrubberLocation location = ScrubberLocation.First) =>
        Scrubbers.AddLegacy((builder, _, _) => scrubber(builder), location);

    /// <summary>
    /// Rewrite the text of every text target, with the verification's counter.
    /// </summary>
    public void AddScrubber(Action<StringBuilder, Counter> scrubber, ScrubberLocation location = ScrubberLocation.First) =>
        Scrubbers.AddLegacy((builder, counter, _) => scrubber(builder, counter), location);

    /// <summary>
    /// Rewrite the text of every text target, with the verification's counter and context.
    /// </summary>
    public void AddScrubber(Action<StringBuilder, Counter, IReadOnlyDictionary<string, object>> scrubber, ScrubberLocation location = ScrubberLocation.First) =>
        Scrubbers.AddLegacy(scrubber, location);

    /// <summary>
    /// Rewrite the text of targets with this extension.
    /// </summary>
    public void AddScrubber(string extension, Action<StringBuilder> scrubber, ScrubberLocation location = ScrubberLocation.First) =>
        ScrubbersFor(extension).AddLegacy((builder, _, _) => scrubber(builder), location);

    /// <summary>
    /// Rewrite the text of targets with this extension, with the verification's counter.
    /// </summary>
    public void AddScrubber(string extension, Action<StringBuilder, Counter> scrubber, ScrubberLocation location = ScrubberLocation.First) =>
        ScrubbersFor(extension).AddLegacy((builder, counter, _) => scrubber(builder, counter), location);

    /// <summary>
    /// Remove lines <paramref name="removeLine" /> matches.
    /// </summary>
    public void ScrubLines(Func<string, bool> removeLine) => Scrubbers.LineRemovals.Add(removeLine);

    /// <summary>
    /// Remove lines <paramref name="removeLine" /> matches, in targets with this extension.
    /// </summary>
    public void ScrubLines(string extension, Func<string, bool> removeLine) => ScrubbersFor(extension).LineRemovals.Add(removeLine);

    /// <summary>
    /// Remove lines that contain any of <paramref name="stringToMatch" />, ignoring case.
    /// </summary>
    public void ScrubLinesContaining(params string[] stringToMatch) =>
        ScrubLinesContaining(StringComparison.OrdinalIgnoreCase, stringToMatch);

    /// <summary>
    /// Remove lines that contain any of <paramref name="stringToMatch" />.
    /// </summary>
    public void ScrubLinesContaining(StringComparison comparison, params string[] stringToMatch)
    {
        if (comparison is not (StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only Ordinal and OrdinalIgnoreCase are supported.", nameof(comparison));
        }

        ScrubLines(line => stringToMatch.Any(value => line.IndexOf(value, comparison) >= 0));
    }

    /// <summary>
    /// Replace each line with what <paramref name="replaceLine" /> returns. Null removes the line.
    /// </summary>
    public void ScrubLinesWithReplace(Func<string, string?> replaceLine) => Scrubbers.LineReplaces.Add(replaceLine);

    /// <summary>
    /// Replace each line of targets with this extension. Null removes the line.
    /// </summary>
    public void ScrubLinesWithReplace(string extension, Func<string, string?> replaceLine) =>
        ScrubbersFor(extension).LineReplaces.Add(replaceLine);

    /// <summary>
    /// Remove empty and whitespace-only lines, and a final newline.
    /// </summary>
    public void ScrubEmptyLines() => Scrubbers.RemoveEmptyLines = true;

    /// <summary>
    /// Replace every occurrence of <paramref name="find" /> with <paramref name="replacement" />.
    /// </summary>
    public void ScrubReplace(string find, string replacement, StringComparison comparison = StringComparison.Ordinal, bool requireWordBoundary = false) =>
        Scrubbers.Inline.Add(new ReplaceScrubber(find, replacement, comparison, requireWordBoundary));

    /// <summary>
    /// Replace the machine name with <c>TheMachineName</c>.
    /// </summary>
    public void ScrubMachineName() => ScrubReplace(Environment.MachineName, "TheMachineName", requireWordBoundary: true);

    /// <summary>
    /// Replace the user name with <c>TheUserName</c>.
    /// </summary>
    public void ScrubUserName() => ScrubReplace(Environment.UserName, "TheUserName", requireWordBoundary: true);

    /// <summary>
    /// Also replace Guids inside longer strings and text (<c>Guid_1</c>, ...). A Guid must not touch a letter or
    /// digit on either side.
    /// </summary>
    public void ScrubInlineGuids(GuidFormats formats = GuidFormats.All)
    {
        if (ScrubGuidsValue == false)
        {
            throw new Exception("ScrubGuids is disabled. Call .ScrubGuids() before calling .ScrubInlineGuids().");
        }

        Scrubbers.Inline.Add(new GuidScrubber(formats));
    }

    /// <summary>
    /// Replace dates in this format inside text with <c>DateTime_1</c>, ...
    /// </summary>
    public void ScrubInlineDateTimes(string format, CultureInfo? culture = null) =>
        AddDateScrubber(format, culture, InlineDateKind.DateTime, nameof(ScrubInlineDateTimes));

    /// <summary>
    /// Replace dates in this format inside text with <c>DateTimeOffset_1</c>, ...
    /// </summary>
    public void ScrubInlineDateTimeOffsets(string format, CultureInfo? culture = null) =>
        AddDateScrubber(format, culture, InlineDateKind.DateTimeOffset, nameof(ScrubInlineDateTimeOffsets));

    /// <summary>
    /// Replace dates in this format inside text with <c>Date_1</c>, ...
    /// </summary>
    public void ScrubInlineDates(string format, CultureInfo? culture = null) =>
        AddDateScrubber(format, culture, InlineDateKind.Date, nameof(ScrubInlineDates));

    void AddDateScrubber(string format, CultureInfo? culture, InlineDateKind kind, string api)
    {
        if (ScrubDateTimesValue == false)
        {
            throw new Exception($"ScrubDateTimes is disabled. Call .ScrubDateTimes() before calling .{api}().");
        }

        Scrubbers.Inline.Add(new DateScrubber(format, culture, kind));
    }

    /// <summary>
    /// Replace Guids with <c>Guid_1</c>, ... (the default).
    /// </summary>
    public void ScrubGuids() => ScrubGuidsValue = true;

    /// <summary>
    /// Keep Guids as they are.
    /// </summary>
    public void DontScrubGuids() => ScrubGuidsValue = false;

    /// <summary>
    /// Replace dates and times with <c>DateTime_1</c>, ... (the default).
    /// </summary>
    public void ScrubDateTimes() => ScrubDateTimesValue = true;

    /// <summary>
    /// Keep dates and times as they are.
    /// </summary>
    public void DontScrubDateTimes() => ScrubDateTimesValue = false;

    /// <summary>
    /// Replace numeric members whose name ends with <c>Id</c> with <c>{Name}_1</c>, ...
    /// </summary>
    public void ScrubNumericIds() => ScrubNumericIdsValue = true;

    /// <summary>
    /// Keep numeric ids as they are (the default).
    /// </summary>
    public void DontScrubNumericIds() => ScrubNumericIdsValue = false;

    /// <summary>
    /// Write dates as <c>{Scrubbed}</c>, without a number.
    /// </summary>
    public void DisableDateCounting() => DateCounting = false;

    /// <summary>
    /// Write this Guid as <paramref name="name" /> wherever it appears.
    /// </summary>
    public void AddNamedGuid(Guid value, [CallerArgumentExpression(nameof(value))] string name = "") => Named.Add(value, name);

    /// <summary>
    /// Write this DateTime as <paramref name="name" /> wherever it appears.
    /// </summary>
    public void AddNamedDateTime(DateTime value, [CallerArgumentExpression(nameof(value))] string name = "") => Named.Add(value, name);

    /// <summary>
    /// Write this DateTimeOffset as <paramref name="name" /> wherever it appears.
    /// </summary>
    public void AddNamedDateTimeOffset(DateTimeOffset value, [CallerArgumentExpression(nameof(value))] string name = "") => Named.Add(value, name);

#if NET6_0_OR_GREATER
    /// <summary>
    /// Write this DateOnly as <paramref name="name" /> wherever it appears.
    /// </summary>
    public void AddNamedDate(DateOnly value, [CallerArgumentExpression(nameof(value))] string name = "") => Named.Add(value, name);

    /// <summary>
    /// Write this TimeOnly as <paramref name="name" /> wherever it appears.
    /// </summary>
    public void AddNamedTime(TimeOnly value, [CallerArgumentExpression(nameof(value))] string name = "") => Named.Add(value, name);
#endif

    /// <summary>
    /// Turn off every scrubber, the built in ones included, for this verification.
    /// </summary>
    public void DisableScrubbers() => ScrubbersDisabled = true;

    #endregion

    #region Comparison

    /// <summary>
    /// Accept every received file of this verification as verified.
    /// </summary>
    public void AutoVerify(bool includeBuildServer = true, bool throwException = false) =>
        AutoVerify(_ => true, includeBuildServer, throwException);

    /// <summary>
    /// Accept the received files <paramref name="autoVerify" /> returns true for.
    /// </summary>
    public void AutoVerify(AutoVerify autoVerify, bool includeBuildServer = true, bool throwException = false)
    {
        if (!includeBuildServer && BuildServer.Detected)
        {
            return;
        }

        AutoVerifyRule = autoVerify;
        AutoVerifyThrows = throwException;
    }

    /// <summary>
    /// Don't open a diff tool for this verification.
    /// </summary>
    public void DisableDiff() => DiffEnabled = false;

    /// <summary>
    /// Compare text that is not exactly equal with <paramref name="compare" />, for the given extensions (all when
    /// none are given).
    /// </summary>
    public void UseStringComparer(StringCompare compare, params string[] extensions)
    {
        if (extensions.Length == 0)
        {
            StringComparers.Add((null, compare));
        }

        foreach (var extension in extensions)
        {
            StringComparers.Add((extension, compare));
        }
    }

    /// <summary>
    /// Compare binary content that is not equal with <paramref name="compare" />, for the given extensions (all
    /// when none are given).
    /// </summary>
    public void UseStreamComparer(StreamCompare compare, params string[] extensions)
    {
        if (extensions.Length == 0)
        {
            StreamComparers.Add((null, compare));
        }

        foreach (var extension in extensions)
        {
            StreamComparers.Add((extension, compare));
        }
    }

    /// <summary>
    /// Run <paramref name="before" /> when the verification starts and <paramref name="after" /> when it ends.
    /// </summary>
    public void OnVerify(Action before, Action after) => VerifyHandlers.Add((before, after));

    /// <summary>
    /// Called for each target that has no verified file yet.
    /// </summary>
    public void OnFirstVerify(FirstVerify firstVerify) => FirstVerifyHandlers.Add(firstVerify);

    /// <summary>
    /// Called for each target that does not match.
    /// </summary>
    public void OnVerifyMismatch(VerifyMismatch verifyMismatch) => MismatchHandlers.Add(verifyMismatch);

    /// <summary>
    /// Called for each verified file the verification no longer produces.
    /// </summary>
    public void OnDelete(VerifyDelete verifyDelete) => DeleteHandlers.Add(verifyDelete);

    #endregion

    #region Appending

    /// <summary>
    /// Add a named value to the snapshot. The verified value moves under <c>target</c>.
    /// </summary>
    public void AppendValue(string name, object? data) => AppendedValues.Add(new(name, data));

    /// <summary>
    /// Add named values to the snapshot.
    /// </summary>
    public void AppendValues(params KeyValuePair<string, object?>[] values) => AppendedValues.AddRange(values);

    /// <summary>
    /// Add named values to the snapshot.
    /// </summary>
    public void AppendValues(IEnumerable<KeyValuePair<string, object?>> values) => AppendedValues.AddRange(values);

    /// <summary>
    /// Verify a file alongside the target. Its name defaults to the file name without extension.
    /// </summary>
    public void AppendFile(string path, string? name = null)
    {
        var extension = Path.GetExtension(path).TrimStart('.');
        name ??= Path.GetFileNameWithoutExtension(path);
        extension = extension.Length == 0 ? "noextension" : extension;
        AppendedFiles.Add(FileExtensions.IsText(extension)
            ? new Target(extension, File.ReadAllText(path), name)
            : new Target(extension, new MemoryStream(File.ReadAllBytes(path)), name));
    }

    /// <summary>
    /// Verify a stream alongside the target.
    /// </summary>
    public void AppendFile(Stream stream, string extension = "txt", string? name = null) =>
        AppendedFiles.Add(new Target(extension, stream, name));

    /// <summary>
    /// Verify text alongside the target, as its own file.
    /// </summary>
    public void AppendContentAsFile(string content, string extension = "txt", string? name = null) =>
        AppendedFiles.Add(new Target(extension, content, name));

    /// <summary>
    /// Verify bytes alongside the target, as their own file.
    /// </summary>
    public void AppendContentAsFile(byte[] content, string extension = "txt", string? name = null) =>
        AppendedFiles.Add(FileExtensions.IsText(extension)
            ? new Target(extension, Encoding.UTF8.GetString(content), name)
            : new Target(extension, new MemoryStream(content), name));

    #endregion

    static readonly char[] invalidFileNameChars = Path.GetInvalidFileNameChars().Concat(['#']).Distinct().ToArray();

    internal static void ValidateFileName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("Invalid character for file name. Value: (empty).");
        }

        var index = value.IndexOfAny(invalidFileNameChars);
        if (index >= 0)
        {
            throw new ArgumentException($"Invalid character for file name. Value: {value}. Char:{value[index]}");
        }
    }

    static void ValidatePath(string value)
    {
        var index = value.IndexOfAny(Path.GetInvalidPathChars());
        if (index >= 0)
        {
            throw new ArgumentException($"Invalid character for path. Value: {value}. Char:{value[index]}");
        }
    }
}
