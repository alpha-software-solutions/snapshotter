using System.Collections.Concurrent;
using System.Reflection;

namespace AlphaSoftware.Snapshotter;

/// <summary>
/// What the build recorded about the test project: its directory, the solution's directory and its target
/// frameworks. The MSBuild props in the package write them as assembly metadata.
/// </summary>
sealed class ProjectInfo
{
    static readonly ConcurrentDictionary<Assembly, ProjectInfo> cache = new();
    static readonly ProjectInfo empty = new(null, null, null);

    ProjectInfo(string? projectDirectory, string? solutionDirectory, string? targetFrameworks)
    {
        ProjectDirectory = projectDirectory;
        SolutionDirectory = solutionDirectory;
        TargetFrameworks = targetFrameworks;
    }

    public string? ProjectDirectory { get; }
    public string? SolutionDirectory { get; }
    public string? TargetFrameworks { get; }

    /// <summary>
    /// Whether the project builds for more than one framework. Without the metadata, assume it does.
    /// </summary>
    public bool MultiTarget => TargetFrameworks == null || TargetFrameworks.Contains(';');

    public static ProjectInfo For(Assembly? assembly) =>
        assembly == null ? empty : cache.GetOrAdd(assembly, Read);

    static ProjectInfo Read(Assembly assembly)
    {
        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .GroupBy(attribute => attribute.Key)
            .ToDictionary(group => group.Key, group => group.Last().Value);

        metadata.TryGetValue("AlphaSoftware.Snapshotter.ProjectDirectory", out var project);
        metadata.TryGetValue("AlphaSoftware.Snapshotter.SolutionDirectory", out var solution);
        metadata.TryGetValue("AlphaSoftware.Snapshotter.TargetFrameworks", out var frameworks);

        if (string.IsNullOrWhiteSpace(project) || !Directory.Exists(project))
        {
            project = null;
        }

        if (string.IsNullOrWhiteSpace(solution) || !Directory.Exists(solution))
        {
            solution = project == null ? null : FindSolution(project);
        }

        if (string.IsNullOrWhiteSpace(frameworks))
        {
            metadata.TryGetValue("AlphaSoftware.Snapshotter.TargetFramework", out var single);
            frameworks = string.IsNullOrWhiteSpace(single) ? null : single;
        }

        return new(project, solution, frameworks);
    }

    // The project directory, its parent or its grandparent, whichever has exactly one solution (.slnx preferred).
    static string? FindSolution(string projectDirectory)
    {
        var directory = new DirectoryInfo(projectDirectory);
        for (var level = 0; level < 3 && directory != null; level++, directory = directory.Parent)
        {
            var slnx = directory.GetFiles("*.slnx");
            if (slnx.Length == 1)
            {
                return directory.FullName;
            }

            var sln = directory.GetFiles("*.sln");
            if (slnx.Length == 0 && sln.Length == 1)
            {
                return directory.FullName;
            }

            if (slnx.Length + sln.Length > 1)
            {
                return null;
            }
        }

        return null;
    }
}
