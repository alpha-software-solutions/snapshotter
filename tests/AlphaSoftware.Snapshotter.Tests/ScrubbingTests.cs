using System.Text;

public class ScrubbingTests
{
    static readonly Guid guid = new("ebced679-45d3-4653-8791-3d969c4a986c");
    static readonly Guid other = new("c8eeaf99-d5c4-4341-8543-4597c3fd40c9");

    [Fact]
    public Task Guids() =>
        Verify(new
        {
            Guid = guid,
            GuidNullable = (Guid?)guid,
            GuidString = guid.ToString(),
            UpperString = guid.ToString().ToUpperInvariant(),
            Other = other,
            Empty = Guid.Empty,
            EmptyNullable = (Guid?)Guid.Empty,
            InsideText = $"id {guid} here"
        });

    [Fact]
    public Task DontScrubGuids() =>
        Verify(new { Guid = guid })
            .DontScrubGuids();

    [Fact]
    public Task Dates()
    {
        var dateTime = new DateTime(2020, 1, 2, 3, 4, 5);
        var offset = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.FromHours(1));
        return Verify(new
        {
            DateTime = dateTime,
            DateTimeNullable = (DateTime?)dateTime,
            SameTicksOtherKind = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
            DateTimeOffset = offset,
            DateTimeOffsetNullable = (DateTimeOffset?)offset,
            IsoString = "2020-01-02T03:04:05+01:00",
            MinValue = (DateTime?)DateTime.MinValue,
            MaxValue = DateTime.MaxValue
        });
    }

    [Fact]
    public Task DisableDateCounting() =>
        Verify(new { A = new DateTime(2020, 1, 1), B = new DateTime(2021, 1, 1) })
            .DisableDateCounting();

    [Fact]
    public Task CountersAreSharedAcrossMembers() =>
        Verify(new
        {
            First = new { Id = guid, Created = new DateTime(2020, 1, 1) },
            Second = new { Id = other, ParentId = guid, Created = new DateTime(2020, 1, 1) }
        });

    [Fact]
    public Task NamedGuid() =>
        Verify(new { Value = guid, Other = other })
            .AddNamedGuid(guid, "TheGuid");

    [Fact]
    public Task NamedGuidFromExpression()
    {
        var theGuid = guid;
        return Verify(new { Value = guid })
            .AddNamedGuid(theGuid);
    }

    [Fact]
    public Task InlineGuids() =>
        Verify(
                $"""
                 braces {'{'}{guid}{'}'}
                 slashes /{guid}/
                 undashed {guid:N}
                 upper {guid.ToString().ToUpperInvariant()}
                 touching a{guid}a
                 empty {Guid.Empty}
                 again {guid}
                 """)
            .ScrubInlineGuids();

    [Fact]
    public Task InlineGuidsDashedOnly() =>
        Verify($"guid: {guid} hash: 5d41402abc4b2a76b9719d911017c592")
            .ScrubInlineGuids(GuidFormats.Dashed);

    [Fact]
    public Task InlineDateTimes() =>
        Verify("content 2020-10-20 content 2020-10-20 and 2021-01-01")
            .ScrubInlineDateTimes("yyyy-MM-dd");

    [Fact]
    public Task NumericIds() =>
        Verify(new { Id = 123, UserId = 456, userID = 789, Name = "Test", Count = 10, Again = new { UserId = 456 } })
            .ScrubNumericIds();

    [Fact]
    public Task NumericIdOfAType() =>
        Verify(new Customer { Id = 5, Name = "Ann" })
            .ScrubNumericIds();

    [Fact]
    public Task ScrubLinesContaining() =>
        Verify("a\nb\nc\nD\ne\nf")
            .ScrubLinesContaining("c", "d", "F");

    [Fact]
    public Task ScrubLinesContainingOrdinal() =>
        Verify("a\nb\nc\nD\ne\nf")
            .ScrubLinesContaining(StringComparison.Ordinal, "c", "d", "F");

    [Fact]
    public Task ScrubLinesWithReplace() =>
        Verify("LineA\nLineB\nLineC")
            .ScrubLinesWithReplace(line => line == "LineB" ? null : line.Replace("Line", "Row"));

    [Fact]
    public Task ScrubEmptyLines() =>
        Verify("\nLineA\n\n  \nLineC\n")
            .ScrubEmptyLines();

    [Fact]
    public Task ScrubReplace() =>
        Verify("One Two Three OneTwo")
            .ScrubReplace("One", "1", requireWordBoundary: true);

    [Fact]
    public Task AddScrubber() =>
        Verify("value: secret")
            .AddScrubber(builder => builder.Replace("secret", "{redacted}"));

    [Fact]
    public Task AddScrubberForExtension() =>
        Verify("value: secret", "md")
            .AddScrubber("txt", builder => builder.Replace("secret", "not this one"))
            .AddScrubber("md", builder => builder.Replace("secret", "{redacted}"));

    [Fact]
    public Task ScrubbersSeeEachOthersOutput() =>
        Verify($"id {guid}")
            .ScrubInlineGuids()
            .AddScrubber(builder => builder.Replace("Guid_1", "TheId"));

    [Fact]
    public Task DisableScrubbers() =>
        Verify(new { Guid = guid, Date = new DateTime(2020, 1, 1) })
            .DisableScrubbers();

    [Fact]
    public Task MachineAndUserName() =>
        Verify($"machine: {Environment.MachineName}, user: {Environment.UserName}")
            .ScrubMachineName()
            .ScrubUserName();

    [Fact]
    public Task Directories() =>
        Verify(new
        {
            Project = AttributeReader.ProjectDirectory + "Inputs/notes.txt",
            ProjectWithForwardSlashes = AttributeReader.ProjectDirectory.Replace('\\', '/') + "Inputs",
            Solution = Path.Combine(AttributeReader.SolutionDirectory, "README.md"),
            Temp = Path.GetTempPath() + "file.txt",
            // Only whole directory names match: this is the solution directory followed by a sibling of the project.
            NotAPrefix = (AttributeReader.ProjectDirectory.TrimEnd('/', '\\') + "Extra").Replace('\\', '/')
        });

    [Fact]
    public Task IgnoreMember() =>
        Verify(new { Keep = 1, Password = "x", Nested = new { Password = "y", Keep = 2 } })
            .IgnoreMember("Password");

    [Fact]
    public Task IgnoreMemberOfType() =>
        Verify(new { Customer = new Customer { Id = 1, Name = "Ann", Secret = "s" }, Other = new { Secret = "kept" } })
            .IgnoreMember<Customer>(_ => _.Secret);

    [Fact]
    public Task IgnoreMembersByPredicate() =>
        Verify(new { Keep = 1, TempA = 2, TempB = 3 })
            .IgnoreMembers(name => name.StartsWith("Temp"));

    [Fact]
    public Task IgnoreMembersWithType() =>
        Verify(new { Keep = 1, When = new DateTime(2020, 1, 1), Customer = new Customer { Id = 1 } })
            .IgnoreMembersWithType<Customer>()
            .IgnoreMembersWithType<DateTime>();

    [Fact]
    public Task ScrubMember() =>
        Verify(new { Keep = 1, Token = "abc", Throws = new ThrowingGetter() })
            .ScrubMember("Token")
            .ScrubMember<ThrowingGetter>(_ => _.Value);

    [Fact]
    public Task IgnoreAndScrubDictionaryKeys() =>
        Verify(new Dictionary<string, object>
            {
                ["Ignore"] = "x",
                ["Scrub"] = "y",
                ["Keep"] = new Dictionary<string, string> { ["Ignore"] = "z", ["Scrub"] = "w", ["Key"] = "v" }
            })
            .IgnoreMember("Ignore")
            .ScrubMember("Scrub");

    [Fact]
    public Task IgnoreAndScrubInstances() =>
        Verify(new
            {
                ToIgnore = new Customer { Name = "Ignore" },
                ToScrub = new Customer { Name = "Scrub" },
                ToInclude = new Customer { Name = "Include" },
                List = new[] { new Customer { Name = "Ignore" }, new Customer { Name = "Include" } }
            })
            .IgnoreInstance<Customer>(_ => _.Name == "Ignore")
            .ScrubInstance<Customer>(_ => _.Name == "Scrub");

    [Fact]
    public Task IgnoreStackTrace() =>
        Throws(() => throw new InvalidOperationException("no stack"))
            .IgnoreStackTrace();

    [Fact]
    public Task ScrubbingAppliesToTextFiles() =>
        Verify(new StringBuilder($"id {guid}"))
            .ScrubInlineGuids();

    public class Customer
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Secret { get; set; }
    }

    public class ThrowingGetter
    {
        public string Value => throw new InvalidOperationException("never called");
    }
}

static class AttributeReader
{
    static string Metadata(string key) =>
        typeof(AttributeReader).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == key)
            .Value!;

    public static string ProjectDirectory => Metadata("AlphaSoftware.Snapshotter.ProjectDirectory");

    // Two levels up: tests/AlphaSoftware.Snapshotter.Tests/ -> the repository root, which has the solution.
    public static string SolutionDirectory => Path.GetFullPath(Path.Combine(ProjectDirectory, "..", ".."));
}
