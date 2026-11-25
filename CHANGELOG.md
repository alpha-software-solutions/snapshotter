# Changelog

## 0.1.0 (2026-11-25)


### ⚠ BREAKING CHANGES

* the API follows Verify (VerifySettings, VerifierSettings, SettingsTask, VerifyException) instead of SnapshotSettings and SnapshotTask, and objects are written in Verify's format to .txt files.
* the package is AlphaSoftware.Snapshotter and the namespace AlphaSoftware.Snapshotter.

### Features

* add Snapshotter, snapshot testing for any test framework ([d99b840](https://github.com/alpha-software-solutions/snapshotter/commit/d99b8405ba8986e99b7447fed01262a9e263fed0))
* match Verify's API, snapshot format and naming ([0885e3c](https://github.com/alpha-software-solutions/snapshotter/commit/0885e3cb6db6fe6ab45b9f2e1bfac457c7436db3))


### Code Refactoring

* rename the package to AlphaSoftware.Snapshotter ([04127fd](https://github.com/alpha-software-solutions/snapshotter/commit/04127fd7f54a3dd42c4af76fa29b5b23a5e038bb))
