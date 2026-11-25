#pragma warning disable InnerVerifyChecks
namespace Snapshotter;

public static class VerifyChecks
{
    public static Task Run(Assembly assembly) =>
        InnerVerifyChecks.Run(assembly);
}