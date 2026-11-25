// ReSharper disable RedundantSuppressNullableWarningExpression

namespace Snapshotter;

public static partial class Verifier
{
    [Pure]
    public static SettingsTask Verify(
        [StringSyntax("*")]
        string? target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.VerifyString(target));
    }

    [Pure]
    public static SettingsTask Verify(
        [StringSyntax("*")]
        Task<string> target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.VerifyString(target));
    }

    [Pure]
    public static SettingsTask Verify(
        [StringSyntax("*")]
        string? target,
        string extension,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.VerifyString(target, extension));
    }

    [Pure]
    public static SettingsTask Verify(
        [StringSyntax("*")]
        Task<string> target,
        string extension,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.VerifyString(target, extension));
    }
}