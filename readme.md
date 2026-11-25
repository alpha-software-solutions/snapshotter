# Snapshotter

Snapshot testing for .NET that works with any test framework, or with none at all.

Snapshotter serializes the value you give it, writes it to a `.received.` file and compares it to the
committed `.verified.` file next to your test. If they differ, it throws. Every test framework treats
an exception as a failure, and so does any other host: a console app, a script, a CI step.

It is built on the MIT licensed [Verify](https://github.com/alpha-software-solutions/Verify) engine,
which it bundles. There is no maintenance fee, no build-time sponsorship check, and no dependency on
xUnit, NUnit, MSTest, TUnit or anything else.


## Install

```
dotnet add package Snapshotter
```

With implicit usings enabled (the SDK default), `Verify(...)` and the rest are available everywhere in
the project, with no `using` needed.


## Use it

The call is the same whichever framework runs the test.

```csharp
// xUnit
public class OrderTests
{
    [Fact]
    public Task Totals() =>
        Verify(new Order(1001, ["apple", "pear"], 12.5m));
}

// NUnit
public class OrderTests
{
    [Test]
    public Task Totals() =>
        Verify(new Order(1001, ["apple", "pear"], 12.5m));
}

// MSTest
[TestClass]
public class OrderTests
{
    [TestMethod]
    public Task Totals() =>
        Verify(new Order(1001, ["apple", "pear"], 12.5m));
}

// TUnit
public class OrderTests
{
    [Test]
    public Task Totals() =>
        Verify(new Order(1001, ["apple", "pear"], 12.5m));
}
```

All four write and compare `OrderTests.Totals.verified.txt`:

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

And without a test framework:

```csharp
// Program.cs
await Verify(order).UseMethodName("Order");      // Program.Order.verified.txt
await VerifyJson(json).UseMethodName("Status");   // Program.Status.verified.txt
```

The engine's whole API is available: `VerifyJson`, `VerifyXml`, `VerifyFile`, `VerifyDirectory`,
`VerifyZip`, `Throws`, `ThrowsTask`, `VerifyTuple`, `Combination`, and every `VerifySettings` option
(scrubbers, ignore members, `UniqueForRuntime`, converters and so on). See the
[Verify documentation](https://github.com/alpha-software-solutions/Verify/tree/main/docs) for all of it.


## How snapshots are named

Framework adapters ask the test framework which test is running. Snapshotter doesn't depend on one, so it
uses what the compiler records at the call site instead:

| Part of the name | Comes from | Example |
|---|---|---|
| Type | The source file name, up to its first dot | `OrderTests.cs` → `OrderTests` |
| Method | The calling member (`[CallerMemberName]`) | `Totals` |

For the usual one-class-per-file layout that is the same name the framework adapters produce, so
existing `.verified.` files keep matching when you switch to Snapshotter. If the names don't fit, set
them yourself with `UseTypeName(...)`, `UseMethodName(...)` or `UseFileName(...)`.

A few things follow from this:

* Two verifications in one test method need distinct names (`UseMethodName`, or `UseTextForParameters`),
  just as with any Verify adapter.
* A lambda or local function inside a test uses the test method's name.
* A nested test class is named after its file, where the adapters use `Outer.Inner`. Use `UseTypeName` to
  keep existing snapshots.
* Top-level statements are named `Main`, and `.ctor` becomes `ctor`.


## Parameterized tests

A framework adapter reads a theory's arguments from the test framework. With Snapshotter, pass them in:

```csharp
// NumberTests.cs
[Theory]
[InlineData(1, "one")]
[InlineData(2, "two")]
public Task Words(int number, string word) =>
    Verify($"{number} is {word}")
        .UseParameters(number, word);   // NumberTests.Words_number=1_word=one.verified.txt
```

The parameter names (`number`, `word`) are read from the test method by reflection: Snapshotter looks for
a method with that name on a type named after the source file. If it can't find exactly one, it says so
and suggests `UseTextForParameters("...")`, which needs no lookup.


## Compared to the framework adapters

| | Snapshotter | Verify.XunitV3, Verify.NUnit, ... |
|---|---|---|
| Test framework dependency | None | One each |
| Maintenance fee and build-time sponsorship check | No | Yes, on releases after 1 September 2026 |
| Test name source | Compiler (file and member name) | Test framework context |
| Theory arguments | Passed with `UseParameters(...)` | Read automatically |
| Received files attached to the test result | No | Yes, where the framework supports it |

Don't reference Snapshotter alongside upstream `Verify.*` packages, including the Verify plugins
(`Verify.Http`, `Verify.EntityFramework` and so on). They depend on the upstream `Verify` package, which
brings in its own `Verify.dll` and the fee check.


## Building

```
git clone --recursive https://github.com/alpha-software-solutions/snapshotter.git
cd Snapshotter
dotnet build src/Snapshotter.slnx -c Release
```

A Release build also packs `nugets/Snapshotter.<version>.nupkg`. The tests:

```
dotnet test --project src/Snapshotter.Tests          # xUnit v3, the main suite
dotnet test --project src/Snapshotter.NUnit.Tests
dotnet test --project src/Snapshotter.MSTest.Tests
dotnet run  --project src/Snapshotter.TUnit.Tests
dotnet run  --project samples/ConsoleSample          # consumes the packed nupkg
```

The engine comes from the [alpha-software-solutions/Verify](https://github.com/alpha-software-solutions/Verify)
fork, checked out as the `verify` submodule. Its package versions are imported from the fork's
`Directory.Packages.props`, so the bundled `Verify.dll` and the dependencies Snapshotter declares always
match. To take a newer engine:

```
git -C verify pull origin main
git add verify
```


## License

MIT. See [license.txt](license.txt). The bundled engine, and the adapter code Snapshotter is derived from,
are copyright .NET Foundation and Contributors under the same license. Snapshotter is not affiliated with
or endorsed by the VerifyTests project.
