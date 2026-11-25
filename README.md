# Snapshotter

Snapshot testing for .NET that works like [Verify](https://github.com/VerifyTests/Verify), with any test framework
or with none at all.

```
dotnet add package AlphaSoftware.Snapshotter
```

Snapshotter has Verify's API, snapshot format, file names and defaults, so in most cases tests and `.verified.`
files move over unchanged. What it doesn't have is a dependency on xUnit, NUnit, MSTest, TUnit or anything else: one package works
in all of them, in a console app and in a CI script.

* No adapter packages and no `[UsesVerify]`: the test method and class are found from the call stack.
* With NUnit, xUnit v3 and TUnit, a parameterized test's arguments are picked up automatically, as Verify's adapters
  do. They are read through reflection, so nothing is referenced.
* No dependencies on .NET 8 and later; `System.Text.Json` on .NET Standard 2.0 (.NET Framework 4.6.1+).
* MIT licensed and written from scratch. It contains no Verify code.


## Use it

With implicit usings on (the SDK default), `Verify(...)` and the rest are available in every file of a project
that references the package.

```csharp
public class OrderTests
{
    [Fact]          // or [Test], [TestMethod], TUnit's [Test], or no attribute at all
    public Task Totals() =>
        Verify(new Order(1001, ["apple", "pear"], 12.5m));
}
```

The first run fails, because there is nothing to compare against yet, and writes
`OrderTests.Totals.received.txt` next to the test file:

```
{
  Id: 1001,
  Lines: [
    apple,
    pear
  ],
  Total: 12.5
}
```

Accept it by renaming it to `OrderTests.Totals.verified.txt` (or with your diff tool, or an IDE plugin), and
commit it. From then on the test fails whenever the output changes:

```
Directory: /repo/tests
NotEqual:
  - Received: OrderTests.Totals.received.txt
    Verified: OrderTests.Totals.verified.txt

FileContent:

NotEqual:

Received: OrderTests.Totals.received.txt
Verified: OrderTests.Totals.verified.txt
Diff:
6 ],
- Total: 12.5
+ Total: 99
7 }
```

Add these to your repository, as with Verify:

```gitignore
# .gitignore
*.received.*
```

```gitattributes
# .gitattributes
*.verified.txt text eol=lf working-tree-encoding=UTF-8
*.verified.xml text eol=lf working-tree-encoding=UTF-8
*.verified.json text eol=lf working-tree-encoding=UTF-8
*.verified.bin binary
```


## Moving from Verify

1. Replace the `Verify.Xunit` / `Verify.NUnit` / `Verify.MSTest` / `Verify.TUnit` package with
   `AlphaSoftware.Snapshotter`.
2. Replace `using VerifyXunit;` (and the others) and `using VerifyTests;` with
   `using AlphaSoftware.Snapshotter;`, or remove them if you use implicit usings.
3. Remove `[UsesVerify]` and `VerifyBase` base classes; there is nothing to inherit.
4. Converters written against Argon change their base type to Snapshotter's `WriteOnlyJsonConverter<T>`; see
   [Serializer settings](#serializer-settings).

Your `.verified.` files stay as they are.


## What can be verified

| Call | Snapshot |
|---|---|
| `Verify(object)` | Serialized, `.txt` (`.json` with `UseStrictJson()`) |
| `Verify(string)`, `Verify(string, "html")` | The text itself |
| `Verify(5)`, `Verify(true)`, `Verify(guid)`, `Verify(date)` | The value as text, not scrubbed |
| `Verify(Task<T>)`, `Verify(ValueTask<T>)`, `Verify(Func<Task<T>>)`, `Verify(IAsyncEnumerable<T>)` | The awaited result |
| `VerifyJson(string or Stream)` | The JSON in the snapshot format, property order kept |
| `VerifyXml(string, Stream or XDocument)` | Pretty printed, without the declaration (`.xml`) |
| `Verify(byte[])`, `Verify(Stream)`, `Verify(bytes, "png")` | The bytes, compared exactly (`.bin` by default) |
| `VerifyFile(path)` | The file, as text for text formats, otherwise byte for byte |
| `VerifyDirectory(path)` | Every file in it, each its own snapshot in a directory named after the test |
| `Verify(Target)`, `Verify(IEnumerable<Target>)`, `Verify(object, IEnumerable<Target>)` | Explicit files |
| `Throws(...)`, `ThrowsTask(...)`, `ThrowsValueTask(...)` | The exception |

Each call returns a `SettingsTask`: chain settings onto it and await it, or return it from the test.
`await Verify(...)` gives a `VerifyResult` with the verified files and their text.


## The snapshot format

Objects are written the way Verify writes them:

* Names and strings are not quoted or escaped. Multiline strings start on the next line.
* Public fields, then public properties, in declaration order, most derived type first.
* Members holding their default value (`0`, `null`, `Guid.Empty`) are left out, except `bool` members, which are
  always written. Empty collections are left out too (`DontIgnoreEmptyCollections()` keeps them).
* Dictionaries are sorted by key (`DontSortDictionaries()` keeps their order).
* Reference loops are cut. `[Obsolete]` members are left out (`IncludeObsoletes()`).
* Enums are written by name, types by their C# name (`List<int>`), exceptions with their type, message, data,
  inner exception and a cleaned stack trace.
* Files are UTF-8 with a byte order mark, with `\n` line endings, and without a trailing newline.

`UseStrictJson()` writes real JSON instead, in `.json` files.


## Scrubbing

Values that change between runs are replaced with numbered placeholders. The same value always gets the same
number within a snapshot, so the snapshot still shows which values were equal:

```
{
  Id: Guid_1,
  ManagerId: Guid_2,
  TeamLeadId: Guid_1,
  Created: DateTime_1,
  Path: {ProjectDirectory}Data/orders.json
}
```

| On by default | |
|---|---|
| Guids | `Guid_1`, `Guid_Empty` (`DontScrubGuids()`) |
| Dates and times | `DateTime_1`, `DateTimeOffset_1`, `Date_1`, `Time_1`, `Date_MinValue` (`DontScrubDateTimes()`) |
| Directories | `{SolutionDirectory}`, `{ProjectDirectory}`, `{CurrentDirectory}`, `{TempPath}`, `{UserProfile}` |

| Opt in | |
|---|---|
| `ScrubInlineGuids()`, `ScrubInlineDateTimes("yyyy-MM-dd")` | Also inside longer strings and text |
| `ScrubNumericIds()` | Numeric members ending in `Id`: `Customer_1`, `OrderId_1` |
| `ScrubMachineName()`, `ScrubUserName()` | `TheMachineName`, `TheUserName` |
| `AddNamedGuid(id, "Admin")`, `AddNamedDate(...)` | A known value as its name |
| `IgnoreMember("Password")`, `IgnoreMember<User>(_ => _.Created)`, `IgnoreMembersWithType<T>()` | Leave members out |
| `ScrubMember(...)`, `ScrubMembersWithType<T>()` | Write `{Scrubbed}` |
| `IgnoreInstance<T>(predicate)`, `ScrubInstance<T>(predicate)` | By value |
| `ScrubLines(...)`, `ScrubLinesContaining(...)`, `ScrubLinesWithReplace(...)`, `ScrubEmptyLines()` | Line by line |
| `ScrubReplace(find, replacement)`, `AddScrubber(builder => ...)` | Anything else |

Settings can be passed in a `VerifySettings` (`Verify(target, settings)`), chained on the call, or set for every
test in a module initializer, before the first verification:

```csharp
public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        VerifierSettings.IgnoreMember("ETag");
        VerifierSettings.ScrubInlineGuids();
    }
}
```


## Naming

`{Directory}/{Type}.{Method}{_Parameters}{.Uniqueness}{#Target}.verified.{ext}`

| Part | Default | Override |
|---|---|---|
| Directory | The test source file's directory | `UseDirectory("Snapshots")`, `Verifier.DerivePathInfo(...)`, `UseUniqueDirectory()` |
| Type | The test class, `Outer.Inner` when nested | `UseTypeName(...)` |
| Method | The test method | `UseMethodName(...)`, or `UseFileName(...)` for the whole name |
| Parameters | `_number=1_word=one` | `UseParameters(...)`, `UseTextForParameters(...)`, `IgnoreParameters(...)` |
| Uniqueness | none | `UniqueForRuntime()` (`.DotNet`, `.Net`), `UniqueForRuntimeAndVersion()`, `UniqueForTargetFramework()`, `UniqueForAssemblyConfiguration()`, `UniqueForArchitecture()`, `UniqueForOSPlatform()` |

Each name may be verified only once per run, so two verifications in one test need `UseMethodName(...)`.

In a project with several target frameworks, received files carry the runtime (`.DotNet10_0.received.txt`) while
every framework shares one verified file.

Snapshots are found through the compiler's caller file path, so tests run where their source is. The package turns
off `DeterministicSourcePaths` in projects that reference it, which would otherwise rewrite that path on CI builds.

### Parameterized tests

```csharp
[Theory]
[InlineData(1, "one")]
public Task Words(int number, string word) =>
    Verify($"{number} is {word}");        // NumberTests.Words_number=1_word=one.verified.txt
```

NUnit, xUnit v3 and TUnit expose the running test's arguments, so they are used without asking. With MSTest and
xUnit v2, pass them in, as with Verify: `.UseParameters(number, word)`.


## Accepting changes and diff tools

* Rename `.received.` files to `.verified.`, accept them in a diff tool or an IDE plugin, or
* call `AutoVerify()` on a verification, or `VerifierSettings.AutoVerify()` in a module initializer.

When a snapshot is new or doesn't match, Snapshotter opens the first installed diff tool: Beyond Compare, P4Merge,
Kaleidoscope, DiffMerge, TortoiseGitMerge, TortoiseMerge, WinMerge, Meld, KDiff3, Sublime Merge, Rider, Visual Studio
Code or Visual Studio. Change the order with `DiffTools.UseOrder(...)` or `DiffEngine_ToolOrder`, add your own with
`DiffTools.AddTool(...)`, and turn it off with `DisableDiff()` or `DiffEngine_Disabled=true`. No diff tool opens on a
build server or under an AI coding agent.


## Serializer settings

```csharp
VerifierSettings.AddExtraSettings(_ =>
{
    _.Converters.Add(new CompanyConverter());
    _.DefaultValueHandling = DefaultValueHandling.Include;
});

class CompanyConverter : WriteOnlyJsonConverter<Company>
{
    public override void Write(VerifyJsonWriter writer, Company company)
    {
        writer.WriteMember(company, company.Name, "Name");
        writer.WriteMember(company, company.Employees, "Employees");
    }
}
```

Snapshotter has its own serializer, so `AddExtraSettings` takes Snapshotter's `SerializerSettings`
(`Converters`, `DefaultValueHandling`, `NullValueHandling`) rather than Argon's `JsonSerializerSettings`.


## Also supported

`AppendValue(...)`, `AppendFile(...)`, `AppendContentAsFile(...)`; `UseStringComparer(...)`,
`UseStreamComparer(...)`, `VerifierSettings.RegisterStringComparer(...)`; `OnVerify`, `OnFirstVerify`,
`OnVerifyMismatch`, `OnDelete`; `VerifierSettings.TreatAsString<T>()`, `SortPropertiesAlphabetically()`,
`UseUtf8NoBom()`, `FixNewlinesOnRead()`, `IgnoreTrailingNewline()`, `OmitContentFromException()`,
`NameForParameter<T>()`, `AddExtraDateTimeFormat(...)`; `FileExtensions.AddTextExtension(...)`.


## Not supported (yet)

Verify's plugin ecosystem (Verify.EntityFramework, Verify.Http, image comparison and the rest), file converters
(`RegisterFileConverter`), inline snapshots, `VerifyZip`, `VerifyTuple`, combinations, `Recording`,
`OrderEnumerableBy`, `MemberConverter`, the split unique-directory mode, DiffEngineTray, and the dangling-snapshot and
convention checks. Issues and pull requests are welcome.


## Building

```
git clone https://github.com/alpha-software-solutions/snapshotter.git
cd snapshotter
dotnet build AlphaSoftware.Snapshotter.slnx
dotnet test --solution AlphaSoftware.Snapshotter.slnx   # xUnit, NUnit, MSTest and TUnit
dotnet pack src/AlphaSoftware.Snapshotter -c Release -o artifacts
dotnet run --project samples/ConsoleSample               # no test framework, uses the packed package
```

See [CONTRIBUTING.md](CONTRIBUTING.md).


## License

MIT. See [LICENSE](LICENSE). Snapshotter is not affiliated with Verify or its authors.
