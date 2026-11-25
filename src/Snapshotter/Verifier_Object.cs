namespace Snapshotter;

// ReSharper disable RedundantSuppressNullableWarningExpression
public static partial class Verifier
{
    [Pure]
    public static SettingsTask Verify(
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.Verify());
    }

    [Pure]
    public static SettingsTask Verify<T>(
        Func<Task<T>> target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.Verify(target()));
    }

    [Pure]
    public static SettingsTask Verify<T>(
        Task<T> target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.Verify(target));
    }

    [Pure]
    public static SettingsTask Verify<T>(
        ValueTask<T> target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.Verify(target));
    }

    [Pure]
    public static SettingsTask Verify<T>(
        IAsyncEnumerable<T> target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.Verify(target));
    }

    [Pure]
    public static SettingsTask Verify(
        object? target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.Verify(target));
    }
}