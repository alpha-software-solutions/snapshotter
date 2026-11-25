# Snapshotter

Snapshot testing for .NET that works with any test framework, or with none at all.

Snapshotter serializes a value, writes it to a `.received.` file next to your test and compares it with the
committed `.verified.` file. If they match, the received file is removed. If they don't, it throws. Every
test framework reports an exception as a failure, and so does a console app or a CI step.

* No dependency on xUnit, NUnit, MSTest, TUnit or anything else.
* No dependencies at all on .NET 8 and later; `System.Text.Json` on .NET Standard 2.0 (.NET Framework 4.6.1+).
* MIT licensed, written from scratch.


## Install

```
dotnet add package AlphaSoftware.Snapshotter
```

With implicit usings on (the SDK default), `Verify(...)` and the rest are available in every file.
Add `*.received.*` to your `.gitignore`.


## Use it

The call is the same whichever framework runs the test:

```csharp
public class OrderTests
{
    [Fact]          // or [Test], [TestMethod], TUnit's [Test]
    public Task Totals() =>
        Verify(new Order(1001, ["apple", "pear"], 12.5m));
}
```

The first run fails, because there is nothing to compare against yet, and writes
`OrderTests.Totals.received.json`:

```json
{
  "Id": 1001,
  "Lines": [
    "apple",
    "pear"
  ],
  "Total": 12.5
}
```

If it's right, rename it to `OrderTests.Totals.verified.json` and commit it, or rerun with
`SNAPSHOTTER_ACCEPT=1` to accept everything that changed. From then on the test fails whenever the output
changes, with a message showing the first line that differs:

```
Snapshot 'OrderTests.Totals' does not match the verified file.
  Verified: /repo/tests/OrderTests.Totals.verified.json
  Received: /repo/tests/OrderTests.Totals.received.json

First difference, line 7:
  - verified:   "Total": 12.5
  + received:   "Total": 99
```


## What can be verified

| Call | Snapshot |
|---|---|
| `Verify(object)` | Indented JSON, `.json` |
| `Verify(string)` | The text itself, `.txt` (change with `.UseExtension("md")`) |
| `Verify(Task<T>)` | The awaited result |
| `VerifyJson(string)` | The JSON, re-indented and scrubbed, property order kept |
| `Verify(byte[], "png")`, `Verify(Stream, "png")` | The bytes, compared exactly |
| `VerifyFile(path)` | The file, as text for known text formats, otherwise byte for byte |
| `Throws(() => ...)`, `ThrowsAsync(async () => ...)` | The exception: type, message, extra properties, inner exceptions, no stack trace |

Objects are serialized with `System.Text.Json`: public properties in declaration order, enums as names,
reference cycles cut, `Type` values as their name, and delegate and reflection members left out.
Exception messages are written the same on .NET Framework and .NET, so one snapshot serves both.


## Scrubbing

Values that change between runs are replaced with numbered placeholders. The same value always gets the
same placeholder, so the snapshot still shows which values were equal:

```json
{
  "Id": "Guid_1",
  "ManagerId": "Guid_2",
  "TeamLeadId": "Guid_1",
  "Created": "DateTime_1"
}
```

| Option | Effect |
|---|---|
| `.DontScrubGuids()`, `.DontScrubDates()` | Keep the real values |
| `.ScrubInlineGuids()` | Also replace GUIDs inside longer strings and text |
| `.IgnoreMember("Password")` | Leave out a member by name, on every type and in `VerifyJson` |
| `.IgnoreMember<User>(_ => _.Created)` | Leave out one member of one type (and its subtypes) |
| `.IgnoreNullValues()` | Leave out null members |
| `.ScrubLinesContaining("token=")` | Remove whole lines |
| `.AddScrubber(text => ...)` | Rewrite the final text however you like |
| `.AddConverter(converter)` | Add a `System.Text.Json` converter |

Set defaults for every test once, for example in a module initializer:

```csharp
public static class SnapshotDefaults
{
    [ModuleInitializer]
    public static void Init()
    {
        SnapshotSettings.Global.IgnoreMember("ETag");
        SnapshotSettings.Global.ScrubInlineGuids = true;
    }
}
```


## How snapshots are named

Snapshotter doesn't ask a test framework which test is running. It uses what the compiler records at the
call site:

| Part | Comes from | Example |
|---|---|---|
| Directory | The directory of the test source file | `tests/` |
| Type | The source file name, up to its first dot | `OrderTests.cs` → `OrderTests` |
| Method | The calling member | `Totals` |

So keep a test class in a file named after it. Override any part with `.UseDirectory("Snapshots")`,
`.UseTypeName(...)`, `.UseMethodName(...)` or `.UseFileName(...)`. A lambda inside a test uses the test's
name, and top level statements are named `Main`.

Each snapshot may only be verified once per run, so two verifications in one test need different names.
Snapshotter tells you if two collide.

Because snapshots are found through the source path, tests run where the source is. The package turns off
`DeterministicSourcePaths` for the projects that reference it, which would otherwise rewrite that path on
CI builds.


## Parameterized tests

Test frameworks know a theory's arguments; Snapshotter doesn't, so pass them in:

```csharp
// NumberTests.cs
[Theory]
[InlineData(1, "one")]
[InlineData(2, "two")]
public Task Words(int number, string word) =>
    Verify($"{number} is {word}")
        .UseParameters(number, word);   // NumberTests.Words_number=1_word=one.verified.txt
```

The names (`number`, `word`) are read from the test method. If it can't be found, use
`.UseTextForParameters("case1")`, which needs no lookup.


## Accepting changes and diff tools

* Rename `.received.` files to `.verified.`, or
* set `SNAPSHOTTER_ACCEPT=1` and rerun (or `.AutoAccept()` / `SnapshotSettings.Global.AutoAccept = true`).

To open a diff tool on a mismatch, set `SNAPSHOTTER_DIFF_TOOL` (or `SnapshotSettings.Global.DiffTool`) to
a command with `{received}` and `{verified}` placeholders, for example
`code --diff "{received}" "{verified}"`. It never runs on a build server.

Comparison ignores line endings, a byte order mark and a final newline, so snapshots behave the same on
every OS and in every editor.


## Building

```
git clone https://github.com/alpha-software-solutions/snapshotter.git
cd snapshotter
dotnet build AlphaSoftware.Snapshotter.slnx -c Release
dotnet test --solution AlphaSoftware.Snapshotter.slnx -c Release --no-build   # xUnit, NUnit, MSTest and TUnit
dotnet pack src/AlphaSoftware.Snapshotter -c Release -o artifacts
dotnet run --project samples/ConsoleSample                                     # no test framework, uses the packed nupkg
```

`SNAPSHOTTER_ACCEPT=1` accepts new snapshots, but `FailureTests` checks what happens on a mismatch and fails
while it is set; run them again without it.


## License

MIT. See [LICENSE](LICENSE).
