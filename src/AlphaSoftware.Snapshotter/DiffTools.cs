using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AlphaSoftware.Snapshotter;

/// <summary>
/// The diff tools Snapshotter knows how to find and open.
/// </summary>
public enum DiffTool
{
    BeyondCompare,
    P4Merge,
    Kaleidoscope,
    DiffMerge,
    TortoiseGitMerge,
    TortoiseMerge,
    WinMerge,
    Meld,
    KDiff3,
    SublimeMerge,
    Rider,
    VisualStudioCode,
    VisualStudio
}

/// <summary>
/// Opens a diff tool when a snapshot does not match: the first installed tool in the order, unless diffing is
/// disabled or the tests run on a build server.
/// </summary>
/// <remarks>
/// The order can also be set with the <c>DiffEngine_ToolOrder</c> environment variable (tool names separated by
/// commas or semicolons), diffing turned off with <c>DiffEngine_Disabled=true</c>, and the number of tools opened in
/// one run (5 by default) changed with <c>DiffEngine_MaxInstances</c>.
/// </remarks>
public static class DiffTools
{
    static readonly object gate = new();
    static List<ToolDefinition>? order;
    static readonly List<ToolDefinition> custom = [];
    static int launched;
    static int? maxInstances;

    /// <summary>
    /// Never open a diff tool. Also set by the <c>DiffEngine_Disabled</c> environment variable, and when the tests run
    /// inside an AI coding agent, where nobody would see the window.
    /// </summary>
    public static bool Disabled { get; set; } = EnvironmentFlag("DiffEngine_Disabled") || InAgent();

    static bool InAgent() =>
        new[] { "CLAUDECODE", "CURSOR_AGENT", "CODEX_SANDBOX", "GEMINI_CLI", "AI_AGENT" }
            .Any(name => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)));

    /// <summary>
    /// The most diff tools opened in one test run. Defaults to 5, or the <c>DiffEngine_MaxInstances</c> environment
    /// variable.
    /// </summary>
    public static int MaxInstances
    {
        get => maxInstances ??= int.TryParse(Environment.GetEnvironmentVariable("DiffEngine_MaxInstances"), out var value) ? value : 5;
        set => maxInstances = value;
    }

    /// <summary>
    /// Try these tools, in this order, before any others.
    /// </summary>
    public static void UseOrder(params DiffTool[] tools)
    {
        lock (gate)
        {
            var preferred = tools.Select(tool => Builtin.First(definition => definition.Tool == tool)).ToList();
            order = custom.Concat(preferred).Concat(Builtin.Except(preferred)).ToList();
        }
    }

    /// <summary>
    /// Add a diff tool, tried before the built in ones.
    /// </summary>
    /// <param name="name">A name for messages.</param>
    /// <param name="exePath">The executable. It is only used if it exists.</param>
    /// <param name="arguments">Builds the command line arguments from the received and verified paths.</param>
    public static void AddTool(string name, string exePath, Func<string, string, string> arguments)
    {
        lock (gate)
        {
            custom.Insert(0, new(null, name, [() => exePath], arguments));
            order = null;
        }
    }

    /// <summary>
    /// The installed diff tool that would be opened, if any.
    /// </summary>
    public static bool TryFind(out string name, out string path)
    {
        foreach (var definition in Order())
        {
            var found = definition.Find();
            if (found != null)
            {
                name = definition.Name;
                path = found;
                return true;
            }
        }

        name = "";
        path = "";
        return false;
    }

    internal static void Launch(string receivedPath, string verifiedPath, bool isText)
    {
        if (Disabled || BuildServer.Detected)
        {
            return;
        }

        try
        {
            if (!TryFind(out _, out var path))
            {
                return;
            }

            var definition = Order().First(candidate => candidate.Find() == path);
            lock (gate)
            {
                if (launched >= MaxInstances)
                {
                    return;
                }

                launched++;
            }

            // Diff tools refuse to open a file that does not exist. An empty one shows the whole result as added.
            if (isText && !File.Exists(verifiedPath))
            {
                File.WriteAllText(verifiedPath, "");
            }

            using var process = Process.Start(new ProcessStartInfo(path, definition.Arguments(receivedPath, verifiedPath))
            {
                UseShellExecute = false,
                CreateNoWindow = false
            });
        }
        catch
        {
            // A diff tool is a convenience. Failing to start one must not hide the mismatch.
        }
    }

    static List<ToolDefinition> Order()
    {
        lock (gate)
        {
            if (order != null)
            {
                return order;
            }

            var preferred = new List<ToolDefinition>();
            var fromEnvironment = Environment.GetEnvironmentVariable("DiffEngine_ToolOrder");
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
            {
                foreach (var name in fromEnvironment!.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries))
                {
                    if (Enum.TryParse<DiffTool>(name.Trim(), ignoreCase: true, out var tool))
                    {
                        preferred.Add(Builtin.First(definition => definition.Tool == tool));
                    }
                }
            }

            return order = custom.Concat(preferred).Concat(Builtin.Except(preferred)).ToList();
        }
    }

    static bool EnvironmentFlag(string name) =>
        string.Equals(Environment.GetEnvironmentVariable(name), "true", StringComparison.OrdinalIgnoreCase) ||
        Environment.GetEnvironmentVariable(name) == "1";

    sealed class ToolDefinition(DiffTool? tool, string name, Func<string?>[] candidates, Func<string, string, string> arguments)
    {
        string? found;
        bool searched;

        public DiffTool? Tool { get; } = tool;
        public string Name { get; } = name;
        public Func<string, string, string> Arguments { get; } = arguments;

        public string? Find()
        {
            if (searched)
            {
                return found;
            }

            foreach (var candidate in candidates)
            {
                string? path;
                try
                {
                    path = candidate();
                }
                catch
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    found = path;
                    break;
                }
            }

            searched = true;
            return found;
        }
    }

    static readonly bool windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    static readonly bool mac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    static string Quote(string path) => $"\"{path}\"";

    static string Pair(string received, string verified) => $"{Quote(received)} {Quote(verified)}";

    static Func<string?> Windows(string relative) =>
        () => windows ? FirstExisting(ProgramFolders().Select(folder => Path.Combine(folder, relative))) : null;

    static Func<string?> Mac(string path) =>
        () => mac ? path : null;

    static Func<string?> Linux(string path) =>
        () => !windows && !mac ? path : null;

    static Func<string?> OnPath(string name) =>
        () =>
        {
            var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
            var names = windows ? [name + ".exe"] : new[] { name };
            return FirstExisting(paths.SelectMany(folder => names.Select(file => Path.Combine(folder.Trim('"'), file))));
        };

    // Versioned install folders, newest first: "JetBrains Rider 2025.2", "Microsoft Visual Studio\2022\Community".
    static Func<string?> WindowsGlob(string parent, string pattern, string relative) =>
        () =>
        {
            if (!windows)
            {
                return null;
            }

            return FirstExisting(ProgramFolders()
                .Select(folder => Path.Combine(folder, parent))
                .Where(Directory.Exists)
                .SelectMany(folder => Directory.GetDirectories(folder, pattern))
                .OrderByDescending(folder => folder, StringComparer.OrdinalIgnoreCase)
                .Select(folder => Path.Combine(folder, relative)));
        };

    static IEnumerable<string> ProgramFolders()
    {
        foreach (var variable in new[] { "ProgramFiles", "ProgramW6432", "ProgramFiles(x86)" })
        {
            var folder = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrEmpty(folder))
            {
                yield return folder!;
            }
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(local))
        {
            yield return Path.Combine(local, "Programs");
        }
    }

    static string? FirstExisting(IEnumerable<string> paths) =>
        paths.FirstOrDefault(File.Exists);

    static string Home(string relative) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), relative);

    static readonly List<ToolDefinition> Builtin =
    [
        new(DiffTool.BeyondCompare, "Beyond Compare",
            [
                Windows(@"Beyond Compare 5\BCompare.exe"),
                Windows(@"Beyond Compare 4\BCompare.exe"),
                Mac("/Applications/Beyond Compare.app/Contents/MacOS/bcomp"),
                Linux("/usr/bin/bcompare")
            ],
            (received, verified) => $"{Pair(received, verified)} {(windows ? "/solo" : "-solo")}"),
        new(DiffTool.P4Merge, "P4Merge",
            [
                Windows(@"Perforce\p4merge.exe"),
                Mac("/Applications/p4merge.app/Contents/MacOS/p4merge"),
                Linux("/usr/bin/p4merge")
            ],
            Pair),
        new(DiffTool.Kaleidoscope, "Kaleidoscope",
            [Mac("/usr/local/bin/ksdiff"), Mac("/opt/homebrew/bin/ksdiff")],
            Pair),
        new(DiffTool.DiffMerge, "DiffMerge",
            [
                Windows(@"SourceGear\Common\DiffMerge\sgdm.exe"),
                Mac("/Applications/DiffMerge.app/Contents/MacOS/DiffMerge"),
                Linux("/usr/bin/diffmerge")
            ],
            (received, verified) => $"--nosplash {Pair(received, verified)}"),
        new(DiffTool.TortoiseGitMerge, "TortoiseGitMerge",
            [Windows(@"TortoiseGit\bin\TortoiseGitMerge.exe")],
            Pair),
        new(DiffTool.TortoiseMerge, "TortoiseMerge",
            [Windows(@"TortoiseSVN\bin\TortoiseMerge.exe")],
            Pair),
        new(DiffTool.WinMerge, "WinMerge",
            [Windows(@"WinMerge\WinMergeU.exe")],
            (received, verified) => $"/u /wl /e {Pair(received, verified)}"),
        new(DiffTool.Meld, "Meld",
            [
                Windows(@"Meld\Meld.exe"),
                Mac("/Applications/Meld.app/Contents/MacOS/Meld"),
                Linux("/usr/bin/meld")
            ],
            Pair),
        new(DiffTool.KDiff3, "KDiff3",
            [
                Windows(@"KDiff3\kdiff3.exe"),
                Windows(@"KDiff3\bin\kdiff3.exe"),
                Mac("/Applications/kdiff3.app/Contents/MacOS/kdiff3"),
                Linux("/usr/bin/kdiff3")
            ],
            (received, verified) => $"{Pair(received, verified)} --cs CreateBakFiles=0"),
        new(DiffTool.SublimeMerge, "Sublime Merge",
            [
                Windows(@"Sublime Merge\smerge.exe"),
                Mac("/Applications/Sublime Merge.app/Contents/SharedSupport/bin/smerge"),
                Linux("/usr/bin/smerge")
            ],
            (received, verified) => $"mergetool {Pair(received, verified)}"),
        new(DiffTool.Rider, "Rider",
            [
                WindowsGlob("JetBrains", "JetBrains Rider*", @"bin\rider64.exe"),
                Windows(@"Rider\bin\rider64.exe"),
                Mac("/Applications/Rider.app/Contents/MacOS/rider"),
                () => !windows && !mac ? Home(".local/share/JetBrains/Toolbox/scripts/rider") : null,
                Linux("/snap/bin/rider")
            ],
            (received, verified) => $"diff {Pair(received, verified)}"),
        new(DiffTool.VisualStudioCode, "Visual Studio Code",
            [
                Windows(@"Microsoft VS Code\Code.exe"),
                Mac("/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code"),
                Linux("/usr/bin/code"),
                Linux("/snap/bin/code"),
                () => windows ? null : OnPath("code")()
            ],
            (received, verified) => $"--diff {Pair(received, verified)}"),
        new(DiffTool.VisualStudio, "Visual Studio",
            [WindowsGlob(@"Microsoft Visual Studio", "*", @"Community\Common7\IDE\devenv.exe"),
             WindowsGlob(@"Microsoft Visual Studio", "*", @"Professional\Common7\IDE\devenv.exe"),
             WindowsGlob(@"Microsoft Visual Studio", "*", @"Enterprise\Common7\IDE\devenv.exe")],
            (received, verified) => $"/diff {Pair(received, verified)}")
    ];
}

/// <summary>
/// Whether the tests run on a build server, where no diff tool is opened and snapshots are never accepted
/// automatically unless asked for.
/// </summary>
public static class BuildServer
{
    static readonly string[] variables =
    [
        "CI", "TF_BUILD", "GITHUB_ACTIONS", "GITLAB_CI", "TEAMCITY_VERSION", "JENKINS_URL", "BUILDKITE", "APPVEYOR",
        "TRAVIS", "CIRCLECI", "BITBUCKET_BUILD_NUMBER", "CODEBUILD_BUILD_ID", "bamboo_buildKey", "GO_SERVER_URL",
        "BUILD_ID_CI"
    ];

    /// <summary>
    /// True when one of the environment variables build servers set is present. Can be overridden, for example
    /// in a test of a test.
    /// </summary>
    public static bool Detected { get; set; } =
        variables.Any(name => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)) &&
                              !string.Equals(Environment.GetEnvironmentVariable(name), "false", StringComparison.OrdinalIgnoreCase));
}
