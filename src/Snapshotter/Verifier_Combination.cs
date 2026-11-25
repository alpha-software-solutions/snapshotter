// ReSharper disable RedundantSuppressNullableWarningExpression
namespace Snapshotter;

public static partial class Verifier
{
    [Pure]
    public static Combination Combination(
        bool? captureExceptions = null,
        VerifySettings? settings = null,
        bool? header = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return new(
            captureExceptions,
            settings,
            header,
            sourceFile,
            lineNumber,
            (settings, sourceFile, line, verify) => Verify(settings, assembly, sourceFile, line, methodName, verify));
    }
}