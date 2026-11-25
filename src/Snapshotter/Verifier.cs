using System.Reflection;
using System.Runtime.CompilerServices;

namespace Snapshotter;

/// <summary>
/// Snapshot verification that does not depend on any test framework.
/// </summary>
/// <remarks>
/// Each method serializes its target, writes it to <c>{name}.received.{ext}</c> next to the calling
/// source file and compares it with the committed <c>{name}.verified.{ext}</c>. When they match the
/// received file is removed. When they don't, a <see cref="SnapshotMismatchException" /> is thrown, which
/// any test framework, or any other host, reports as a failure.
/// <para>
/// The name is <c>{type}.{method}</c>: the type is the source file name up to its first dot, and the
/// method is the calling member, both supplied by the compiler. So <c>Totals()</c> in <c>OrderTests.cs</c>
/// is verified against <c>OrderTests.Totals.verified.json</c>.
/// </para>
/// </remarks>
public static class Verifier
{
    /// <summary>
    /// Verifies <paramref name="target" />, serialized as indented JSON (<c>.json</c>).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SnapshotTask Verify(
        object? target,
        SnapshotSettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName,
            s => Task.FromResult(Serialization.FromObject(target, s)));

    /// <summary>
    /// Verifies <paramref name="target" /> as text (<c>.txt</c>, or the extension set with
    /// <see cref="SnapshotTask.UseExtension" />).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SnapshotTask Verify(
        string? target,
        SnapshotSettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName,
            s => Task.FromResult(Serialization.FromText(target ?? "null", s)));

    /// <summary>
    /// Awaits <paramref name="target" /> and verifies its result.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SnapshotTask Verify<T>(
        Task<T> target,
        SnapshotSettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName,
            async s => Serialization.FromObject(await target.ConfigureAwait(false), s));

    /// <summary>
    /// Verifies raw bytes, compared exactly and stored with <paramref name="extension" />.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SnapshotTask Verify(
        byte[] target,
        string extension,
        SnapshotSettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName,
            _ => Task.FromResult(Payload.FromBytes(target, CleanExtension(extension))));

    /// <summary>
    /// Reads <paramref name="target" /> to the end and verifies the bytes, stored with <paramref name="extension" />.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SnapshotTask Verify(
        Stream target,
        string extension,
        SnapshotSettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName,
            async _ =>
            {
                using var memory = new MemoryStream();
                await target.CopyToAsync(memory).ConfigureAwait(false);
                return Payload.FromBytes(memory.ToArray(), CleanExtension(extension));
            });

    /// <summary>
    /// Verifies a JSON document, re-indented and scrubbed (<c>.json</c>). Property order is kept.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SnapshotTask VerifyJson(
        string json,
        SnapshotSettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName,
            s => Task.FromResult(Serialization.FromJson(json, s)));

    /// <summary>
    /// Verifies the contents of a file, keeping its extension. Known text formats are compared as text
    /// (with scrubbers applied); anything else byte for byte.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SnapshotTask VerifyFile(
        string path,
        SnapshotSettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName,
            s => Task.FromResult(FileTarget(path, s)));

    /// <summary>
    /// Runs <paramref name="action" />, which must throw, and verifies the exception: its type, message
    /// and properties, without the stack trace.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SnapshotTask Throws(
        Action action,
        SnapshotSettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName,
            s =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    return Task.FromResult(Serialization.FromObject(exception, s));
                }

                throw new SnapshotterException("Throws(...) expected the action to throw an exception, but it completed.");
            });

    /// <summary>
    /// Awaits <paramref name="action" />, which must fail, and verifies the exception.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SnapshotTask ThrowsAsync(
        Func<Task> action,
        SnapshotSettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string memberName = "") =>
        Create(settings, Assembly.GetCallingAssembly(), sourceFile, memberName,
            async s =>
            {
                try
                {
                    await action().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    return Serialization.FromObject(exception, s);
                }

                throw new SnapshotterException("ThrowsAsync(...) expected the task to fail, but it completed.");
            });

    static SnapshotTask Create(
        SnapshotSettings? settings,
        Assembly assembly,
        string sourceFile,
        string memberName,
        Func<SnapshotSettings, Task<Payload>> produce)
    {
        var site = new CallSite(sourceFile, memberName, assembly);
        return new((settings ?? SnapshotSettings.Global).Clone(), s => Engine.Run(s, site, produce));
    }

    static readonly HashSet<string> textExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "txt", "json", "xml", "html", "htm", "csv", "tsv", "md", "yml", "yaml", "svg", "css", "js", "ts",
        "sql", "config", "ini", "log", "cs", "fs", "vb", "props", "targets", "csproj", "graphql", "toml"
    };

    static Payload FileTarget(string path, SnapshotSettings settings)
    {
        var extension = Path.GetExtension(path).TrimStart('.');
        if (extension.Length == 0)
        {
            extension = "bin";
        }

        if (!textExtensions.Contains(extension))
        {
            return Payload.FromBytes(File.ReadAllBytes(path), extension);
        }

        var text = settings.Clone();
        text.Extension = extension;
        return Serialization.FromText(File.ReadAllText(path), text);
    }

    static string CleanExtension(string extension)
    {
        extension = extension.TrimStart('.');
        if (extension.Length == 0)
        {
            throw new ArgumentException("An extension is required, such as \"png\".", nameof(extension));
        }

        return extension;
    }
}

/// <summary>
/// Snapshotter was used in a way it cannot work with, such as two verifications with the same name.
/// </summary>
public class SnapshotterException(string message) : Exception(message);

/// <summary>
/// The received result does not match the verified snapshot, or there is no verified snapshot yet.
/// </summary>
public sealed class SnapshotMismatchException(string message, string verifiedPath, string receivedPath)
    : SnapshotterException(message)
{
    /// <summary>
    /// The committed snapshot, which may not exist yet.
    /// </summary>
    public string VerifiedPath { get; } = verifiedPath;

    /// <summary>
    /// The result of this run.
    /// </summary>
    public string ReceivedPath { get; } = receivedPath;
}
