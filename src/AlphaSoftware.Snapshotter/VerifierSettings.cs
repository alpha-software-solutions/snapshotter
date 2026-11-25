using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace AlphaSoftware.Snapshotter;

/// <summary>
/// Settings for every verification. Change them before the first verification runs, usually in a
/// <c>[ModuleInitializer]</c>; changing them afterwards throws.
/// </summary>
public static class VerifierSettings
{
    internal static readonly VerifySettings Global = new();
    static volatile string? startedBy;

    internal static Encoding Encoding { get; private set; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true);
    internal static bool OmitContent { get; private set; }
    internal static bool SortProperties { get; private set; }
    internal static bool FixNewlines { get; private set; }
    internal static bool TolerateTrailingNewline { get; private set; }
    internal static bool ScrubProjectDirectoryEnabled { get; private set; } = true;
    internal static bool ScrubSolutionDirectoryEnabled { get; private set; } = true;
    internal static bool ScrubUserProfileEnabled { get; private set; } = true;
    internal static GlobalAutoVerify? AutoVerifyRule { get; private set; }
    internal static bool AutoVerifyThrows { get; private set; }
    internal static List<string> ExtraDateTimeFormats { get; } = [];
    internal static List<string> ExtraDateTimeOffsetFormats { get; } = [];
    internal static List<string> ExtraDateFormats { get; } = [];
    internal static List<string> ExtraTimeFormats { get; } = [];
    internal static List<(Type Type, bool Inherit, Func<object, IReadOnlyDictionary<string, object>, AsStringResult> Convert)> StringConversions { get; } = [];
    internal static List<(Type Type, Func<object, string> Convert)> ParameterNames { get; } = [];
    internal static Dictionary<string, StringCompare> StringComparers { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal static Dictionary<string, StreamCompare> StreamComparers { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal static StringCompare? DefaultStringComparer { get; private set; }
    internal static DerivePathInfo? PathInfoDeriver { get; set; }

    internal static void Started(string by) => startedBy ??= by;

    internal static void Guard(string api)
    {
        if (startedBy != null)
        {
            throw new InvalidOperationException(
                $"The API '{api}' must be called prior to any Verify has run. Usually this is done in a [ModuleInitializer]. Verify run by: {startedBy}");
        }
    }

    static void Set(string api, Action<VerifySettings> change)
    {
        Guard(api);
        change(Global);
    }

    #region Output

    /// <summary>
    /// Write snapshot files as UTF-8 without a byte order mark.
    /// </summary>
    public static void UseUtf8NoBom()
    {
        Guard(nameof(UseUtf8NoBom));
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    }

    /// <summary>
    /// Write snapshot files in <paramref name="encoding" />.
    /// </summary>
    public static void UseEncoding(Encoding encoding)
    {
        Guard(nameof(UseEncoding));
        Encoding = encoding;
    }

    /// <summary>
    /// Leave file contents and diffs out of the exception message.
    /// </summary>
    public static void OmitContentFromException()
    {
        Guard(nameof(OmitContentFromException));
        OmitContent = true;
    }

    /// <summary>
    /// Treat carriage returns in verified files as newlines instead of failing.
    /// </summary>
    public static void FixNewlinesOnRead()
    {
        Guard(nameof(FixNewlinesOnRead));
        FixNewlines = true;
    }

    /// <summary>
    /// Ignore one extra trailing newline in a verified file.
    /// </summary>
    public static void IgnoreTrailingNewline()
    {
        Guard(nameof(IgnoreTrailingNewline));
        TolerateTrailingNewline = true;
    }

    /// <summary>
    /// Write members in ordinal name order instead of declaration order.
    /// </summary>
    public static void SortPropertiesAlphabetically()
    {
        Guard(nameof(SortPropertiesAlphabetically));
        SortProperties = true;
    }

    /// <summary>
    /// Verify root values of <typeparamref name="T" /> as text: <paramref name="toString" /> or their
    /// <c>ToString()</c>.
    /// </summary>
    public static void TreatAsString<T>(AsString<T>? toString = null, bool checkInheritance = false)
    {
        Guard(nameof(TreatAsString));
        StringConversions.Add((typeof(T), checkInheritance,
            (value, context) => toString == null ? value.ToString() ?? "" : toString((T)value, context)));
    }

    /// <summary>
    /// Write parameter values of <typeparamref name="T" /> in file names with <paramref name="toName" />.
    /// </summary>
    public static void NameForParameter<T>(ParameterToName<T> toName)
    {
        Guard(nameof(NameForParameter));
        ParameterNames.Add((typeof(T), value => toName((T)value)));
    }

    #endregion

    #region Naming

    /// <inheritdoc cref="VerifySettings.UniqueForRuntime" />
    public static void UniqueForRuntime() => Set(nameof(UniqueForRuntime), _ => _.UniqueForRuntime());

    /// <inheritdoc cref="VerifySettings.UniqueForRuntimeAndVersion" />
    public static void UniqueForRuntimeAndVersion() => Set(nameof(UniqueForRuntimeAndVersion), _ => _.UniqueForRuntimeAndVersion());

    /// <inheritdoc cref="VerifySettings.UniqueForTargetFramework" />
    public static void UniqueForTargetFramework() => Set(nameof(UniqueForTargetFramework), _ => _.UniqueForTargetFramework());

    /// <inheritdoc cref="VerifySettings.UniqueForTargetFrameworkAndVersion" />
    public static void UniqueForTargetFrameworkAndVersion() => Set(nameof(UniqueForTargetFrameworkAndVersion), _ => _.UniqueForTargetFrameworkAndVersion());

    /// <inheritdoc cref="VerifySettings.UniqueForAssemblyConfiguration" />
    public static void UniqueForAssemblyConfiguration() => Set(nameof(UniqueForAssemblyConfiguration), _ => _.UniqueForAssemblyConfiguration());

    /// <inheritdoc cref="VerifySettings.UniqueForArchitecture" />
    public static void UniqueForArchitecture() => Set(nameof(UniqueForArchitecture), _ => _.UniqueForArchitecture());

    /// <inheritdoc cref="VerifySettings.UniqueForOSPlatform" />
    public static void UniqueForOSPlatform() => Set(nameof(UniqueForOSPlatform), _ => _.UniqueForOSPlatform());

    /// <inheritdoc cref="VerifySettings.DisableRequireUniquePrefix" />
    public static void DisableRequireUniquePrefix() => Set(nameof(DisableRequireUniquePrefix), _ => _.DisableRequireUniquePrefix());

    /// <inheritdoc cref="VerifySettings.IgnoreParameters" />
    public static void IgnoreParameters(params string[] parameterNames) => Set(nameof(IgnoreParameters), _ => _.IgnoreParameters(parameterNames));

    #endregion

    #region Serialization

    /// <inheritdoc cref="VerifySettings.UseStrictJson" />
    public static void UseStrictJson() => Set(nameof(UseStrictJson), _ => _.UseStrictJson());

    /// <summary>
    /// Whether objects are written as strict JSON.
    /// </summary>
    public static bool StrictJson => Global.StrictJson == true;

    /// <inheritdoc cref="VerifySettings.AddExtraSettings" />
    public static void AddExtraSettings(Action<SerializerSettings> action) => Set(nameof(AddExtraSettings), _ => _.AddExtraSettings(action));

    /// <inheritdoc cref="VerifySettings.DontIgnoreEmptyCollections" />
    public static void DontIgnoreEmptyCollections() => Set(nameof(DontIgnoreEmptyCollections), _ => _.DontIgnoreEmptyCollections());

    /// <inheritdoc cref="VerifySettings.DontSortDictionaries" />
    public static void DontSortDictionaries() => Set(nameof(DontSortDictionaries), _ => _.DontSortDictionaries());

    /// <inheritdoc cref="VerifySettings.IncludeObsoletes" />
    public static void IncludeObsoletes() => Set(nameof(IncludeObsoletes), _ => _.IncludeObsoletes());

    /// <inheritdoc cref="VerifySettings.IgnoreStackTrace" />
    public static void IgnoreStackTrace() => Set(nameof(IgnoreStackTrace), _ => _.IgnoreStackTrace());

    /// <inheritdoc cref="VerifySettings.IgnoreMembersThatThrow{T}()" />
    public static void IgnoreMembersThatThrow<T>()
        where T : Exception =>
        Set(nameof(IgnoreMembersThatThrow), _ => _.IgnoreMembersThatThrow<T>());

    /// <inheritdoc cref="VerifySettings.IgnoreMembersThatThrow(Func{Exception, bool})" />
    public static void IgnoreMembersThatThrow(Func<Exception, bool> item) => Set(nameof(IgnoreMembersThatThrow), _ => _.IgnoreMembersThatThrow(item));

    /// <inheritdoc cref="VerifySettings.IgnoreMember(string)" />
    public static void IgnoreMember(string name) => Set(nameof(IgnoreMember), _ => _.IgnoreMember(name));

    /// <inheritdoc cref="VerifySettings.IgnoreMembers(string[])" />
    public static void IgnoreMembers(params string[] names) => Set(nameof(IgnoreMembers), _ => _.IgnoreMembers(names));

    /// <inheritdoc cref="VerifySettings.IgnoreMember(Type, string)" />
    public static void IgnoreMember(Type declaringType, string name) => Set(nameof(IgnoreMember), _ => _.IgnoreMember(declaringType, name));

    /// <inheritdoc cref="VerifySettings.IgnoreMember{T}(string)" />
    public static void IgnoreMember<T>(string name) => Set(nameof(IgnoreMember), _ => _.IgnoreMember<T>(name));

    /// <inheritdoc cref="VerifySettings.IgnoreMember{T}(Expression{Func{T, object}})" />
    public static void IgnoreMember<T>(Expression<Func<T, object?>> expression) => Set(nameof(IgnoreMember), _ => _.IgnoreMember(expression));

    /// <inheritdoc cref="VerifySettings.IgnoreMembers{T}(Expression{Func{T, object}}[])" />
    public static void IgnoreMembers<T>(params Expression<Func<T, object?>>[] expressions) => Set(nameof(IgnoreMembers), _ => _.IgnoreMembers(expressions));

    /// <inheritdoc cref="VerifySettings.IgnoreMembers(Func{string, bool})" />
    public static void IgnoreMembers(Func<string, bool> predicate) => Set(nameof(IgnoreMembers), _ => _.IgnoreMembers(predicate));

    /// <inheritdoc cref="VerifySettings.IgnoreMembers(Func{MemberInfo, bool})" />
    public static void IgnoreMembers(Func<MemberInfo, bool> predicate) => Set(nameof(IgnoreMembers), _ => _.IgnoreMembers(predicate));

    /// <inheritdoc cref="VerifySettings.IgnoreMembersWithType{T}" />
    public static void IgnoreMembersWithType<T>() => Set(nameof(IgnoreMembersWithType), _ => _.IgnoreMembersWithType<T>());

    /// <inheritdoc cref="VerifySettings.IgnoreMembersWithType(Type)" />
    public static void IgnoreMembersWithType(Type type) => Set(nameof(IgnoreMembersWithType), _ => _.IgnoreMembersWithType(type));

    /// <inheritdoc cref="VerifySettings.ScrubMember(string)" />
    public static void ScrubMember(string name) => Set(nameof(ScrubMember), _ => _.ScrubMember(name));

    /// <inheritdoc cref="VerifySettings.ScrubMembers(string[])" />
    public static void ScrubMembers(params string[] names) => Set(nameof(ScrubMembers), _ => _.ScrubMembers(names));

    /// <inheritdoc cref="VerifySettings.ScrubMember(Type, string)" />
    public static void ScrubMember(Type declaringType, string name) => Set(nameof(ScrubMember), _ => _.ScrubMember(declaringType, name));

    /// <inheritdoc cref="VerifySettings.ScrubMember{T}(string)" />
    public static void ScrubMember<T>(string name) => Set(nameof(ScrubMember), _ => _.ScrubMember<T>(name));

    /// <inheritdoc cref="VerifySettings.ScrubMember{T}(Expression{Func{T, object}})" />
    public static void ScrubMember<T>(Expression<Func<T, object?>> expression) => Set(nameof(ScrubMember), _ => _.ScrubMember(expression));

    /// <inheritdoc cref="VerifySettings.ScrubMembers(Func{string, bool})" />
    public static void ScrubMembers(Func<string, bool> predicate) => Set(nameof(ScrubMembers), _ => _.ScrubMembers(predicate));

    /// <inheritdoc cref="VerifySettings.ScrubMembersWithType{T}" />
    public static void ScrubMembersWithType<T>() => Set(nameof(ScrubMembersWithType), _ => _.ScrubMembersWithType<T>());

    /// <inheritdoc cref="VerifySettings.ScrubMembersWithType(Type)" />
    public static void ScrubMembersWithType(Type type) => Set(nameof(ScrubMembersWithType), _ => _.ScrubMembersWithType(type));

    /// <inheritdoc cref="VerifySettings.IgnoreInstance{T}" />
    public static void IgnoreInstance<T>(Func<T, bool> shouldIgnore) => Set(nameof(IgnoreInstance), _ => _.IgnoreInstance(shouldIgnore));

    /// <inheritdoc cref="VerifySettings.ScrubInstance{T}" />
    public static void ScrubInstance<T>(Func<T, bool> shouldScrub) => Set(nameof(ScrubInstance), _ => _.ScrubInstance(shouldScrub));

    #endregion

    #region Scrubbing

    /// <inheritdoc cref="VerifySettings.AddScrubber(Action{StringBuilder}, ScrubberLocation)" />
    public static void AddScrubber(Action<StringBuilder> scrubber, ScrubberLocation location = ScrubberLocation.First) =>
        Set(nameof(AddScrubber), _ => _.AddScrubber(scrubber, location));

    /// <inheritdoc cref="VerifySettings.AddScrubber(Action{StringBuilder, Counter}, ScrubberLocation)" />
    public static void AddScrubber(Action<StringBuilder, Counter> scrubber, ScrubberLocation location = ScrubberLocation.First) =>
        Set(nameof(AddScrubber), _ => _.AddScrubber(scrubber, location));

    /// <inheritdoc cref="VerifySettings.AddScrubber(Action{StringBuilder, Counter, IReadOnlyDictionary{string, object}}, ScrubberLocation)" />
    public static void AddScrubber(Action<StringBuilder, Counter, IReadOnlyDictionary<string, object>> scrubber, ScrubberLocation location = ScrubberLocation.First) =>
        Set(nameof(AddScrubber), _ => _.AddScrubber(scrubber, location));

    /// <inheritdoc cref="VerifySettings.AddScrubber(string, Action{StringBuilder}, ScrubberLocation)" />
    public static void AddScrubber(string extension, Action<StringBuilder> scrubber, ScrubberLocation location = ScrubberLocation.First) =>
        Set(nameof(AddScrubber), _ => _.AddScrubber(extension, scrubber, location));

    /// <inheritdoc cref="VerifySettings.ScrubLines(Func{string, bool})" />
    public static void ScrubLines(Func<string, bool> removeLine) => Set(nameof(ScrubLines), _ => _.ScrubLines(removeLine));

    /// <inheritdoc cref="VerifySettings.ScrubLinesContaining(string[])" />
    public static void ScrubLinesContaining(params string[] stringToMatch) => Set(nameof(ScrubLinesContaining), _ => _.ScrubLinesContaining(stringToMatch));

    /// <inheritdoc cref="VerifySettings.ScrubLinesContaining(StringComparison, string[])" />
    public static void ScrubLinesContaining(StringComparison comparison, params string[] stringToMatch) =>
        Set(nameof(ScrubLinesContaining), _ => _.ScrubLinesContaining(comparison, stringToMatch));

    /// <inheritdoc cref="VerifySettings.ScrubLinesWithReplace(Func{string, string})" />
    public static void ScrubLinesWithReplace(Func<string, string?> replaceLine) => Set(nameof(ScrubLinesWithReplace), _ => _.ScrubLinesWithReplace(replaceLine));

    /// <inheritdoc cref="VerifySettings.ScrubEmptyLines" />
    public static void ScrubEmptyLines() => Set(nameof(ScrubEmptyLines), _ => _.ScrubEmptyLines());

    /// <inheritdoc cref="VerifySettings.ScrubReplace" />
    public static void ScrubReplace(string find, string replacement, StringComparison comparison = StringComparison.Ordinal, bool requireWordBoundary = false) =>
        Set(nameof(ScrubReplace), _ => _.ScrubReplace(find, replacement, comparison, requireWordBoundary));

    /// <inheritdoc cref="VerifySettings.ScrubMachineName" />
    public static void ScrubMachineName() => Set(nameof(ScrubMachineName), _ => _.ScrubMachineName());

    /// <inheritdoc cref="VerifySettings.ScrubUserName" />
    public static void ScrubUserName() => Set(nameof(ScrubUserName), _ => _.ScrubUserName());

    /// <inheritdoc cref="VerifySettings.ScrubInlineGuids" />
    public static void ScrubInlineGuids(GuidFormats formats = GuidFormats.All) => Set(nameof(ScrubInlineGuids), _ => _.ScrubInlineGuids(formats));

    /// <inheritdoc cref="VerifySettings.ScrubInlineDateTimes" />
    public static void ScrubInlineDateTimes(string format, CultureInfo? culture = null) => Set(nameof(ScrubInlineDateTimes), _ => _.ScrubInlineDateTimes(format, culture));

    /// <inheritdoc cref="VerifySettings.ScrubInlineDateTimeOffsets" />
    public static void ScrubInlineDateTimeOffsets(string format, CultureInfo? culture = null) =>
        Set(nameof(ScrubInlineDateTimeOffsets), _ => _.ScrubInlineDateTimeOffsets(format, culture));

    /// <inheritdoc cref="VerifySettings.ScrubInlineDates" />
    public static void ScrubInlineDates(string format, CultureInfo? culture = null) => Set(nameof(ScrubInlineDates), _ => _.ScrubInlineDates(format, culture));

    /// <inheritdoc cref="VerifySettings.DontScrubGuids" />
    public static void DontScrubGuids() => Set(nameof(DontScrubGuids), _ => _.DontScrubGuids());

    /// <inheritdoc cref="VerifySettings.DontScrubDateTimes" />
    public static void DontScrubDateTimes() => Set(nameof(DontScrubDateTimes), _ => _.DontScrubDateTimes());

    /// <inheritdoc cref="VerifySettings.ScrubNumericIds" />
    public static void ScrubNumericIds() => Set(nameof(ScrubNumericIds), _ => _.ScrubNumericIds());

    /// <inheritdoc cref="VerifySettings.DontScrubNumericIds" />
    public static void DontScrubNumericIds() => Set(nameof(DontScrubNumericIds), _ => _.DontScrubNumericIds());

    /// <inheritdoc cref="VerifySettings.DisableDateCounting" />
    public static void DisableDateCounting() => Set(nameof(DisableDateCounting), _ => _.DisableDateCounting());

    /// <summary>
    /// Write this Guid as <paramref name="name" /> in every verification.
    /// </summary>
    public static void AddNamedGuid(Guid value, string name) => Set(nameof(AddNamedGuid), _ => AddNamed(_, value, name));

    /// <summary>
    /// Write this DateTime as <paramref name="name" /> in every verification.
    /// </summary>
    public static void AddNamedDateTime(DateTime value, string name) => Set(nameof(AddNamedDateTime), _ => AddNamed(_, value, name));

    /// <summary>
    /// Write this DateTimeOffset as <paramref name="name" /> in every verification.
    /// </summary>
    public static void AddNamedDateTimeOffset(DateTimeOffset value, string name) => Set(nameof(AddNamedDateTimeOffset), _ => AddNamed(_, value, name));

#if NET6_0_OR_GREATER
    /// <summary>
    /// Write this DateOnly as <paramref name="name" /> in every verification.
    /// </summary>
    public static void AddNamedDate(DateOnly value, string name) => Set(nameof(AddNamedDate), _ => AddNamed(_, value, name));

    /// <summary>
    /// Write this TimeOnly as <paramref name="name" /> in every verification.
    /// </summary>
    public static void AddNamedTime(TimeOnly value, string name) => Set(nameof(AddNamedTime), _ => AddNamed(_, value, name));
#endif

    static void AddNamed(VerifySettings settings, object value, string name)
    {
        if (settings.Named.Contains(value))
        {
            throw new ArgumentException($"The value {value} already has a name.", nameof(value));
        }

        settings.Named.Add(value, name);
    }

    /// <summary>
    /// Also recognize whole string values in this format as dates (<c>DateTime_N</c>).
    /// </summary>
    public static void AddExtraDateTimeFormat(string format)
    {
        Guard(nameof(AddExtraDateTimeFormat));
        ExtraDateTimeFormats.Add(format);
    }

    /// <summary>
    /// Also recognize whole string values in this format as dates (<c>DateTimeOffset_N</c>).
    /// </summary>
    public static void AddExtraDateTimeOffsetFormat(string format)
    {
        Guard(nameof(AddExtraDateTimeOffsetFormat));
        ExtraDateTimeOffsetFormats.Add(format);
    }

    /// <summary>
    /// Also recognize whole string values in this format as dates (<c>Date_N</c>).
    /// </summary>
    public static void AddExtraDateFormat(string format)
    {
        Guard(nameof(AddExtraDateFormat));
        ExtraDateFormats.Add(format);
    }

    /// <summary>
    /// Also recognize whole string values in this format as times (<c>Time_N</c>).
    /// </summary>
    public static void AddExtraTimeFormat(string format)
    {
        Guard(nameof(AddExtraTimeFormat));
        ExtraTimeFormats.Add(format);
    }

    /// <summary>
    /// Keep the project directory instead of writing <c>{ProjectDirectory}</c>.
    /// </summary>
    public static void DontScrubProjectDirectory()
    {
        Guard(nameof(DontScrubProjectDirectory));
        ScrubProjectDirectoryEnabled = false;
    }

    /// <summary>
    /// Keep the solution directory instead of writing <c>{SolutionDirectory}</c>.
    /// </summary>
    public static void DontScrubSolutionDirectory()
    {
        Guard(nameof(DontScrubSolutionDirectory));
        ScrubSolutionDirectoryEnabled = false;
    }

    /// <summary>
    /// Keep the user profile directory instead of writing <c>{UserProfile}</c>.
    /// </summary>
    public static void DontScrubUserProfile()
    {
        Guard(nameof(DontScrubUserProfile));
        ScrubUserProfileEnabled = false;
    }

    #endregion

    #region Comparison

    /// <summary>
    /// Accept every received file as verified.
    /// </summary>
    public static void AutoVerify(bool includeBuildServer = true, bool throwException = false) =>
        AutoVerify((_, _, _) => true, includeBuildServer, throwException);

    /// <summary>
    /// Accept the received files <paramref name="autoVerify" /> returns true for.
    /// </summary>
    public static void AutoVerify(GlobalAutoVerify autoVerify, bool includeBuildServer = true, bool throwException = false)
    {
        Guard(nameof(AutoVerify));
        if (!includeBuildServer && BuildServer.Detected)
        {
            return;
        }

        AutoVerifyRule = autoVerify;
        AutoVerifyThrows = throwException;
    }

    /// <summary>
    /// Compare text with this extension that is not exactly equal with <paramref name="compare" />.
    /// </summary>
    public static void RegisterStringComparer(string extension, StringCompare compare)
    {
        Guard(nameof(RegisterStringComparer));
        StringComparers[extension] = compare;
    }

    /// <summary>
    /// Compare binary content with this extension that is not equal with <paramref name="compare" />.
    /// </summary>
    public static void RegisterStreamComparer(string extension, StreamCompare compare)
    {
        Guard(nameof(RegisterStreamComparer));
        StreamComparers[extension] = compare;
    }

    /// <summary>
    /// Compare text of any extension without its own comparer with <paramref name="compare" />.
    /// </summary>
    public static void SetDefaultStringComparer(StringCompare compare)
    {
        Guard(nameof(SetDefaultStringComparer));
        DefaultStringComparer = compare;
    }

    /// <inheritdoc cref="VerifySettings.OnVerify" />
    public static void OnVerify(Action before, Action after) => Set(nameof(OnVerify), _ => _.OnVerify(before, after));

    /// <inheritdoc cref="VerifySettings.OnFirstVerify" />
    public static void OnFirstVerify(FirstVerify firstVerify) => Set(nameof(OnFirstVerify), _ => _.OnFirstVerify(firstVerify));

    /// <inheritdoc cref="VerifySettings.OnVerifyMismatch" />
    public static void OnVerifyMismatch(VerifyMismatch verifyMismatch) => Set(nameof(OnVerifyMismatch), _ => _.OnVerifyMismatch(verifyMismatch));

    /// <inheritdoc cref="VerifySettings.OnDelete" />
    public static void OnDelete(VerifyDelete verifyDelete) => Set(nameof(OnDelete), _ => _.OnDelete(verifyDelete));

    #endregion

    internal static void ResetForTests() => startedBy = null;
}
