# Contributing to Snapshotter

Thanks for helping! Issues, ideas and pull requests are all welcome. Please follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Before you start

- **Bugs**: open an issue with the test (or console code), the snapshot you expected and the one you got, and the exception message.
- **Differences from Verify**: Snapshotter aims to behave like Verify. If it doesn't, that's a bug; link the Verify documentation that describes the behavior.
- **Features or API changes**: open an issue first so we can agree on the shape before you invest time.
- **Security issues**: don't open a public issue. See [SECURITY.md](SECURITY.md).

## Clean room

Snapshotter is an independent implementation. It matches Verify's documented behavior, public API names and snapshot format, but contains none of its code. Don't copy, port or paste source code from Verify (or any other project) into a pull request. Reading Verify's documentation and snapshot files to learn what it does is fine.

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (see `global.json`) and the .NET 10 runtime
- On Windows, the .NET Framework 4.8 developer pack, for the `net48` test runs
- Node.js 22+ and npm, for the commit-msg hook

## Repository layout

```
src/
  AlphaSoftware.Snapshotter/                 the library (netstandard2.0 and net8.0) and the MSBuild files it packs
tests/
  AlphaSoftware.Snapshotter.Tests/           the main suite (xUnit v3, net48 and net10.0)
  AlphaSoftware.Snapshotter.NUnit.Tests/     the same calls under NUnit
  AlphaSoftware.Snapshotter.MSTest.Tests/    ... MSTest
  AlphaSoftware.Snapshotter.TUnit.Tests/     ... TUnit
samples/
  ConsoleSample/                             no test framework, against the packed package
docs/                                        releasing
```

## Build and test

```bash
npm ci                                                   # installs the commit-msg hook
dotnet build AlphaSoftware.Snapshotter.slnx
dotnet test --solution AlphaSoftware.Snapshotter.slnx

dotnet pack src/AlphaSoftware.Snapshotter -c Release -o artifacts
dotnet run --project samples/ConsoleSample               # uses the package in artifacts/
```

Snapshot tests that fail leave a `.received.` file next to the `.verified.` one; rename it to `.verified.` to accept it, or see the README's *Accepting changes* section.

## Commit messages

Commit messages and pull request titles follow [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/):

```text
<type>(<optional scope>): <description>

<optional body>

<optional footers>
```

- **Types**: `feat`, `fix`, `docs`, `perf`, `refactor`, `test`, `build`, `ci`, `chore`, `style`, `revert`.
- **Scopes** (optional): `serialization`, `scrubbing`, `naming`, `diff`, `msbuild`, `samples`, `deps`.
- Write the description in the imperative, starting lowercase, without a trailing period: `fix(naming): keep dots in parameter values`. Keep the header within 100 characters.
- Mark breaking changes with `!` before the colon (`feat(naming)!: drop UseTypeName`) and a `BREAKING CHANGE:` footer.
- The type decides what gets released: `feat`, `fix`, `perf` and breaking changes trigger a release and become its changelog entry. See [docs/releasing.md](docs/releasing.md).

`npm ci` installs a commit-msg hook ([husky](https://typicode.github.io/husky/) + [commitlint](https://commitlint.js.org/)) that rejects messages that aren't Conventional Commits and warns about style (subject case, header length). The **Conventional commits** check applies the same rules to pull requests. Squash merges use the pull request title as the commit message, so the title must follow the format too. `npm run lint:commits` checks every commit on your branch against `origin/master`.

## Pull requests

- Keep changes focused; one topic per PR.
- Add or update tests in `tests/AlphaSoftware.Snapshotter.Tests`. Behavior that depends on the host (a framework, no framework) also goes in the other test projects or the console sample.
- Public APIs need XML docs. Builds treat warnings as errors.
- Match the existing style (`.editorconfig`).
- Don't edit `CHANGELOG.md`: release-please writes it from commit messages, so describe user-visible changes in a `feat` or `fix` commit.
- **CI result** must pass. Documentation-only changes skip the builds.

## Releasing (maintainers)

See [docs/releasing.md](docs/releasing.md).
