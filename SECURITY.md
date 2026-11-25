# Security Policy

Snapshotter runs inside test suites and build pipelines: it reads and writes snapshot files next to the test source and can start a diff tool. Bugs that let it write outside those places, run something other than the configured or detected diff tool, or leak data that its scrubbers are documented to remove are security issues.

## Reporting a vulnerability

Please **do not** open a public issue. Report privately through GitHub:
[**Report a vulnerability**](https://github.com/alpha-software-solutions/snapshotter/security/advisories/new).

Include the Snapshotter version, the .NET version and OS, and a proof of concept if you have one. You'll get an acknowledgement within a few days. A fix and advisory are coordinated with you before anything is disclosed.

## In scope

- Snapshot names, parameters or settings that make Snapshotter write or delete files outside the snapshot directory
- Diff tool launching that runs an unexpected program or passes unescaped arguments to a shell
- Vulnerabilities in the published package (`AlphaSoftware.Snapshotter` on NuGet)

## Out of scope

- Secrets that end up in snapshots because a test serialized them; scrub or ignore those members
- Vulnerabilities in diff tools themselves

## Supported versions

Until 1.0, only the latest release receives security fixes.
