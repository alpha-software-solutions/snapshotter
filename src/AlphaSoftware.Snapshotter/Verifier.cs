using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace AlphaSoftware.Snapshotter;

/// <summary>
/// Snapshot verification that works with any test framework, or none.
/// </summary>
/// <remarks>
/// Each method serializes its target, scrubs it and compares it with <c>{Type}.{Method}.verified.{ext}</c> next to
/// the calling source file. A difference writes <c>.received.</c> and throws a <see cref="VerifyException" />, which
/// any test framework, console app or CI step reports as a failure.
/// </remarks>
public static class Verifier
{
    #region Objects and text

    /// <summary>
    /// Verifies <paramref name="target" />: strings, numbers and other simple values as text, anything else
    /// serialized.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask Verify(object? target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, serializer => Task.FromResult(Targets.FromObject(target, serializer, null)));

    /// <summary>
    /// Verifies the targets added with <c>AppendValue</c> and <c>AppendFile</c> only.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask Verify(VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, serializer => Task.FromResult(Targets.FromObject(Targets.NoTarget, serializer, null)));

    /// <summary>
    /// Verifies text, stored as <c>.txt</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask Verify(string? target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, serializer => Task.FromResult(Targets.FromObject(target, serializer, null)));

    /// <summary>
    /// Verifies text stored with <paramref name="extension" />, such as <c>html</c> or <c>md</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask Verify(string? target, string extension, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, serializer =>
        {
            extension = Target.CleanExtension(extension);
            if (!FileExtensions.IsText(extension))
            {
                throw new Exception($"Don't pass text for a binary extension. Either call FileExtensions.AddTextExtension(\"{extension}\") or pass the content as a Stream or byte[].");
            }

            return Task.FromResult(Targets.FromObject(target, serializer, extension));
        });

    /// <summary>
    /// Awaits <paramref name="target" /> and verifies the text.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask Verify(Task<string> target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, async serializer => Targets.FromObject(await target.ConfigureAwait(false), serializer, null));

    /// <summary>
    /// Awaits <paramref name="target" /> and verifies the result.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask Verify<T>(Task<T> target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, async serializer => Targets.FromObject(await target.ConfigureAwait(false), serializer, null));

    /// <summary>
    /// Awaits <paramref name="target" /> and verifies the result.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask Verify<T>(ValueTask<T> target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, async serializer => Targets.FromObject(await target.ConfigureAwait(false), serializer, null));

    /// <summary>
    /// Runs <paramref name="target" /> and verifies its result.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask Verify<T>(Func<Task<T>> target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, async serializer => Targets.FromObject(await target().ConfigureAwait(false), serializer, null));

#if NET6_0_OR_GREATER
    /// <summary>
    /// Reads every item of <paramref name="target" /> and verifies the list.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask Verify<T>(IAsyncEnumerable<T> target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, async serializer =>
        {
            var items = new List<T>();
            await foreach (var item in target.ConfigureAwait(false))
            {
                items.Add(item);
            }

            return Targets.FromObject(items, serializer, null);
        });
#endif

    /// <summary>
    /// Verifies JSON, re-indented in the snapshot format with its property order kept. Member ignore and scrub
    /// rules apply to property names, and Guid and date strings are scrubbed.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask VerifyJson(string? target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, serializer => Task.FromResult(Targets.FromJson(target, serializer)));

    /// <inheritdoc cref="VerifyJson(string, VerifySettings, string, string)" />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask VerifyJson(Task<string> target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, async serializer => Targets.FromJson(await target.ConfigureAwait(false), serializer));

    /// <inheritdoc cref="VerifyJson(string, VerifySettings, string, string)" />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask VerifyJson(Stream? target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, async serializer =>
            Targets.FromJson(target == null ? null : await Targets.ReadText(target).ConfigureAwait(false), serializer));

    /// <summary>
    /// Verifies XML, pretty printed without the declaration (<c>.xml</c>). Member ignore and scrub rules apply to
    /// element and attribute names.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask VerifyXml(string? target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, serializer =>
            Task.FromResult(Targets.FromXml(target == null ? null : XDocument.Parse(target), serializer)));

    /// <inheritdoc cref="VerifyXml(string, VerifySettings, string, string)" />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask VerifyXml(XDocument? target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, serializer => Task.FromResult(Targets.FromXml(target, serializer)));

    /// <inheritdoc cref="VerifyXml(string, VerifySettings, string, string)" />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask VerifyXml(Stream? target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, serializer =>
            Task.FromResult(Targets.FromXml(target == null ? null : XDocument.Load(target), serializer)));

    #endregion

    #region Binary and files

    /// <summary>
    /// Verifies bytes, compared exactly, as <c>.bin</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask Verify(byte[]? target, VerifySettings? settings = null, object? info = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, serializer => Task.FromResult(Targets.FromBytes(target, "bin", info, serializer)));

    /// <summary>
    /// Verifies bytes stored with <paramref name="extension" />. A text extension is compared as text.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask Verify(byte[]? target, string extension, VerifySettings? settings = null, object? info = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, serializer => Task.FromResult(Targets.FromBytes(target, extension, info, serializer)));

    /// <summary>
    /// Reads <paramref name="target" /> from the start and verifies its bytes, as <c>.bin</c> (or the file's
    /// extension for a <see cref="FileStream" />).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask Verify(Stream? target, VerifySettings? settings = null, object? info = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, async serializer =>
        {
            var extension = target is FileStream file && Path.GetExtension(file.Name).Length > 1 ? Path.GetExtension(file.Name).TrimStart('.') : "bin";
            return Targets.FromBytes(target == null ? null : await Targets.ReadBytes(target).ConfigureAwait(false), extension, info, serializer);
        });

    /// <summary>
    /// Reads <paramref name="target" /> from the start and verifies it, stored with <paramref name="extension" />.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask Verify(Stream? target, string extension, VerifySettings? settings = null, object? info = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, async serializer =>
            Targets.FromBytes(target == null ? null : await Targets.ReadBytes(target).ConfigureAwait(false), extension, info, serializer));

    /// <summary>
    /// Verifies the content of a file, keeping its extension. Text formats are compared as text, anything else byte
    /// for byte.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask VerifyFile(string path, VerifySettings? settings = null, object? info = null, string? extension = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, serializer => Task.FromResult(Targets.FromFile(path, extension, info, serializer)));

    /// <inheritdoc cref="VerifyFile(string, VerifySettings, object, string, string, string)" />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask VerifyFile(FileInfo path, VerifySettings? settings = null, object? info = null, string? extension = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, serializer => Task.FromResult(Targets.FromFile(path.FullName, extension, info, serializer)));

    /// <summary>
    /// Verifies every file in a directory, each as its own snapshot in a directory named after the test.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask VerifyDirectory(string path, Func<string, bool>? include = null, string? pattern = null, SearchOption option = SearchOption.AllDirectories, VerifySettings? settings = null, object? info = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "")
    {
        var task = Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, serializer => Task.FromResult(Targets.FromDirectory(path, include, pattern, option, info, serializer)));
        task.CurrentSettings.UseUniqueDirectory();
        return task;
    }

    /// <summary>
    /// Verifies explicit targets.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask Verify(Target target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, async serializer => await Targets.FromTargets(Targets.NoTarget, [target], serializer).ConfigureAwait(false));

    /// <inheritdoc cref="Verify(Target, VerifySettings, string, string)" />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask Verify(IEnumerable<Target> targets, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, async serializer => await Targets.FromTargets(Targets.NoTarget, targets, serializer).ConfigureAwait(false));

    /// <summary>
    /// Verifies <paramref name="target" /> serialized, plus explicit raw targets.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask Verify(object? target, IEnumerable<Target> rawTargets, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, async serializer => await Targets.FromTargets(target, rawTargets, serializer).ConfigureAwait(false));

    #endregion

    #region Exceptions

    /// <summary>
    /// Runs <paramref name="target" />, which must throw, and verifies the exception.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask Throws(Action target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, serializer =>
        {
            try
            {
                target();
            }
            catch (Exception exception)
            {
                return Task.FromResult(Targets.FromObject(exception, serializer, null));
            }

            throw new Exception("Did not throw.");
        });

    /// <inheritdoc cref="Throws(Action, VerifySettings, string, string)" />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask Throws(Func<object?> target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, serializer =>
        {
            try
            {
                target();
            }
            catch (Exception exception)
            {
                return Task.FromResult(Targets.FromObject(exception, serializer, null));
            }

            throw new Exception("Did not throw.");
        });

    /// <summary>
    /// Awaits <paramref name="target" />, which must fail, and verifies the exception.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask ThrowsTask(Func<Task> target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName, async serializer =>
        {
            try
            {
                await target().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                return Targets.FromObject(exception, serializer, null);
            }

            throw new Exception("Did not throw.");
        });

    /// <inheritdoc cref="ThrowsTask(Func{Task}, VerifySettings, string, string)" />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask ThrowsTask<T>(Func<Task<T>> target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        ThrowsTask(() => (Task)target(), settings, sourceFile, memberName);

    /// <inheritdoc cref="ThrowsTask(Func{Task}, VerifySettings, string, string)" />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask ThrowsValueTask(Func<ValueTask> target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        ThrowsTask(() => target().AsTask(), settings, sourceFile, memberName);

    /// <inheritdoc cref="ThrowsTask(Func{Task}, VerifySettings, string, string)" />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SettingsTask ThrowsValueTask<T>(Func<ValueTask<T>> target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "", [CallerMemberName] string memberName = "") =>
        ThrowsTask(() => target().AsTask(), settings, sourceFile, memberName);

    #endregion

    #region Path conventions

    /// <summary>
    /// Work out every snapshot's directory and names with <paramref name="derivePathInfo" />. Call it before the
    /// first verification.
    /// </summary>
    public static void DerivePathInfo(DerivePathInfo derivePathInfo)
    {
        VerifierSettings.Guard(nameof(DerivePathInfo));
        VerifierSettings.PathInfoDeriver = derivePathInfo;
    }

    /// <summary>
    /// Store snapshots in <paramref name="directory" /> under the source file's directory.
    /// </summary>
    public static void UseSourceFileRelativeDirectory(string directory) =>
        DerivePathInfo((sourceFile, _, type, method) => new(
            Path.Combine(Path.GetDirectoryName(sourceFile)!, directory),
            NameWithParent(type),
            method.Name));

    /// <summary>
    /// Store snapshots in <paramref name="directory" /> under the project directory, optionally mirroring the source
    /// file's folders.
    /// </summary>
    public static void UseProjectRelativeDirectory(string directory, bool mirrorSourceStructure = false) =>
        DerivePathInfo((sourceFile, projectDirectory, type, method) =>
        {
            var target = Path.Combine(projectDirectory, directory);
            if (mirrorSourceStructure && projectDirectory.Length > 0)
            {
                var sourceDirectory = Path.GetDirectoryName(sourceFile)!;
                var root = projectDirectory.TrimEnd('/', '\\');
                if (sourceDirectory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    target = Path.Combine(target, sourceDirectory.Substring(root.Length).TrimStart('/', '\\'));
                }
            }

            return new(target, NameWithParent(type), method.Name);
        });

    static string NameWithParent(Type type) =>
        type.IsNested && type.DeclaringType != null ? $"{type.DeclaringType.Name}.{type.Name}" : type.Name;

    #endregion

    static SettingsTask Create(VerifySettings? settings, Assembly assembly, string sourceFile, string memberName, Func<Serializer, Task<Produced>> produce)
    {
        var site = new CallSite(sourceFile, memberName, assembly);
        return new(new VerifySettings(settings), current => Engine.Run(current, site, produce));
    }
}

/// <summary>
/// Turns what was passed to <c>Verify</c> into files.
/// </summary>
static class Targets
{
    internal static readonly object NoTarget = new();

    public static Produced FromObject(object? target, Serializer serializer, string? extension, bool appendFiles = true)
    {
        var settings = serializer.Settings.Instance;
        var appended = settings.AppendedValues;
        var targets = new List<ProducedTarget>();
        var strictExtension = serializer.Settings.Strict ? "json" : "txt";

        if (appended.Count > 0)
        {
            var obj = new ObjectNode();
            if (!ReferenceEquals(target, NoTarget))
            {
                var asText = AsText(target, settings);
                obj.Add("target", asText != null ? ValueNode.String(asText.Value.Text) : serializer.Root(target));
            }

            foreach (var pair in appended)
            {
                obj.Add(pair.Key, serializer.Value(pair.Value, pair.Value?.GetType() ?? typeof(object), ValueContext.Item) ?? ValueNode.Null);
            }

            targets.Add(new(extension ?? strictExtension, NodeWriter.Write(obj, serializer.Settings.Strict), null, null));
        }
        else if (!ReferenceEquals(target, NoTarget))
        {
            var asText = AsText(target, settings);
            targets.Add(asText != null
                ? new(extension ?? asText.Value.Extension ?? "txt", asText.Value.Text, null, null)
                : new(extension ?? strictExtension, NodeWriter.Write(serializer.Root(target), serializer.Settings.Strict), null, null));
        }

        if (appendFiles)
        {
            AddAppendedFiles(targets, settings);
        }

        return new(targets, ReferenceEquals(target, NoTarget) ? null : target);
    }

    // Values written as text rather than serialized, as Verify does for its root target.
    static (string Text, string? Extension)? AsText(object? target, VerifySettings settings)
    {
        var invariant = CultureInfo.InvariantCulture;
        if (target == null)
        {
            return ("null", null);
        }

        var type = target.GetType();
        foreach (var (conversionType, inherit, convert) in VerifierSettings.StringConversions)
        {
            if (conversionType == type || (inherit && conversionType.IsAssignableFrom(type)))
            {
                var result = convert(target, settings.Context);
                return (result.Value, result.Extension);
            }
        }

        return target switch
        {
            string text => (text.Length == 0 ? "emptyString" : text, null),
            StringBuilder builder => (builder.ToString(), null),
            StringWriter writer => (writer.ToString(), null),
            bool boolean => (boolean.ToString(), null),
            Guid guid => (guid.ToString("D"), null),
            DateTime dateTime => (Formatting.DateTime(dateTime), null),
            DateTimeOffset offset => (Formatting.DateTimeOffset(offset), null),
#if NET6_0_OR_GREATER
            DateOnly date => (Formatting.Date(date), null),
            TimeOnly time => (Formatting.Time(time), null),
            Half half => (half.ToString(invariant), null),
#endif
            XDocument document => (document.ToString(), "xml"),
            XElement element => (element.ToString(), "xml"),
            XmlNode node => (XDocument.Parse(node.OuterXml).ToString(), "xml"),
            int or long or short or byte or sbyte or uint or ulong or ushort or decimal or double or float or System.Numerics.BigInteger =>
                (((IFormattable)target).ToString(null, invariant), null),
            _ => null
        };
    }

    public static Produced FromJson(string? json, Serializer serializer)
    {
        if (json == null)
        {
            return FromObject(null, serializer, null);
        }

        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        var node = serializer.Value(document.RootElement, typeof(JsonElement), ValueContext.Root) ?? ValueNode.Null;
        var targets = new List<ProducedTarget> { new(serializer.Settings.Strict ? "json" : "txt", NodeWriter.Write(node, serializer.Settings.Strict), null, null) };
        AddAppendedFiles(targets, serializer.Settings.Instance);
        return new(targets, json);
    }

    public static Produced FromXml(XDocument? document, Serializer serializer)
    {
        if (document == null)
        {
            return FromObject(null, serializer, null);
        }

        var rules = serializer.Rules;
        var counter = Counter.Current;
        var settings = serializer.Settings;

        string Scrub(string value) =>
            !settings.ScrubbersDisabled && counter.TryConvert(value, settings.ScrubGuids, settings.ScrubDates, out var replaced) ? replaced : value;

        foreach (var element in document.Descendants().ToList())
        {
            switch (rules.ForName(element.Name.LocalName))
            {
                case MemberDecision.Ignore:
                    element.Remove();
                    continue;
                case MemberDecision.Scrub:
                    element.ReplaceNodes(new XText("Scrubbed"));
                    break;
                default:
                    foreach (var text in element.Nodes().OfType<XText>())
                    {
                        text.Value = Scrub(text.Value);
                    }

                    break;
            }

            foreach (var attribute in element.Attributes().ToList())
            {
                switch (rules.ForName(attribute.Name.LocalName))
                {
                    case MemberDecision.Ignore:
                        attribute.Remove();
                        break;
                    case MemberDecision.Scrub:
                        attribute.Value = "Scrubbed";
                        break;
                    default:
                        attribute.Value = Scrub(attribute.Value);
                        break;
                }
            }
        }

        var targets = new List<ProducedTarget> { new("xml", document.ToString(), null, null) };
        AddAppendedFiles(targets, serializer.Settings.Instance);
        return new(targets, document);
    }

    public static Produced FromBytes(byte[]? bytes, string extension, object? info, Serializer serializer)
    {
        extension = Target.CleanExtension(extension);
        if (bytes == null)
        {
            return FromObject(null, serializer, null);
        }

        var targets = new List<ProducedTarget>();
        AddInfo(targets, info, serializer);
        if (FileExtensions.IsText(extension))
        {
            targets.Add(new(extension, DecodeText(bytes), null, null));
        }
        else
        {
            if (bytes.Length == 0)
            {
                throw new Exception("Empty data is not allowed.");
            }

            targets.Add(new(extension, null, bytes, null));
        }

        AddAppendedFiles(targets, serializer.Settings.Instance);
        return new(targets, bytes);
    }

    public static Produced FromFile(string path, string? extension, object? info, Serializer serializer)
    {
        extension ??= Path.GetExtension(path).TrimStart('.');
        if (extension.Length == 0)
        {
            extension = "noextension";
        }

        var bytes = File.ReadAllBytes(path);
        if (!FileExtensions.IsText(extension) && bytes.Length == 0)
        {
            throw new Exception($"Empty data is not allowed. Path: {path}");
        }

        return FromBytes(bytes, extension, info, serializer);
    }

    public static Produced FromDirectory(string path, Func<string, bool>? include, string? pattern, SearchOption option, object? info, Serializer serializer)
    {
        var root = Path.GetFullPath(path).TrimEnd('/', '\\');
        var targets = new List<ProducedTarget>();
        AddInfo(targets, info, serializer);
        foreach (var file in Directory.GetFiles(root, pattern ?? "*", option).OrderBy(file => file, StringComparer.Ordinal))
        {
            if (include != null && !include(file))
            {
                continue;
            }

            var relative = file.Substring(root.Length + 1);
            var extension = Path.GetExtension(relative).TrimStart('.');
            var name = (Path.GetDirectoryName(relative) is { Length: > 0 } folder ? folder.Replace('\\', '/') + "/" : "") + Path.GetFileNameWithoutExtension(relative);
            if (extension.Length == 0)
            {
                extension = "noextension";
            }

            var bytes = File.ReadAllBytes(file);
            targets.Add(FileExtensions.IsText(extension)
                ? new(extension, DecodeText(bytes), null, name)
                : new(extension, null, bytes, name));
        }

        AddAppendedFiles(targets, serializer.Settings.Instance);
        return new(targets, path);
    }

    public static async Task<Produced> FromTargets(object? target, IEnumerable<Target> rawTargets, Serializer serializer)
    {
        var settings = serializer.Settings.Instance;
        var targets = ReferenceEquals(target, NoTarget) && settings.AppendedValues.Count == 0
            ? []
            : FromObject(target, serializer, null, appendFiles: false).Targets;
        foreach (var raw in rawTargets)
        {
            targets.Add(await Convert(raw).ConfigureAwait(false));
        }

        AddAppendedFiles(targets, settings);
        return new(targets, ReferenceEquals(target, NoTarget) ? null : target);
    }

    static void AddInfo(List<ProducedTarget> targets, object? info, Serializer serializer)
    {
        if (info == null && serializer.Settings.Instance.AppendedValues.Count == 0)
        {
            return;
        }

        targets.AddRange(FromObject(info ?? NoTarget, serializer, null, appendFiles: false).Targets);
    }

    static void AddAppendedFiles(List<ProducedTarget> targets, VerifySettings settings)
    {
        foreach (var file in settings.AppendedFiles)
        {
            targets.Add(Convert(file).GetAwaiter().GetResult());
        }
    }

    static async Task<ProducedTarget> Convert(Target target)
    {
        if (target.IsString)
        {
            if (!FileExtensions.IsText(target.Extension))
            {
                throw new Exception($"Don't pass text for a binary extension. Either call FileExtensions.AddTextExtension(\"{target.Extension}\") or use the Stream constructor of Target.");
            }

            return new(target.Extension, target.StringData, null, target.Name);
        }

        var bytes = await ReadBytes(target.StreamData!).ConfigureAwait(false);
        return FileExtensions.IsText(target.Extension)
            ? new(target.Extension, DecodeText(bytes), null, target.Name)
            : new(target.Extension, null, bytes, target.Name);
    }

    internal static async Task<byte[]> ReadBytes(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory).ConfigureAwait(false);
        return memory.ToArray();
    }

    internal static async Task<string> ReadText(Stream stream) =>
        DecodeText(await ReadBytes(stream).ConfigureAwait(false));

    static string DecodeText(byte[] bytes)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var offset = bytes.Length >= preamble.Length && bytes.AsSpan(0, preamble.Length).SequenceEqual(preamble) ? preamble.Length : 0;
        return Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
    }
}
