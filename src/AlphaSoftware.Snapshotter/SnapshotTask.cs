using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace AlphaSoftware.Snapshotter;

/// <summary>
/// A verification that has not run yet. Configure it with the <c>Use...</c>, <c>Scrub...</c> and
/// <c>Ignore...</c> methods, then await it. It also converts to <see cref="Task" />, so a test can
/// return it directly.
/// </summary>
public sealed class SnapshotTask
{
    readonly Func<SnapshotSettings, Task> run;
    Task? task;

    internal SnapshotTask(SnapshotSettings settings, Func<SnapshotSettings, Task> run)
    {
        Settings = settings;
        this.run = run;
    }

    /// <summary>
    /// The settings of this verification. Changes after it has started have no effect.
    /// </summary>
    public SnapshotSettings Settings { get; }

    /// <inheritdoc cref="SnapshotSettings.Directory" />
    public SnapshotTask UseDirectory(string directory) =>
        Configure(_ => _.Directory = directory);

    /// <inheritdoc cref="SnapshotSettings.FileName" />
    public SnapshotTask UseFileName(string fileName) =>
        Configure(_ => _.FileName = fileName);

    /// <inheritdoc cref="SnapshotSettings.TypeName" />
    public SnapshotTask UseTypeName(string typeName) =>
        Configure(_ => _.TypeName = typeName);

    /// <inheritdoc cref="SnapshotSettings.MethodName" />
    public SnapshotTask UseMethodName(string methodName) =>
        Configure(_ => _.MethodName = methodName);

    /// <inheritdoc cref="SnapshotSettings.Parameters" />
    public SnapshotTask UseParameters(params object?[] parameters) =>
        Configure(_ => _.Parameters = parameters);

    /// <inheritdoc cref="SnapshotSettings.ParametersText" />
    public SnapshotTask UseTextForParameters(string text) =>
        Configure(_ => _.ParametersText = text);

    /// <inheritdoc cref="SnapshotSettings.Extension" />
    public SnapshotTask UseExtension(string extension) =>
        Configure(_ => _.Extension = extension);

    /// <summary>
    /// Keep GUID values as they are.
    /// </summary>
    public SnapshotTask DontScrubGuids() =>
        Configure(_ => _.ScrubGuids = false);

    /// <summary>
    /// Keep date and time values as they are.
    /// </summary>
    public SnapshotTask DontScrubDates() =>
        Configure(_ => _.ScrubDates = false);

    /// <inheritdoc cref="SnapshotSettings.ScrubInlineGuids" />
    public SnapshotTask ScrubInlineGuids() =>
        Configure(_ => _.ScrubInlineGuids = true);

    /// <inheritdoc cref="SnapshotSettings.IgnoreNullValues" />
    public SnapshotTask IgnoreNullValues() =>
        Configure(_ => _.IgnoreNullValues = true);

    /// <inheritdoc cref="SnapshotSettings.AutoAccept" />
    public SnapshotTask AutoAccept() =>
        Configure(_ => _.AutoAccept = true);

    /// <inheritdoc cref="SnapshotSettings.IgnoreMember(string)" />
    public SnapshotTask IgnoreMember(string name) =>
        Configure(_ => _.IgnoreMember(name));

    /// <inheritdoc cref="SnapshotSettings.IgnoreMember{T}(Expression{Func{T, object}})" />
    public SnapshotTask IgnoreMember<T>(Expression<Func<T, object?>> member) =>
        Configure(_ => _.IgnoreMember(member));

    /// <inheritdoc cref="SnapshotSettings.AddScrubber" />
    public SnapshotTask AddScrubber(Func<string, string> scrubber) =>
        Configure(_ => _.AddScrubber(scrubber));

    /// <inheritdoc cref="SnapshotSettings.ScrubLinesContaining" />
    public SnapshotTask ScrubLinesContaining(params string[] values) =>
        Configure(_ => _.ScrubLinesContaining(values));

    /// <inheritdoc cref="SnapshotSettings.ScrubLines" />
    public SnapshotTask ScrubLines(Func<string, bool> remove) =>
        Configure(_ => _.ScrubLines(remove));

    /// <summary>
    /// Add a converter to the JSON serializer for this verification.
    /// </summary>
    public SnapshotTask AddConverter(JsonConverter converter) =>
        Configure(_ => _.Converters.Add(converter));

    /// <summary>
    /// Change any other setting.
    /// </summary>
    public SnapshotTask Configure(Action<SnapshotSettings> configure)
    {
        if (task != null)
        {
            throw new InvalidOperationException("This verification has already started. Configure it before awaiting it.");
        }

        configure(Settings);
        return this;
    }

    /// <summary>
    /// Runs the verification, once.
    /// </summary>
    public Task ToTask() =>
        task ??= run(Settings);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public TaskAwaiter GetAwaiter() =>
        ToTask().GetAwaiter();

    public ConfiguredTaskAwaitable ConfigureAwait(bool continueOnCapturedContext) =>
        ToTask().ConfigureAwait(continueOnCapturedContext);

    public static implicit operator Task(SnapshotTask snapshot) =>
        snapshot.ToTask();
}
