# Releasing

Releases are automated with [release-please](https://github.com/googleapis/release-please) and driven by [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/). Nobody picks version numbers or writes tags by hand.

| Component | Package | Tag | Changelog |
|---|---|---|---|
| `Snapshotter` | `AlphaSoftware.Snapshotter` (NuGet) | `Snapshotter-v1.2.3` | [`CHANGELOG.md`](../CHANGELOG.md) |

## How a release happens

1. Commits land on `master`. Only `feat`, `fix`, `perf`, `revert` and breaking changes (`!` or a `BREAKING CHANGE:` footer) are releasable; `docs`, `chore`, `ci`, `build`, `test`, `refactor` and `style` never trigger a release on their own.
2. On every push to `master`, the **Release** workflow runs release-please. When there are releasable changes, it opens or updates a release PR (`chore(master): release Snapshotter 0.2.0`) that bumps the version and adds the changelog entry.
3. Merge the release PR when you want to ship. You can edit its changelog first.
4. release-please tags the merge commit and creates the GitHub Release. The same workflow run then runs the full CI, packs the package with the release's version, publishes it to nuget.org and attaches it to the release.

Versions follow [SemVer](https://semver.org/spec/v2.0.0.html). Before 1.0, `feat` and breaking changes bump the minor version and `fix` the patch; from 1.0, breaking changes bump the major. To force a version, add a `Release-As: 1.0.0` footer to a commit.

Dependabot's commits use the `build(deps)` and `ci(deps)` prefixes, so dependency updates ship with the next release rather than triggering one. To release a dependency update on its own (e.g. a security fix), merge it and push a `fix(deps): ...` commit describing it.

Between releases, builds of `master` are previews of the next patch (`0.1.1-preview.0.5` is five commits after `Snapshotter-v0.1.0`, via [MinVer](https://github.com/adamralph/minver)), uploaded as workflow artifacts. They are not published.

## Release PRs and CI

CI doesn't build or test release PRs: they only change the version and changelog, and the Release workflow runs the full CI before it publishes anything. Instead, each Release run reports the required **CI result** check on the open release PR, so you can merge it as soon as you want to ship. If anything else is pushed to a release PR, CI checks it like any other pull request.

To have release PRs opened with a token whose PRs trigger workflows normally, create a [fine-grained personal access token](https://github.com/settings/personal-access-tokens/new) for this repository with **Contents**, **Pull requests** and **Issues** set to read and write, then:

```bash
gh secret set RELEASE_PLEASE_TOKEN --repo alpha-software-solutions/snapshotter
```

## nuget.org

The publish job runs in the `release` environment, which only accepts deployments from `master`. Pick one way to authenticate.

**Trusted publishing (recommended, no long-lived secrets)**

1. On nuget.org, go to **Trusted Publishing** → **Create**, with the **AlphaSoftware** organization as the policy owner (so the package ID is created under it) and:
   - Repository owner: `alpha-software-solutions`
   - Repository: `snapshotter`
   - Workflow file: `release.yml`
   - Environment: `release`
2. Store your nuget.org **username** (not email) as a repository variable:
   ```bash
   gh variable set NUGET_USER --repo alpha-software-solutions/snapshotter --body "<nuget.org username>"
   ```

**API key**

Create a key on nuget.org with *Push new packages and package versions* for `AlphaSoftware.Snapshotter`, then:

```bash
gh secret set NUGET_API_KEY --repo alpha-software-solutions/snapshotter --env release
```

When `NUGET_USER` is set, the workflow uses trusted publishing; otherwise it falls back to `NUGET_API_KEY`.

## When something fails

The GitHub Release and tag exist as soon as the release PR is merged; the package follows once CI passes. If a later job fails:

- **Something outside the workflow** (a registry outage, missing credentials): fix it and re-run the failed jobs from the workflow run.
- **The workflow itself**: re-runs use the workflow file of the original commit, so fix it on `master`, then run **Release** manually with **publish** checked (`gh workflow run release.yml -f publish=true`). That builds the latest release again from its tag and publishes it.

Pushes use `--skip-duplicate`, so a version that already went out is skipped. Don't delete the tag: release-please uses it to find the last release.

## Configuration files

- `release-please-config.json`: the component and versioning rules.
- `.release-please-manifest.json`: the last released version. release-please updates it in every release PR; don't edit it by hand.
