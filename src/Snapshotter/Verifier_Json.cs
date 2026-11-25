namespace Snapshotter;

// ReSharper disable RedundantSuppressNullableWarningExpression
public static partial class Verifier
{
    [Pure]
    public static SettingsTask VerifyJson(
        [StringSyntax(StringSyntaxAttribute.Json)]
        StringBuilder? target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0) =>
        Verify(settings, Assembly.GetCallingAssembly()!, sourceFile, lineNumber, methodName, _ => _.VerifyJson(target));

    [Pure]
    public static SettingsTask VerifyJson(
        Task<StringBuilder> target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0) =>
        Verify(settings, Assembly.GetCallingAssembly()!, sourceFile, lineNumber, methodName, _ => _.VerifyJson(target));

    [Pure]
    public static SettingsTask VerifyJson(
        ValueTask<StringBuilder> target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0) =>
        Verify(settings, Assembly.GetCallingAssembly()!, sourceFile, lineNumber, methodName, _ => _.VerifyJson(target));

    [Pure]
    public static SettingsTask VerifyJson(
        [StringSyntax(StringSyntaxAttribute.Json)]
        string? target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0) =>
        Verify(settings, Assembly.GetCallingAssembly()!, sourceFile, lineNumber, methodName, _ => _.VerifyJson(target));

    [Pure]
    public static SettingsTask VerifyJson(
        Task<string> target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0) =>
        Verify(settings, Assembly.GetCallingAssembly()!, sourceFile, lineNumber, methodName, _ => _.VerifyJson(target));

    [Pure]
    public static SettingsTask VerifyJson(
        ValueTask<string> target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0) =>
        Verify(settings, Assembly.GetCallingAssembly()!, sourceFile, lineNumber, methodName, _ => _.VerifyJson(target));

    [Pure]
    public static SettingsTask VerifyJson(
        Stream? target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0) =>
        Verify(settings, Assembly.GetCallingAssembly()!, sourceFile, lineNumber, methodName, _ => _.VerifyJson(target));

    [Pure]
    public static SettingsTask VerifyJson(
        Task<Stream> target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0) =>
        Verify(settings, Assembly.GetCallingAssembly()!, sourceFile, lineNumber, methodName, _ => _.VerifyJson(target));

    [Pure]
    public static SettingsTask VerifyJson(
        ValueTask<Stream> target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0) =>
        Verify(settings, Assembly.GetCallingAssembly()!, sourceFile, lineNumber, methodName, _ => _.VerifyJson(target));
}