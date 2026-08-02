# Release Checklist

This document is the release checklist for `PlcComm.Slmp`.

## Scope

Confirm that the source archive contains the complete buildable and reviewable repository contract:

- solution and project manifests, `src/PlcComm.Slmp`, and `samples/`
- repository tests and the standard pages under `docsrc/user/`
- tracked validation and maintainer material under `.github/`, `docsrc/maintainer/`, `internal_docs/`, `scripts/`, and `tools/`
- `run_ci.bat`, `release_check.bat`, `README.md`, `CHANGELOG.md`, `LICENSE`, and repository instruction/TODO files when tracked

Confirm that local output is excluded:

- `bin/`
- `obj/`
- `local_folder/`
- `build_check.log`

## Versioning

Before packaging:

1. Update `<Version>` in `Directory.Build.props`.
2. Update `CHANGELOG.md` so the released changes are recorded in the target version section.
3. Make sure the release tag matches the package version, for example `v1.0.0`.

## Quality Gates

Run these commands locally:

```powershell
dotnet build PlcComm.Slmp.sln
dotnet test PlcComm.Slmp.sln --no-build
dotnet pack src\PlcComm.Slmp\PlcComm.Slmp.csproj -c Release
```

## Pre-Tag Review Checklist

Before creating a release tag, confirm these review items:

1. Tag alignment
   - The target tag commit matches the intended `main` commit.
2. Changelog alignment
   - Recent fixes are recorded in `CHANGELOG.md`.
3. Cross-library parity
   - Public API surface matches the SLMP Python library equivalents.
   - `SlmpClient` remains the sole public live client and its FIFO contract covers all new operations.
4. Release consistency
   - GitHub Release notes mention the package version.

## GitHub Actions

Repository workflows:

- `.github/workflows/ci.yml`
  - on pushes and pull requests, checks no-auto-publish policy, canonical profiles, restore/build/test, API-diff policy and generated-reference freshness, package/source-archive contents, formatting, and a bounded Linux lifecycle smoke test
- `.github/workflows/release.yml`
  - build release artifacts on tag pushes
  - create or update a GitHub Release for `v*` tags

## NuGet Readiness

Confirm package metadata in `src/PlcComm.Slmp/PlcComm.Slmp.csproj`:

- package id
- version
- description
- repository URL
- README
- license

## Final Git Check

Before tagging:

```powershell
git status
git diff --stat
```

Confirm:

- no accidental local files
- no generated logs
- no leftover temporary artifacts

## Final Publication Integrity Gate

Before final publication, enumerate every unchecked repository TODO and maintainer checkbox and
give each item a result or explicit release disposition. Build the shared docs site in a fresh
virtual environment and require its package version/symbol check plus strict build. After NuGet
publication, compare the GitHub `.nupkg` with the registry package after allowing only NuGet.org's
repository signature entries. Finally verify the immutable tag target, Release assets/state, docs
deployment, open release PR count, and clean working tree.

## Publish Order

Recommended order:

1. Merge the release commit.
2. Verify CI on that commit.
3. Create and push the version tag.
4. Let the release workflow build the package artifacts.
5. Publish the `.nupkg` and `.snupkg` to NuGet manually after the GitHub Release artifacts are checked.
