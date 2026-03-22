# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.2] - 2026-03-22

### Changed
- Unified `Directory.Build.props` with `TreatWarningsAsErrors`, `EnableNETAnalyzers`, and `AnalysisLevel=latest-recommended`.
- Enriched NuGet package metadata: added `PackageTags`, `PackageProjectUrl`, `PackageReadmeFile`, symbol package settings (`snupkg`), and source-link support.
- Fixed misleading `SlmpClient` doc comment: port default documented as 1025 (was "5000 or 1025").
- Fixed solution name reference in `RELEASE_PROCESS.md` and `README.md` (`PlcCommSlmp.sln` → `PlcComm.Slmp.sln`).

## [0.1.0] - 2026-03-19

### Added
- Bootstrap `plc-comm-slmp-dotnet` solution with library, CLI sample, and tests.
- Core `SlmpClient` transport and binary 3E/4E request handling.
- Core read/write, type-name, remote control, and clear-error APIs.
- `connection-check` and `other-station-check` CLI commands.
- Mixed block write retry handling for `0xC056`/`0xC05B`/`0xC061`/`0x414A`.
- `compatibility-probe` CLI command with markdown/json report output.
- `g-hg-appendix1-coverage` CLI command with optional write-check flow.
- `compatibility-matrix-render` command for dotnet probe outputs.
- `appendix1-device-recheck`, `read-soak`, `mixed-read-load`, and `tcp-concurrency` CLI probes.
- `QueuedSlmpClient` for single-TCP-connection serialized execution.
- Extended Specification extended device read/write support.
- GitHub Actions CI workflow (`.github/workflows/ci.yml`).
- Initial user/maintainer/validation documents.

### Fixed
- iQ-R random bit write payload encoding for `1402` write-check path.
