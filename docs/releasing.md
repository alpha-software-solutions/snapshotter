# Releasing

A release is a tag. Push `Snapshotter-v1.2.3` on a `master` commit and the **Release** workflow runs the full CI, packs `AlphaSoftware.Snapshotter` 1.2.3 and publishes it to nuget.org:

```bash
git switch master && git pull
git tag Snapshotter-v0.2.0
git push origin Snapshotter-v0.2.0
```

The version comes from the tag ([MinVer](https://github.com/adamralph/minver)); nothing in the repository holds it. Versions follow [SemVer](https://semver.org/spec/v2.0.0.html), and a tag like `Snapshotter-v0.2.0-preview.1` publishes a prerelease. The workflow refuses tags that aren't on `master`.

Between releases, builds of `master` are previews of the next patch (`0.1.1-preview.0.5` is five commits after `Snapshotter-v0.1.0`), uploaded as CI artifacts. They are not published.

## nuget.org

The publish job runs in the `release` environment, which only accepts deployments from `Snapshotter-v*` tags. Pick one way to authenticate.

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

- **Something outside the workflow** (a registry outage, missing credentials): fix it and re-run the failed jobs from the workflow run.
- **The code or the workflow**: fix it on `master` and release the next patch. A version can't be republished differently once it's on nuget.org.

Pushes use `--skip-duplicate`, so re-running after a version already went out is harmless.
