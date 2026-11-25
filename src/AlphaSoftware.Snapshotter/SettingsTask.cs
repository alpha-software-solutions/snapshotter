// The fluent members below forward to VerifySettings. They are generated from VerifySettings.cs; keep the two in
// step when adding a setting.
using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace AlphaSoftware.Snapshotter;

/// <summary>
/// A verification that has not run yet. Configure it with the same methods as <see cref="VerifySettings" />, then
/// await it; a test can also return it directly as a <see cref="Task" />.
/// </summary>
public sealed class SettingsTask
{
    readonly VerifySettings settings;
    readonly Func<VerifySettings, Task<VerifyResult>> run;
    Task<VerifyResult>? task;

    internal SettingsTask(VerifySettings settings, Func<VerifySettings, Task<VerifyResult>> run)
    {
        this.settings = settings;
        this.run = run;
    }

    /// <summary>
    /// The settings of this verification. Throws once it has started.
    /// </summary>
    public VerifySettings CurrentSettings =>
        task == null
            ? settings
            : throw new InvalidOperationException("This verification has already started. Configure it before awaiting it.");

    /// <summary>
    /// Adds a value to the context scrubbers, comparers and converters see.
    /// </summary>
    public SettingsTask AddContext(string name, object value)
    {
        CurrentSettings.Context[name] = value;
        return this;
    }

    /// <summary>
    /// Runs the verification, once.
    /// </summary>
    public Task<VerifyResult> ToTask() =>
        task ??= run(settings);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public TaskAwaiter<VerifyResult> GetAwaiter() =>
        ToTask().GetAwaiter();

    public ConfiguredTaskAwaitable<VerifyResult> ConfigureAwait(bool continueOnCapturedContext) =>
        ToTask().ConfigureAwait(continueOnCapturedContext);

    public static implicit operator Task<VerifyResult>(SettingsTask settingsTask) =>
        settingsTask.ToTask();


    /// <summary>
    /// Store snapshots in <paramref name="directory" />. A relative path is relative to the test source file's
    /// directory.
    /// </summary>
    public SettingsTask UseDirectory(string directory)
    {
        CurrentSettings.UseDirectory(directory);
        return this;
    }

    /// <summary>
    /// Replace <c>{TypeName}.{MethodName}{_Parameters}</c> with <paramref name="fileName" />.
    /// </summary>
    public SettingsTask UseFileName(string fileName)
    {
        CurrentSettings.UseFileName(fileName);
        return this;
    }

    /// <summary>
    /// Replace the type part of the snapshot name.
    /// </summary>
    public SettingsTask UseTypeName(string name)
    {
        CurrentSettings.UseTypeName(name);
        return this;
    }

    /// <summary>
    /// Replace the method part of the snapshot name. Use it to verify more than once in one test.
    /// </summary>
    public SettingsTask UseMethodName(string name)
    {
        CurrentSettings.UseMethodName(name);
        return this;
    }

    /// <summary>
    /// Store each target as a file in a directory named after the test.
    /// </summary>
    public SettingsTask UseUniqueDirectory()
    {
        CurrentSettings.UseUniqueDirectory();
        return this;
    }

    /// <summary>
    /// The arguments of a parameterized test, added to the name as <c>_{name}={value}</c>. The names come from the
    /// test method. With NUnit, xUnit v3 and TUnit the arguments are found without this call.
    /// </summary>
    public SettingsTask UseParameters(params object?[] parameters)
    {
        CurrentSettings.UseParameters(parameters);
        return this;
    }

    /// <summary>
    /// Use <paramref name="parametersText" /> as the parameter part of the name, instead of argument values.
    /// </summary>
    public SettingsTask UseTextForParameters(string parametersText)
    {
        CurrentSettings.UseTextForParameters(parametersText);
        return this;
    }

    /// <summary>
    /// Leave the named parameters (all of them when none are named) out of the verified file name, so all cases
    /// share one verified file. Received files keep every parameter.
    /// </summary>
    public SettingsTask IgnoreParameters(params string[] parameterNames)
    {
        CurrentSettings.IgnoreParameters(parameterNames);
        return this;
    }

    /// <summary>
    /// Uses <paramref name="parameters" /> (if any) and leaves all parameters out of the verified file name.
    /// </summary>
    public SettingsTask IgnoreParametersForVerified(params object?[] parameters)
    {
        CurrentSettings.IgnoreParametersForVerified(parameters);
        return this;
    }

    /// <summary>
    /// A separate snapshot per runtime: <c>.DotNet</c>, <c>.Net</c> (.NET Framework) or <c>.Mono</c>.
    /// </summary>
    public SettingsTask UniqueForRuntime()
    {
        CurrentSettings.UniqueForRuntime();
        return this;
    }

    /// <summary>
    /// A separate snapshot per runtime and version, such as <c>.DotNet10_0</c> or <c>.Net4_8</c>.
    /// </summary>
    public SettingsTask UniqueForRuntimeAndVersion()
    {
        CurrentSettings.UniqueForRuntimeAndVersion();
        return this;
    }

    /// <summary>
    /// A separate snapshot per target framework of the test assembly: <c>.DotNet</c> or <c>.Net</c>.
    /// </summary>
    public SettingsTask UniqueForTargetFramework()
    {
        CurrentSettings.UniqueForTargetFramework();
        return this;
    }

    /// <summary>
    /// A separate snapshot per target framework and version of the test assembly, such as <c>.DotNet10_0</c>.
    /// </summary>
    public SettingsTask UniqueForTargetFrameworkAndVersion()
    {
        CurrentSettings.UniqueForTargetFrameworkAndVersion();
        return this;
    }

    /// <summary>
    /// A separate snapshot per build configuration: <c>.Debug</c> or <c>.Release</c>.
    /// </summary>
    public SettingsTask UniqueForAssemblyConfiguration()
    {
        CurrentSettings.UniqueForAssemblyConfiguration();
        return this;
    }

    /// <summary>
    /// A separate snapshot per process architecture: <c>.x64</c>, <c>.x86</c>, <c>.arm64</c>.
    /// </summary>
    public SettingsTask UniqueForArchitecture()
    {
        CurrentSettings.UniqueForArchitecture();
        return this;
    }

    /// <summary>
    /// A separate snapshot per operating system: <c>.Windows</c>, <c>.Linux</c> or <c>.OSX</c>.
    /// </summary>
    public SettingsTask UniqueForOSPlatform()
    {
        CurrentSettings.UniqueForOSPlatform();
        return this;
    }

    /// <summary>
    /// Allow two verifications in one run to use the same file names.
    /// </summary>
    public SettingsTask DisableRequireUniquePrefix()
    {
        CurrentSettings.DisableRequireUniquePrefix();
        return this;
    }

    /// <summary>
    /// Write objects as real JSON, with quoted names and strings, in <c>.json</c> files.
    /// </summary>
    public SettingsTask UseStrictJson()
    {
        CurrentSettings.UseStrictJson();
        return this;
    }

    /// <summary>
    /// Change the serializer settings: add converters, or keep default and null values.
    /// </summary>
    public SettingsTask AddExtraSettings(Action<SerializerSettings> action)
    {
        CurrentSettings.AddExtraSettings(action);
        return this;
    }

    /// <summary>
    /// Write empty collections as <c>[]</c> instead of leaving them out.
    /// </summary>
    public SettingsTask DontIgnoreEmptyCollections()
    {
        CurrentSettings.DontIgnoreEmptyCollections();
        return this;
    }

    /// <summary>
    /// Keep dictionaries in their own order instead of sorting them by key.
    /// </summary>
    public SettingsTask DontSortDictionaries()
    {
        CurrentSettings.DontSortDictionaries();
        return this;
    }

    /// <summary>
    /// Also write members marked <c>[Obsolete]</c>.
    /// </summary>
    public SettingsTask IncludeObsoletes()
    {
        CurrentSettings.IncludeObsoletes();
        return this;
    }

    /// <summary>
    /// Leave the stack trace out of exceptions.
    /// </summary>
    public SettingsTask IgnoreStackTrace()
    {
        CurrentSettings.IgnoreStackTrace();
        return this;
    }

    /// <summary>
    /// Leave out members whose getter throws <typeparamref name="T" />. <see cref="NotImplementedException" /> and
    /// <see cref="NotSupportedException" /> are always left out.
    /// </summary>
    public SettingsTask IgnoreMembersThatThrow<T>()
        where T : Exception
    {
        CurrentSettings.IgnoreMembersThatThrow<T>();
        return this;
    }

    /// <summary>
    /// Leave out members whose getter throws an exception <paramref name="item" /> matches.
    /// </summary>
    public SettingsTask IgnoreMembersThatThrow(Func<Exception, bool> item)
    {
        CurrentSettings.IgnoreMembersThatThrow(item);
        return this;
    }

    /// <summary>
    /// Leave out members whose getter throws a <typeparamref name="T" /> that <paramref name="item" /> matches.
    /// </summary>
    public SettingsTask IgnoreMembersThatThrow<T>(Func<T, bool> item)
        where T : Exception
    {
        CurrentSettings.IgnoreMembersThatThrow<T>(item);
        return this;
    }

    /// <summary>
    /// Leave out members with this name, on every type. Also applies to dictionary keys and JSON properties.
    /// </summary>
    public SettingsTask IgnoreMember(string name)
    {
        CurrentSettings.IgnoreMember(name);
        return this;
    }

    /// <summary>
    /// Leave out members with these names, on every type.
    /// </summary>
    public SettingsTask IgnoreMembers(params string[] names)
    {
        CurrentSettings.IgnoreMembers(names);
        return this;
    }

    /// <summary>
    /// Leave out the member <paramref name="name" /> of <paramref name="declaringType" /> and derived types.
    /// </summary>
    public SettingsTask IgnoreMember(Type declaringType, string name)
    {
        CurrentSettings.IgnoreMember(declaringType, name);
        return this;
    }

    /// <summary>
    /// Leave out these members of <paramref name="declaringType" />.
    /// </summary>
    public SettingsTask IgnoreMembers(Type declaringType, params string[] names)
    {
        CurrentSettings.IgnoreMembers(declaringType, names);
        return this;
    }

    /// <summary>
    /// Leave out the member <paramref name="name" /> of <typeparamref name="T" />.
    /// </summary>
    public SettingsTask IgnoreMember<T>(string name)
    {
        CurrentSettings.IgnoreMember<T>(name);
        return this;
    }

    /// <summary>
    /// Leave out these members of <typeparamref name="T" />.
    /// </summary>
    public SettingsTask IgnoreMembers<T>(params string[] names)
    {
        CurrentSettings.IgnoreMembers<T>(names);
        return this;
    }

    /// <summary>
    /// Leave out one member of <typeparamref name="T" />: <c>IgnoreMember&lt;User&gt;(_ =&gt; _.Password)</c>.
    /// </summary>
    public SettingsTask IgnoreMember<T>(Expression<Func<T, object?>> expression)
    {
        CurrentSettings.IgnoreMember<T>(expression);
        return this;
    }

    /// <summary>
    /// Leave out these members of <typeparamref name="T" />.
    /// </summary>
    public SettingsTask IgnoreMembers<T>(params Expression<Func<T, object?>>[] expressions)
    {
        CurrentSettings.IgnoreMembers<T>(expressions);
        return this;
    }

    /// <summary>
    /// Leave out members whose name <paramref name="predicate" /> matches. Also applies to dictionary keys.
    /// </summary>
    public SettingsTask IgnoreMembers(Func<string, bool> predicate)
    {
        CurrentSettings.IgnoreMembers(predicate);
        return this;
    }

    /// <summary>
    /// Leave out members <paramref name="predicate" /> matches.
    /// </summary>
    public SettingsTask IgnoreMembers(Func<MemberInfo, bool> predicate)
    {
        CurrentSettings.IgnoreMembers(predicate);
        return this;
    }

    /// <summary>
    /// Leave out every member whose type is, derives from or implements <typeparamref name="T" />.
    /// </summary>
    public SettingsTask IgnoreMembersWithType<T>()
    {
        CurrentSettings.IgnoreMembersWithType<T>();
        return this;
    }

    /// <summary>
    /// Leave out every member whose type is, derives from or implements <paramref name="type" />. An open generic
    /// type matches its constructed forms.
    /// </summary>
    public SettingsTask IgnoreMembersWithType(Type type)
    {
        CurrentSettings.IgnoreMembersWithType(type);
        return this;
    }

    /// <summary>
    /// Write members with this name, on every type, as <c>{Scrubbed}</c>.
    /// </summary>
    public SettingsTask ScrubMember(string name)
    {
        CurrentSettings.ScrubMember(name);
        return this;
    }

    /// <summary>
    /// Write members with these names as <c>{Scrubbed}</c>.
    /// </summary>
    public SettingsTask ScrubMembers(params string[] names)
    {
        CurrentSettings.ScrubMembers(names);
        return this;
    }

    /// <summary>
    /// Write the member <paramref name="name" /> of <paramref name="declaringType" /> as <c>{Scrubbed}</c>.
    /// </summary>
    public SettingsTask ScrubMember(Type declaringType, string name)
    {
        CurrentSettings.ScrubMember(declaringType, name);
        return this;
    }

    /// <summary>
    /// Write these members of <paramref name="declaringType" /> as <c>{Scrubbed}</c>.
    /// </summary>
    public SettingsTask ScrubMembers(Type declaringType, params string[] names)
    {
        CurrentSettings.ScrubMembers(declaringType, names);
        return this;
    }

    /// <summary>
    /// Write the member <paramref name="name" /> of <typeparamref name="T" /> as <c>{Scrubbed}</c>.
    /// </summary>
    public SettingsTask ScrubMember<T>(string name)
    {
        CurrentSettings.ScrubMember<T>(name);
        return this;
    }

    /// <summary>
    /// Write these members of <typeparamref name="T" /> as <c>{Scrubbed}</c>.
    /// </summary>
    public SettingsTask ScrubMembers<T>(params string[] names)
    {
        CurrentSettings.ScrubMembers<T>(names);
        return this;
    }

    /// <summary>
    /// Write one member of <typeparamref name="T" /> as <c>{Scrubbed}</c>.
    /// </summary>
    public SettingsTask ScrubMember<T>(Expression<Func<T, object?>> expression)
    {
        CurrentSettings.ScrubMember<T>(expression);
        return this;
    }

    /// <summary>
    /// Write these members of <typeparamref name="T" /> as <c>{Scrubbed}</c>.
    /// </summary>
    public SettingsTask ScrubMembers<T>(params Expression<Func<T, object?>>[] expressions)
    {
        CurrentSettings.ScrubMembers<T>(expressions);
        return this;
    }

    /// <summary>
    /// Write members whose name <paramref name="predicate" /> matches as <c>{Scrubbed}</c>.
    /// </summary>
    public SettingsTask ScrubMembers(Func<string, bool> predicate)
    {
        CurrentSettings.ScrubMembers(predicate);
        return this;
    }

    /// <summary>
    /// Write members <paramref name="predicate" /> matches as <c>{Scrubbed}</c>.
    /// </summary>
    public SettingsTask ScrubMembers(Func<MemberInfo, bool> predicate)
    {
        CurrentSettings.ScrubMembers(predicate);
        return this;
    }

    /// <summary>
    /// Write every member of type <typeparamref name="T" /> as <c>{Scrubbed}</c>.
    /// </summary>
    public SettingsTask ScrubMembersWithType<T>()
    {
        CurrentSettings.ScrubMembersWithType<T>();
        return this;
    }

    /// <summary>
    /// Write every member of type <paramref name="type" /> as <c>{Scrubbed}</c>.
    /// </summary>
    public SettingsTask ScrubMembersWithType(Type type)
    {
        CurrentSettings.ScrubMembersWithType(type);
        return this;
    }

    /// <summary>
    /// Leave out values of <typeparamref name="T" /> that <paramref name="shouldIgnore" /> matches, wherever they
    /// appear.
    /// </summary>
    public SettingsTask IgnoreInstance<T>(Func<T, bool> shouldIgnore)
    {
        CurrentSettings.IgnoreInstance<T>(shouldIgnore);
        return this;
    }

    /// <summary>
    /// Write values of <typeparamref name="T" /> that <paramref name="shouldScrub" /> matches as <c>{Scrubbed}</c>.
    /// </summary>
    public SettingsTask ScrubInstance<T>(Func<T, bool> shouldScrub)
    {
        CurrentSettings.ScrubInstance<T>(shouldScrub);
        return this;
    }

    /// <summary>
    /// Rewrite the text of every text target.
    /// </summary>
    public SettingsTask AddScrubber(Action<StringBuilder> scrubber, ScrubberLocation location = ScrubberLocation.First)
    {
        CurrentSettings.AddScrubber(scrubber, location);
        return this;
    }

    /// <summary>
    /// Rewrite the text of every text target, with the verification's counter.
    /// </summary>
    public SettingsTask AddScrubber(Action<StringBuilder, Counter> scrubber, ScrubberLocation location = ScrubberLocation.First)
    {
        CurrentSettings.AddScrubber(scrubber, location);
        return this;
    }

    /// <summary>
    /// Rewrite the text of every text target, with the verification's counter and context.
    /// </summary>
    public SettingsTask AddScrubber(Action<StringBuilder, Counter, IReadOnlyDictionary<string, object>> scrubber, ScrubberLocation location = ScrubberLocation.First)
    {
        CurrentSettings.AddScrubber(scrubber, location);
        return this;
    }

    /// <summary>
    /// Rewrite the text of targets with this extension.
    /// </summary>
    public SettingsTask AddScrubber(string extension, Action<StringBuilder> scrubber, ScrubberLocation location = ScrubberLocation.First)
    {
        CurrentSettings.AddScrubber(extension, scrubber, location);
        return this;
    }

    /// <summary>
    /// Rewrite the text of targets with this extension, with the verification's counter.
    /// </summary>
    public SettingsTask AddScrubber(string extension, Action<StringBuilder, Counter> scrubber, ScrubberLocation location = ScrubberLocation.First)
    {
        CurrentSettings.AddScrubber(extension, scrubber, location);
        return this;
    }

    /// <summary>
    /// Remove lines <paramref name="removeLine" /> matches.
    /// </summary>
    public SettingsTask ScrubLines(Func<string, bool> removeLine)
    {
        CurrentSettings.ScrubLines(removeLine);
        return this;
    }

    /// <summary>
    /// Remove lines <paramref name="removeLine" /> matches, in targets with this extension.
    /// </summary>
    public SettingsTask ScrubLines(string extension, Func<string, bool> removeLine)
    {
        CurrentSettings.ScrubLines(extension, removeLine);
        return this;
    }

    /// <summary>
    /// Remove lines that contain any of <paramref name="stringToMatch" />, ignoring case.
    /// </summary>
    public SettingsTask ScrubLinesContaining(params string[] stringToMatch)
    {
        CurrentSettings.ScrubLinesContaining(stringToMatch);
        return this;
    }

    /// <summary>
    /// Remove lines that contain any of <paramref name="stringToMatch" />.
    /// </summary>
    public SettingsTask ScrubLinesContaining(StringComparison comparison, params string[] stringToMatch)
    {
        CurrentSettings.ScrubLinesContaining(comparison, stringToMatch);
        return this;
    }

    /// <summary>
    /// Replace each line with what <paramref name="replaceLine" /> returns. Null removes the line.
    /// </summary>
    public SettingsTask ScrubLinesWithReplace(Func<string, string?> replaceLine)
    {
        CurrentSettings.ScrubLinesWithReplace(replaceLine);
        return this;
    }

    /// <summary>
    /// Replace each line of targets with this extension. Null removes the line.
    /// </summary>
    public SettingsTask ScrubLinesWithReplace(string extension, Func<string, string?> replaceLine)
    {
        CurrentSettings.ScrubLinesWithReplace(extension, replaceLine);
        return this;
    }

    /// <summary>
    /// Remove empty and whitespace-only lines, and a final newline.
    /// </summary>
    public SettingsTask ScrubEmptyLines()
    {
        CurrentSettings.ScrubEmptyLines();
        return this;
    }

    /// <summary>
    /// Replace every occurrence of <paramref name="find" /> with <paramref name="replacement" />.
    /// </summary>
    public SettingsTask ScrubReplace(string find, string replacement, StringComparison comparison = StringComparison.Ordinal, bool requireWordBoundary = false)
    {
        CurrentSettings.ScrubReplace(find, replacement, comparison, requireWordBoundary);
        return this;
    }

    /// <summary>
    /// Replace the machine name with <c>TheMachineName</c>.
    /// </summary>
    public SettingsTask ScrubMachineName()
    {
        CurrentSettings.ScrubMachineName();
        return this;
    }

    /// <summary>
    /// Replace the user name with <c>TheUserName</c>.
    /// </summary>
    public SettingsTask ScrubUserName()
    {
        CurrentSettings.ScrubUserName();
        return this;
    }

    /// <summary>
    /// Also replace Guids inside longer strings and text (<c>Guid_1</c>, ...). A Guid must not touch a letter or
    /// digit on either side.
    /// </summary>
    public SettingsTask ScrubInlineGuids(GuidFormats formats = GuidFormats.All)
    {
        CurrentSettings.ScrubInlineGuids(formats);
        return this;
    }

    /// <summary>
    /// Replace dates in this format inside text with <c>DateTime_1</c>, ...
    /// </summary>
    public SettingsTask ScrubInlineDateTimes(string format, CultureInfo? culture = null)
    {
        CurrentSettings.ScrubInlineDateTimes(format, culture);
        return this;
    }

    /// <summary>
    /// Replace dates in this format inside text with <c>DateTimeOffset_1</c>, ...
    /// </summary>
    public SettingsTask ScrubInlineDateTimeOffsets(string format, CultureInfo? culture = null)
    {
        CurrentSettings.ScrubInlineDateTimeOffsets(format, culture);
        return this;
    }

    /// <summary>
    /// Replace dates in this format inside text with <c>Date_1</c>, ...
    /// </summary>
    public SettingsTask ScrubInlineDates(string format, CultureInfo? culture = null)
    {
        CurrentSettings.ScrubInlineDates(format, culture);
        return this;
    }

    /// <summary>
    /// Replace Guids with <c>Guid_1</c>, ... (the default).
    /// </summary>
    public SettingsTask ScrubGuids()
    {
        CurrentSettings.ScrubGuids();
        return this;
    }

    /// <summary>
    /// Keep Guids as they are.
    /// </summary>
    public SettingsTask DontScrubGuids()
    {
        CurrentSettings.DontScrubGuids();
        return this;
    }

    /// <summary>
    /// Replace dates and times with <c>DateTime_1</c>, ... (the default).
    /// </summary>
    public SettingsTask ScrubDateTimes()
    {
        CurrentSettings.ScrubDateTimes();
        return this;
    }

    /// <summary>
    /// Keep dates and times as they are.
    /// </summary>
    public SettingsTask DontScrubDateTimes()
    {
        CurrentSettings.DontScrubDateTimes();
        return this;
    }

    /// <summary>
    /// Replace numeric members whose name ends with <c>Id</c> with <c>{Name}_1</c>, ...
    /// </summary>
    public SettingsTask ScrubNumericIds()
    {
        CurrentSettings.ScrubNumericIds();
        return this;
    }

    /// <summary>
    /// Keep numeric ids as they are (the default).
    /// </summary>
    public SettingsTask DontScrubNumericIds()
    {
        CurrentSettings.DontScrubNumericIds();
        return this;
    }

    /// <summary>
    /// Write dates as <c>{Scrubbed}</c>, without a number.
    /// </summary>
    public SettingsTask DisableDateCounting()
    {
        CurrentSettings.DisableDateCounting();
        return this;
    }

    /// <summary>
    /// Write this Guid as <paramref name="name" /> wherever it appears.
    /// </summary>
    public SettingsTask AddNamedGuid(Guid value, [CallerArgumentExpression(nameof(value))] string name = "")
    {
        CurrentSettings.AddNamedGuid(value, name);
        return this;
    }

    /// <summary>
    /// Write this DateTime as <paramref name="name" /> wherever it appears.
    /// </summary>
    public SettingsTask AddNamedDateTime(DateTime value, [CallerArgumentExpression(nameof(value))] string name = "")
    {
        CurrentSettings.AddNamedDateTime(value, name);
        return this;
    }

    /// <summary>
    /// Write this DateTimeOffset as <paramref name="name" /> wherever it appears.
    /// </summary>
    public SettingsTask AddNamedDateTimeOffset(DateTimeOffset value, [CallerArgumentExpression(nameof(value))] string name = "")
    {
        CurrentSettings.AddNamedDateTimeOffset(value, name);
        return this;
    }

#if NET6_0_OR_GREATER
    /// <summary>
    /// Write this DateOnly as <paramref name="name" /> wherever it appears.
    /// </summary>
    public SettingsTask AddNamedDate(DateOnly value, [CallerArgumentExpression(nameof(value))] string name = "")
    {
        CurrentSettings.AddNamedDate(value, name);
        return this;
    }

    /// <summary>
    /// Write this TimeOnly as <paramref name="name" /> wherever it appears.
    /// </summary>
    public SettingsTask AddNamedTime(TimeOnly value, [CallerArgumentExpression(nameof(value))] string name = "")
    {
        CurrentSettings.AddNamedTime(value, name);
        return this;
    }

#endif
    /// <summary>
    /// Turn off every scrubber, the built in ones included, for this verification.
    /// </summary>
    public SettingsTask DisableScrubbers()
    {
        CurrentSettings.DisableScrubbers();
        return this;
    }

    /// <summary>
    /// Accept every received file of this verification as verified.
    /// </summary>
    public SettingsTask AutoVerify(bool includeBuildServer = true, bool throwException = false)
    {
        CurrentSettings.AutoVerify(includeBuildServer, throwException);
        return this;
    }

    /// <summary>
    /// Accept the received files <paramref name="autoVerify" /> returns true for.
    /// </summary>
    public SettingsTask AutoVerify(AutoVerify autoVerify, bool includeBuildServer = true, bool throwException = false)
    {
        CurrentSettings.AutoVerify(autoVerify, includeBuildServer, throwException);
        return this;
    }

    /// <summary>
    /// Don't open a diff tool for this verification.
    /// </summary>
    public SettingsTask DisableDiff()
    {
        CurrentSettings.DisableDiff();
        return this;
    }

    /// <summary>
    /// Compare text that is not exactly equal with <paramref name="compare" />, for the given extensions (all when
    /// none are given).
    /// </summary>
    public SettingsTask UseStringComparer(StringCompare compare, params string[] extensions)
    {
        CurrentSettings.UseStringComparer(compare, extensions);
        return this;
    }

    /// <summary>
    /// Compare binary content that is not equal with <paramref name="compare" />, for the given extensions (all
    /// when none are given).
    /// </summary>
    public SettingsTask UseStreamComparer(StreamCompare compare, params string[] extensions)
    {
        CurrentSettings.UseStreamComparer(compare, extensions);
        return this;
    }

    /// <summary>
    /// Run <paramref name="before" /> when the verification starts and <paramref name="after" /> when it ends.
    /// </summary>
    public SettingsTask OnVerify(Action before, Action after)
    {
        CurrentSettings.OnVerify(before, after);
        return this;
    }

    /// <summary>
    /// Called for each target that has no verified file yet.
    /// </summary>
    public SettingsTask OnFirstVerify(FirstVerify firstVerify)
    {
        CurrentSettings.OnFirstVerify(firstVerify);
        return this;
    }

    /// <summary>
    /// Called for each target that does not match.
    /// </summary>
    public SettingsTask OnVerifyMismatch(VerifyMismatch verifyMismatch)
    {
        CurrentSettings.OnVerifyMismatch(verifyMismatch);
        return this;
    }

    /// <summary>
    /// Called for each verified file the verification no longer produces.
    /// </summary>
    public SettingsTask OnDelete(VerifyDelete verifyDelete)
    {
        CurrentSettings.OnDelete(verifyDelete);
        return this;
    }

    /// <summary>
    /// Add a named value to the snapshot. The verified value moves under <c>target</c>.
    /// </summary>
    public SettingsTask AppendValue(string name, object? data)
    {
        CurrentSettings.AppendValue(name, data);
        return this;
    }

    /// <summary>
    /// Add named values to the snapshot.
    /// </summary>
    public SettingsTask AppendValues(params KeyValuePair<string, object?>[] values)
    {
        CurrentSettings.AppendValues(values);
        return this;
    }

    /// <summary>
    /// Add named values to the snapshot.
    /// </summary>
    public SettingsTask AppendValues(IEnumerable<KeyValuePair<string, object?>> values)
    {
        CurrentSettings.AppendValues(values);
        return this;
    }

    /// <summary>
    /// Verify a file alongside the target. Its name defaults to the file name without extension.
    /// </summary>
    public SettingsTask AppendFile(string path, string? name = null)
    {
        CurrentSettings.AppendFile(path, name);
        return this;
    }

    /// <summary>
    /// Verify a stream alongside the target.
    /// </summary>
    public SettingsTask AppendFile(Stream stream, string extension = "txt", string? name = null)
    {
        CurrentSettings.AppendFile(stream, extension, name);
        return this;
    }

    /// <summary>
    /// Verify text alongside the target, as its own file.
    /// </summary>
    public SettingsTask AppendContentAsFile(string content, string extension = "txt", string? name = null)
    {
        CurrentSettings.AppendContentAsFile(content, extension, name);
        return this;
    }

    /// <summary>
    /// Verify bytes alongside the target, as their own file.
    /// </summary>
    public SettingsTask AppendContentAsFile(byte[] content, string extension = "txt", string? name = null)
    {
        CurrentSettings.AppendContentAsFile(content, extension, name);
        return this;
    }
}
