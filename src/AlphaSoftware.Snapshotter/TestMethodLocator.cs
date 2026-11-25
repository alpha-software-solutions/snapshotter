using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace AlphaSoftware.Snapshotter;

/// <summary>
/// Finds the method and type that called <c>Verify</c>, without asking a test framework.
/// </summary>
/// <remarks>
/// The first stack frame outside this library is the caller. Compiler generated code is unwrapped: an async
/// method's state machine, a lambda's closure class and a local function all map back to the method they were
/// written in, and their declaring type to the user's type.
/// </remarks>
static class TestMethodLocator
{
    static readonly Assembly self = typeof(TestMethodLocator).Assembly;

    public static (Type Type, MethodBase Method)? Find(string memberName)
    {
        StackTrace trace;
        try
        {
            trace = new(1, false);
        }
        catch
        {
            return null;
        }

        foreach (var frame in trace.GetFrames() ?? [])
        {
            var method = frame?.GetMethod();
            var declaring = method?.DeclaringType;
            if (method == null || declaring == null || declaring.Assembly == self)
            {
                continue;
            }

            // Framework plumbing (async method builders, the thread pool) sits between an awaited continuation and
            // the user's code only below the user's frame, so the first frame outside this library is the caller.
            return Unwrap(method, declaring, memberName);
        }

        return null;
    }

    static (Type Type, MethodBase Method)? Unwrap(MethodBase method, Type declaring, string memberName)
    {
        var type = declaring;
        var name = method.Name;

        while (IsCompilerGenerated(type) && type.DeclaringType != null)
        {
            // <Totals>d__3 (async state machine), <Totals>b__0 (lambda), <>c__DisplayClass1_0 (closure).
            name = OriginalName(type.Name) ?? name;
            type = type.DeclaringType;
        }

        name = OriginalName(name) ?? name;

        var methods = type
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Cast<MethodBase>()
            .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .ToList();

        // The compiler supplied member name is the reliable one; the stack frame only adds the type and overload.
        var wanted = memberName.Length > 0 ? memberName : name;
        var candidates = methods.Where(candidate => candidate.Name == wanted).ToList();
        if (candidates.Count == 0)
        {
            return (type, method);
        }

        var stateMachineOwner = candidates.FirstOrDefault(candidate =>
            candidate.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType == declaring ||
            candidate.GetCustomAttribute<IteratorStateMachineAttribute>()?.StateMachineType == declaring);
        if (stateMachineOwner != null)
        {
            return (type, stateMachineOwner);
        }

        var same = candidates.FirstOrDefault(candidate => candidate == method);
        return (type, same ?? candidates[0]);
    }

    static bool IsCompilerGenerated(Type type) =>
        type.Name.StartsWith("<", StringComparison.Ordinal) ||
        type.IsDefined(typeof(CompilerGeneratedAttribute), false);

    // "<Totals>d__3" -> "Totals"; "<>c__DisplayClass1_0" -> null.
    static string? OriginalName(string generated)
    {
        if (!generated.StartsWith("<", StringComparison.Ordinal))
        {
            return null;
        }

        var close = generated.IndexOf('>');
        return close > 1 ? generated.Substring(1, close - 1) : null;
    }
}
