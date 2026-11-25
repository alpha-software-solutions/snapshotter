namespace AlphaSoftware.Snapshotter;

/// <summary>
/// Which file extensions hold text. Text is compared as text, with scrubbers; anything else byte for byte.
/// </summary>
public static class FileExtensions
{
    static readonly HashSet<string> text = new(StringComparer.OrdinalIgnoreCase)
    {
        "txt", "text", "noextension", "csv", "tsv", "psv", "json", "json5", "jsonc", "ndjson", "xml", "xaml", "xsd", "xsl",
        "xslt", "html", "htm", "xhtml", "cshtml", "razor", "md", "markdown", "mdx", "rst", "adoc", "tex", "rtf", "cs", "csx",
        "vb", "fs", "fsx", "fsi", "js", "mjs", "cjs", "jsx", "ts", "mts", "cts", "tsx", "vue", "svelte", "css", "scss", "sass",
        "less", "sql", "yaml", "yml", "toml", "ini", "cfg", "conf", "config", "properties", "env", "log", "svg", "sln", "slnx",
        "csproj", "fsproj", "vbproj", "props", "targets", "nuspec", "resx", "ps1", "psm1", "psd1", "sh", "bash", "zsh", "bat",
        "cmd", "py", "rb", "go", "rs", "java", "kt", "kts", "swift", "c", "h", "cpp", "hpp", "cc", "m", "mm", "php", "pl",
        "lua", "r", "dart", "scala", "groovy", "gradle", "graphql", "gql", "proto", "editorconfig", "gitignore",
        "gitattributes", "dockerfile", "http", "ics", "vcf", "eml", "plist", "manifest", "tt", "t4", "liquid", "mustache",
        "hbs", "handlebars", "diff", "patch", "srt", "vtt", "sarif", "wsdl", "xliff", "xlf", "dot", "mermaid", "puml"
    };

    /// <summary>
    /// Whether files with this extension (without the dot) hold text.
    /// </summary>
    public static bool IsText(string extension) =>
        text.Contains(extension.TrimStart('.'));

    /// <summary>
    /// Treat files with this extension as text.
    /// </summary>
    public static void AddTextExtension(string extension) =>
        text.Add(extension.TrimStart('.'));

    /// <summary>
    /// Treat files with this extension as binary.
    /// </summary>
    public static void RemoveTextExtension(string extension) =>
        text.Remove(extension.TrimStart('.'));
}
