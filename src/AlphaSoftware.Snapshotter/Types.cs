using System.Reflection;

namespace AlphaSoftware.Snapshotter;

/// <summary>
/// Decides whether a verified file of this verification is accepted automatically.
/// </summary>
public delegate bool AutoVerify(string verifiedFile);

/// <summary>
/// Decides, for every verification, whether a verified file is accepted automatically.
/// </summary>
public delegate bool GlobalAutoVerify(string typeName, string methodName, string verifiedFile);

/// <summary>
/// Called for a target that has no verified file yet, after its received file is written.
/// </summary>
public delegate Task FirstVerify(FilePair filePair, string? receivedText, bool autoVerify);

/// <summary>
/// Called for a target that does not match its verified file.
/// </summary>
public delegate Task VerifyMismatch(FilePair filePair, string? message, bool autoVerify);

/// <summary>
/// Called for a verified file this verification no longer produces.
/// </summary>
public delegate Task VerifyDelete(string file, bool autoVerify);

/// <summary>
/// Compares text that is not exactly equal, for example to allow small differences.
/// </summary>
public delegate Task<CompareResult> StringCompare(string received, string verified, IReadOnlyDictionary<string, object> context);

/// <summary>
/// Compares binary content that is not byte for byte equal.
/// </summary>
public delegate Task<CompareResult> StreamCompare(Stream received, Stream verified, IReadOnlyDictionary<string, object> context);

/// <summary>
/// Works out where snapshots of a test are stored and how they are named.
/// </summary>
public delegate PathInfo DerivePathInfo(string sourceFile, string projectDirectory, Type type, MethodInfo method);

/// <summary>
/// Turns a root value of <typeparamref name="T" /> into text, registered with
/// <see cref="VerifierSettings.TreatAsString{T}" />.
/// </summary>
public delegate AsStringResult AsString<in T>(T target, IReadOnlyDictionary<string, object> context);

/// <summary>
/// Turns a parameter value of <typeparamref name="T" /> into file name text.
/// </summary>
public delegate string ParameterToName<in T>(T value);

/// <summary>
/// The text a <see cref="AsString{T}" /> conversion produced, and optionally the extension to store it with.
/// </summary>
public readonly struct AsStringResult(string value, string? extension = null)
{
    public string Value { get; } = value;
    public string? Extension { get; } = extension;

    public static implicit operator AsStringResult(string value) => new(value);
}

/// <summary>
/// A received file and the verified file it is compared with.
/// </summary>
public readonly struct FilePair(string extension, string receivedPath, string verifiedPath, bool isText)
{
    public string Extension { get; } = extension;
    public string ReceivedPath { get; } = receivedPath;
    public string VerifiedPath { get; } = verifiedPath;
    public bool IsText { get; } = isText;
}

/// <summary>
/// The outcome of a custom comparison.
/// </summary>
public readonly struct CompareResult
{
    CompareResult(bool isEqual, string? message)
    {
        IsEqual = isEqual;
        Message = message;
    }

    public bool IsEqual { get; }

    /// <summary>
    /// Shown in the exception message when the files differ.
    /// </summary>
    public string? Message { get; }

    public static CompareResult Equal => new(true, null);

    public static CompareResult NotEqual(string? message = null) => new(false, message);
}

/// <summary>
/// Overrides parts of a snapshot's location. A null part keeps the default.
/// </summary>
public readonly struct PathInfo(string? directory = null, string? typeName = null, string? methodName = null)
{
    public string? Directory { get; } = directory;
    public string? TypeName { get; } = typeName;
    public string? MethodName { get; } = methodName;
}

/// <summary>
/// One file of a verification: text or binary content with its extension and an optional name.
/// </summary>
public readonly struct Target
{
    public Target(string extension, string data, string? name = null)
    {
        Extension = CleanExtension(extension);
        StringData = data;
        StreamData = null;
        Name = name;
    }

    public Target(string extension, System.Text.StringBuilder data, string? name = null)
        : this(extension, data.ToString(), name)
    {
    }

    public Target(string extension, Stream data, string? name = null)
    {
        Extension = CleanExtension(extension);
        StringData = null;
        StreamData = data;
        Name = name;
    }

    public string Extension { get; }
    public string? Name { get; }
    public string NameOrTarget => Name ?? "target";
    public bool IsString => StringData != null;
    public bool IsStream => StreamData != null;
    public string? StringData { get; }
    public Stream? StreamData { get; }

    internal static string CleanExtension(string extension)
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
/// What a passing verification produced.
/// </summary>
public sealed class VerifyResult
{
    readonly IReadOnlyList<(string Path, string? Text)> files;

    internal VerifyResult(IReadOnlyList<(string Path, string? Text)> files, object? target)
    {
        this.files = files;
        Target = target;
    }

    /// <summary>
    /// The verified files.
    /// </summary>
    public IReadOnlyList<string> Files => files.Select(file => file.Path).ToList();

    /// <summary>
    /// The verified text files and their content.
    /// </summary>
    public IReadOnlyList<(string Path, string Text)> TextFiles =>
        files.Where(file => file.Text != null).Select(file => (file.Path, file.Text!)).ToList();

    /// <summary>
    /// The content of the only text file. Throws when there is not exactly one.
    /// </summary>
    public string Text =>
        TextFiles.Count == 1
            ? TextFiles[0].Text
            : throw new InvalidOperationException($"Text is only available when there is exactly one text file, but there are {TextFiles.Count}.");

    /// <summary>
    /// The value that was verified.
    /// </summary>
    public object? Target { get; }
}

/// <summary>
/// Verification failed: a snapshot is new, does not match, or is no longer produced.
/// </summary>
public sealed class VerifyException(string message) : Exception(message);
