## What and why

<!-- What does this change, and why? Link the issue it addresses (e.g. "Fixes #123"). -->

## How was it tested?

- [ ] `dotnet test --solution AlphaSoftware.Snapshotter.slnx`
- [ ] Console sample against the packed package (`dotnet pack src/AlphaSoftware.Snapshotter -c Release -o artifacts`, then `dotnet run --project samples/ConsoleSample`)

## Checklist

- [ ] Title and commits follow [Conventional Commits](https://github.com/alpha-software-solutions/snapshotter/blob/master/CONTRIBUTING.md#commit-messages) (`type(scope): description`)
- [ ] No code copied from Verify or other projects (see [CONTRIBUTING.md](https://github.com/alpha-software-solutions/snapshotter/blob/master/CONTRIBUTING.md#clean-room))
- [ ] Public APIs have XML docs
- [ ] `README.md` and `docs/` updated if behavior or configuration changed
