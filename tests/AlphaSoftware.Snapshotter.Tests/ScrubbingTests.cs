public class ScrubbingTests
{
    static readonly Guid first = new("6b1b7a4e-6f0a-4c36-9a44-1f1d5a5f3c11");
    static readonly Guid second = new("0d3e5c2a-9b8f-4e7d-8c6b-5a4f3e2d1c0b");

    public class User
    {
        public Guid Id { get; set; }
        public Guid ManagerId { get; set; }
        public Guid TeamLeadId { get; set; }
        public DateTime Created { get; set; }
        public DateTimeOffset LastSeen { get; set; }
        public string Name { get; set; } = "";
        public string Password { get; set; } = "";
    }

    static User CreateUser() =>
        new()
        {
            Id = first,
            ManagerId = second,
            // The same value as Id: gets the same placeholder, so the snapshot still shows they are equal.
            TeamLeadId = first,
            Created = new(2026, 11, 25, 10, 30, 0, DateTimeKind.Utc),
            LastSeen = new(2026, 11, 25, 11, 0, 0, TimeSpan.FromHours(2)),
            Name = "Ada",
            Password = "secret"
        };

    [Fact]
    public Task GuidsAndDates() =>
        Verify(CreateUser());

    [Fact]
    public Task KeepGuidsAndDates() =>
        Verify(CreateUser())
            .DontScrubGuids()
            .DontScrubDates();

    [Fact]
    public Task IgnoreMemberByName() =>
        Verify(CreateUser())
            .IgnoreMember("Password");

    [Fact]
    public Task IgnoreMemberOfType() =>
        Verify(CreateUser())
            .IgnoreMember<User>(_ => _.Created)
            .IgnoreMember<User>(_ => _.Password);

    [Fact]
    public Task IgnoreMemberInJson() =>
        VerifyJson("""{"name": "Ada", "password": "secret"}""")
            .IgnoreMember("password");

    [Fact]
    public Task InlineGuids() =>
        Verify($"Created {first}, then {second}, then {first} again.")
            .ScrubInlineGuids();

    [Fact]
    public Task LinesContaining() =>
        Verify("keep\ndrop: token=abc\nkeep too")
            .ScrubLinesContaining("token=");

    [Fact]
    public Task CustomScrubber() =>
        Verify("Build 1234 finished in 56s")
            .AddScrubber(text => System.Text.RegularExpressions.Regex.Replace(text, @"\d+s", "{duration}"));
}
