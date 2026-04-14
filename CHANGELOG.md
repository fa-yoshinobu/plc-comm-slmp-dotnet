# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.8] - 2026-04-14

### Added
- `SlmpPlcFamily` and fixed PLC-family default resolution for high-level client options, helper parsing, and device-range lookups.

### Changed
- High-level string address parsing and device-range catalog helpers now follow explicit PLC-family rules, with `iQ-F` `X/Y` as octal and other supported families as hexadecimal.

## [0.1.7] - 2026-04-14

### Added
- Public device-range catalog APIs and user docs for supported SLMP device families and ranges.
- `SlmpConnectionProfileProbe` support, CLI coverage, and regression tests for profile probing and CPU operation-state inspection flows.

### Changed
- `QueuedSlmpClient` and the sample CLI now expose the new device-range and connection-profile probe helpers alongside the existing high-level API surface.

## [0.1.6] - 2026-04-13

### Added
- Guard tests for unsupported long-timer direct reads and unsupported `LCS/LCC` random, block, and monitor-registration command paths.

### Changed
- `SlmpClient` now rejects unsupported long-timer and long-counter-state command combinations before any PLC I/O so the .NET client matches the cross-library consistency rules.

## [0.1.5] - 2026-04-13

### Changed
- CI and release workflows now materialize `plc-comm-slmp-cross-verify/specs/shared` before build and test so the packaged library keeps using the canonical shared verification vectors.

## [0.1.4] - 2026-04-01

### Removed
- `Step Relay S`: removed from the public device parser and device-code table. `TS/LTS/STS/LSTS/CS/LCS` remain supported.
- Stale current-doc references to file commands and PLC-initiated ondemand (`2101`), which are not part of the implemented public API.
- Unstable CLI auto profile flags: removed `--series auto` and `--frame-type auto` from the public `connection-check` and `other-station-check` commands.
- `sync_from_python.bat`: removed the obsolete doc-sync helper now that current release docs are maintained directly in this repository.

### Added
- String-address overloads for high-level helpers (`ReadTypedAsync`, `WriteTypedAsync`, `WriteBitInWordAsync`, `ReadWordsAsync`, `ReadDWordsAsync`).
- Queued high-level helper overloads so `QueuedSlmpClient` can call typed helpers directly.
- `SlmpDeviceParser`: added `F` (Annunciator), `LCS`, `LCC`, `LCN` (Long Counter Contact/Coil/Current) to the prefix table, placed before shorter prefixes to ensure correct longest-match ordering.
- `release_check.bat`: added a release-preflight batch entry point that runs CI and docs generation together.

### Changed
- User-facing docs now describe the high-level helper layer as the primary API surface.
- `PlcComm.Slmp.HighLevelSample` and `PlcComm.Slmp.QueuedSample` now use only the high-level API path in their recommended flows.
- Expanded XML comments for user-facing high-level helpers so DocFX output carries clearer parameter, return, and usage guidance.
- Refreshed the published DocFX site after the high-level API unification and the explicit `SingleRequest` / `Chunked` helper split.

### Fixed
- `ReadNamedAsync` / `PollAsync` now compile the address plan once and batch direct word/DWord reads via `0403 random read` when possible.
- Reduced TCP request overhead by removing redundant `NetworkStream.FlushAsync`, trimming TCP receive-path allocations, and replacing several `List<byte>.ToArray()` builders with exact-size payload builders in extended random / monitor-ext / label commands.
- `SlmpQualifiedDeviceParser.Parse`: `U\G` now sets `DirectMemorySpecification = 0xF8` (`DIRECT_MEMORY_MODULE_ACCESS`) and `U\HG` sets `0xFA` (`DIRECT_MEMORY_CPU_BUFFER`); previously both defaulted to `0x00`, causing the wrong 10-byte generic format instead of the pcap-verified 11-byte format.
- `WriteWordsSingleRequestAsync` and `WriteDWordsSingleRequestAsync` now stay covered by regression tests that fail before transport dispatch when the requested point count exceeds the single-request limits.

## [0.1.2] - 2026-03-22

### Changed
- Unified `Directory.Build.props` with `TreatWarningsAsErrors`, `EnableNETAnalyzers`, and `AnalysisLevel=latest-recommended`.
- Enriched NuGet package metadata: added `PackageTags`, `PackageProjectUrl`, `PackageReadmeFile`, symbol package settings (`snupkg`), and source-link support.
- Fixed misleading `SlmpClient` doc comment: port default documented as 1025 (was "5000 or 1025").
- Fixed solution name reference in `RELEASE_PROCESS.md` and `README.md` (`PlcCommSlmp.sln` -> `PlcComm.Slmp.sln`).

## [0.1.0] - 2026-03-19

### Added
- Bootstrap `plc-comm-slmp-dotnet` solution with library, CLI sample, and tests.
- Core `SlmpClient` transport and binary 3E/4E request handling.
- Core read/write, type-name, remote control, and clear-error APIs.
- `connection-check` and `other-station-check` CLI commands.
- Mixed block write retry handling for `0xC056`/`0xC05B`/`0xC061`/`0x414A`.
- `compatibility-probe` CLI command with markdown/json report output.
- `g-hg-ExtendedDevice-coverage` CLI command with optional write-check flow.
- `compatibility-matrix-render` command for dotnet probe outputs.
- `ExtendedDevice-device-recheck`, `read-soak`, `mixed-read-load`, and `tcp-concurrency` CLI probes.
- `QueuedSlmpClient` for single-TCP-connection serialized execution.
- Extended Specification extended device read/write support.
- GitHub Actions CI workflow (`.github/workflows/ci.yml`).
- Initial user/maintainer/validation documents.

### Fixed
- iQ-R random bit write payload encoding for `1402` write-check path.
