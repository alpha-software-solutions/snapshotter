#if !NET462
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace Snapshotter;

public static partial class Verifier
{
    [Pure]
    public static SettingsTask VerifyTuple(
        Expression<Func<ITuple>> expression,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.VerifyTuple(expression));
    }
}
#endif
