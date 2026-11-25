// ReSharper disable RedundantSuppressNullableWarningExpression

namespace Snapshotter;

public static partial class Verifier
{
    [Pure]
    public static SettingsTask Verify(
        byte[]? target,
        string extension,
        VerifySettings? settings = null,
        object? info = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.VerifyStream(target, extension, info));
    }

    [Pure]
    public static SettingsTask Verify(
        byte[]? target,
        VerifySettings? settings = null,
        object? info = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.VerifyStream(target, info));
    }

    [Pure]
    public static SettingsTask Verify(
        Task<byte[]> target,
        string extension,
        VerifySettings? settings = null,
        object? info = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.VerifyStream(target, extension, info));
    }

    [Pure]
    public static SettingsTask Verify(
        ValueTask<byte[]> target,
        string extension,
        VerifySettings? settings = null,
        object? info = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.VerifyStream(target, extension, info));
    }

    [Pure]
    public static SettingsTask Verify(
        FileStream? target,
        VerifySettings? settings = null,
        object? info = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.VerifyStream(target, info));
    }

    [Pure]
    public static SettingsTask Verify(
        Stream? target,
        VerifySettings? settings = null,
        object? info = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.VerifyStream(target, info));
    }

    [Pure]
    public static SettingsTask Verify(
        Stream? target,
        string extension,
        VerifySettings? settings = null,
        object? info = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.VerifyStream(target, extension, info));
    }

    [Pure]
    public static SettingsTask Verify<T>(
        Task<T> target,
        string extension,
        VerifySettings? settings = null,
        object? info = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
        where T : Stream
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.VerifyStream(target, extension, info));
    }

    [Pure]
    public static SettingsTask Verify<T>(
        ValueTask<T> target,
        string extension,
        VerifySettings? settings = null,
        object? info = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
        where T : Stream
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.VerifyStream(target, extension, info));
    }

    [Pure]
    public static SettingsTask Verify<T>(
        IEnumerable<T> targets,
        string extension,
        VerifySettings? settings = null,
        object? info = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
        where T : Stream
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.VerifyStreams(targets, extension, info));
    }
}