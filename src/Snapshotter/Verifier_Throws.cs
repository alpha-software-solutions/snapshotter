// ReSharper disable RedundantSuppressNullableWarningExpression

namespace Snapshotter;

public static partial class Verifier
{
    [Pure]
    public static SettingsTask Throws(
        Action target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.Throws(target));
    }

    [Pure]
    public static SettingsTask Throws(
        Func<object?> target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.Throws(target));
    }

    [Pure]
    public static SettingsTask ThrowsTask(
        Func<Task> target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.ThrowsTask(target));
    }

    [Pure]
    public static SettingsTask ThrowsTask<T>(
        Func<Task<T>> target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.ThrowsTask(target));
    }

    [Pure]
    public static SettingsTask ThrowsValueTask(
        Func<ValueTask> target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.ThrowsValueTask(target));
    }

    [Pure]
    public static SettingsTask ThrowsValueTask<T>(
        Func<ValueTask<T>> target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.ThrowsValueTask(target));
    }
}