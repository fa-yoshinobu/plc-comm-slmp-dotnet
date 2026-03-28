# Release Checklist

This document is the release checklist for `PlcComm.Slmp`.

## Scope

Confirm that the release contains only public .NET assets:

- `src/PlcComm.Slmp`
- `samples/`
- `README.md`
- `CHANGELOG.md`
- `LICENSE`

Confirm that local output is excluded:

- `bin/`
- `obj/`
- `local_folder/`
- `build_check.log`

## Versioning

Before packaging:

1. Update `<Version>` in `src/PlcComm.Slmp/PlcComm.Slmp.csproj`.
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
   - `QueuedSlmpClient` exposes all new methods added to `SlmpClient`.
4. Release consistency
   - GitHub Release notes mention the package version.

## GitHub Actions

Repository workflows:

- `.github/workflows/ci.yml`
  - restore, build, and test on Windows for pushes and pull requests
- `.github/workflows/release.yml`
  - build release artifacts on tag pushes
  - create or update a GitHub Release for `v*` tags
  - optionally push `.nupkg` to NuGet when `NUGET_API_KEY` is configured

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

## Publish Order

Recommended order:

1. Merge the release commit.
2. Verify CI on that commit.
3. Create and push the version tag.
4. Let the release workflow build the package artifacts.
5. If `NUGET_API_KEY` is configured, let the workflow publish to NuGet.
