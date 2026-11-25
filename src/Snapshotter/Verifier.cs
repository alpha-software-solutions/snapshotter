using System.Runtime.ExceptionServices;

namespace Snapshotter;

/// <summary>
/// Snapshot verification that does not depend on any test framework.
/// </summary>
/// <remarks>
/// Snapshots are named from information the compiler supplies at the call site, rather than from a
/// test framework's runtime context:
/// <list type="bullet">
///   <item>the type name is the source file name up to its first dot (<c>OrderTests.cs</c> gives <c>OrderTests</c>),</item>
///   <item>the method name is the calling member (<see cref="CallerMemberNameAttribute" />).</item>
/// </list>
/// For the usual one-class-per-file layout this produces the same file names as the framework specific
/// adapters, so existing <c>.verified.</c> files keep matching. Either part can be overridden with
/// <see cref="VerifySettings.UseTypeName" /> and <see cref="VerifySettings.UseMethodName" />.
/// A failed verification throws, which every test framework, and any other host, reports as a failure.
/// </remarks>
public static partial class Verifier
{
    public static InnerVerifier BuildVerifier(
        VerifySettings settings,
        Assembly assembly,
        string sourceFile,
        string methodName,
        bool useUniqueDirectory = false,
        int lineNumber = 0)
    {
        if (useUniqueDirectory)
        {
            settings.UseUniqueDirectory();
        }

        sourceFile = IoHelpers.GetMappedBuildPath(sourceFile);
        var typeName = GetTypeName(sourceFile);
        methodName = CleanMethodName(methodName);

        var parameterNames = ResolveParameterNames(settings, assembly, typeName, methodName);

        var pathInfo = GetPathInfo(sourceFile, typeName, methodName);
        return new(sourceFile, settings, typeName, methodName, parameterNames, pathInfo, lineNumber);
    }

    internal static string GetTypeName(string sourceFile)
    {
        var fileName = Path.GetFileName(sourceFile);
        var index = fileName.IndexOf('.');
        return index > 0 ? fileName[..index] : fileName;
    }

    /// <summary>
    /// Turns the member name the compiler supplies into one that is safe in a file name.
    /// Top level statements report <c>&lt;Main&gt;$</c>, and constructors <c>.ctor</c>.
    /// </summary>
    internal static string CleanMethodName(string methodName)
    {
        if (string.IsNullOrWhiteSpace(methodName))
        {
            throw new("Snapshotter could not determine the calling member name. Pass `methodName:` explicitly or use `UseMethodName()`.");
        }

        var start = methodName.IndexOf('<');
        if (start >= 0)
        {
            var end = methodName.IndexOf('>', start + 1);
            if (end > start + 1)
            {
                return methodName[(start + 1)..end];
            }
        }

        return methodName.TrimStart('.');
    }

    /// <summary>
    /// Without a test framework there is no runtime context that lists the parameters of the test,
    /// so their names are read from the test method itself: a method named <paramref name="methodName" />
    /// on a type named <paramref name="typeName" /> in the calling assembly.
    /// </summary>
    static IReadOnlyList<string>? ResolveParameterNames(VerifySettings settings, Assembly assembly, string typeName, string methodName)
    {
        if (!settings.TryGetParameters(out var values))
        {
            return null;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var candidates = GetLoadableTypes(assembly)
            .Where(_ => StripArity(_.Name) == typeName)
            .SelectMany(_ => _.GetMethods(flags))
            .Where(_ => _.Name == methodName)
            .Select(_ => _.GetParameters().Select(p => p.Name!).ToArray())
            .Where(_ => _.Length >= values.Length)
            .ToList();

        var exact = candidates.Where(_ => _.Length == values.Length).ToList();
        if (exact.Count > 0)
        {
            candidates = exact;
        }

        var distinct = candidates
            .GroupBy(_ => string.Join(",", _))
            .Select(_ => _.First())
            .ToList();

        if (distinct.Count == 1)
        {
            return distinct[0];
        }

        var problem = distinct.Count == 0 ? "No method" : "More than one method";
        throw new(
            $"""
             {problem} named `{typeName}.{methodName}` with at least {values.Length} parameter(s) was found in `{assembly.GetName().Name}`, so the names for `UseParameters()` cannot be determined.
             Snapshotter takes the type name from the source file name and the method name from the calling member.
             Either keep the test in a class named after its file, or name the snapshot explicitly with `UseTextForParameters()`.
             """);
    }

    static string StripArity(string name)
    {
        var index = name.IndexOf('`');
        return index > 0 ? name[..index] : name;
    }

    static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(_ => _ != null)!;
        }
    }

    static SettingsTask Verify(
        VerifySettings? settings,
        Assembly assembly,
        string sourceFile,
        int lineNumber,
        string methodName,
        Func<InnerVerifier, Task<VerifyResult>> verify,
        bool useUniqueDirectory = false)
    {
        VerifierSettings.AssignTargetAssembly(assembly);
        Guards.AgainstBadSourceFile(sourceFile);
        return new(
            settings,
            async settings =>
            {
                using var verifier = BuildVerifier(settings, assembly, sourceFile, methodName, useUniqueDirectory, lineNumber);
                try
                {
                    return await verify(verifier);
                }
                catch (TargetInvocationException exception)
                    when (exception.InnerException != null)
                {
                    // Preserve the original stack trace instead of resetting it
                    // with a bare `throw exception.InnerException`.
                    ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                    throw;
                }
            });
    }

    [Pure]
    public static SettingsTask Verify(
        object? target,
        IEnumerable<Target> rawTargets,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly();
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.Verify(target, rawTargets));
    }

    [Pure]
    public static SettingsTask Verify(
        IEnumerable<Target> targets,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly();
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.Verify(targets));
    }

    [Pure]
    public static SettingsTask Verify(
        Target target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var assembly = Assembly.GetCallingAssembly();
        return Verify(settings, assembly, sourceFile, lineNumber, methodName, _ => _.Verify(target));
    }
}
