using System.Runtime.CompilerServices;

public static class ModuleInitializer
{
    // Accept every changed snapshot: SNAPSHOTTER_ACCEPT=1 dotnet test. FailureTests then fail, since they check
    // mismatches; run the suite again without it.
    [ModuleInitializer]
    public static void Initialize()
    {
        if (Environment.GetEnvironmentVariable("SNAPSHOTTER_ACCEPT") == "1")
        {
            VerifierSettings.AutoVerify();
        }
    }
}
