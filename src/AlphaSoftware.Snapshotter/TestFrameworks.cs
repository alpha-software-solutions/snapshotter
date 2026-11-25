using System.Reflection;

namespace AlphaSoftware.Snapshotter;

/// <summary>
/// Reads the arguments of the running parameterized test from the test framework's ambient context, when there is
/// one. Nothing is referenced: the frameworks are found by name among the loaded assemblies, so without them this
/// does nothing.
/// </summary>
static class TestFrameworks
{
    // Assembly, context type, the static property with the current context, then property paths to the arguments.
    static readonly (string Assembly, string Type, string Current, string[][] Paths)[] frameworks =
    [
        ("nunit.framework", "NUnit.Framework.TestContext", "CurrentContext", [["Test", "Arguments"]]),
        ("xunit.v3.core", "Xunit.TestContext", "Current", [["Test", "TestMethodArguments"], ["Test", "TestCase", "TestMethodArguments"]]),
        ("TUnit.Core", "TUnit.Core.TestContext", "Current",
            [["TestDetails", "TestMethodArguments"], ["Metadata", "TestDetails", "TestMethodArguments"]])
    ];

    public static object?[]? CurrentArguments(MethodBase? method)
    {
        if (method == null)
        {
            return null;
        }

        var parameterCount = method.GetParameters().Length;
        if (parameterCount == 0)
        {
            return null;
        }

        foreach (var (assemblyName, typeName, currentName, paths) in frameworks)
        {
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(candidate => string.Equals(candidate.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
                var context = assembly?.GetType(typeName)?.GetProperty(currentName, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (context == null)
                {
                    continue;
                }

                foreach (var path in paths)
                {
                    if (Follow(context, path) is object?[] arguments && arguments.Length == parameterCount)
                    {
                        return arguments;
                    }
                }
            }
            catch
            {
                // A framework version with a different shape: no arguments from it.
            }
        }

        return null;
    }

    static object? Follow(object? value, string[] path)
    {
        foreach (var name in path)
        {
            if (value == null)
            {
                return null;
            }

            value = value.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(value)
                    ?? value.GetType().GetInterfaces().Select(type => type.GetProperty(name)).FirstOrDefault(property => property != null)?.GetValue(value);
        }

        return value;
    }
}
