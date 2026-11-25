public static class ModuleInitializer
{
    // Never open a diff tool while the tests run, including for the deliberate mismatches.
    [ModuleInitializer]
    public static void Init() =>
        DiffEngine.DiffRunner.Disabled = true;
}
