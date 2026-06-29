# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

**Entry labels**

- `Release`: Package/version metadata and publishing preparation.
- `Library`: Runtime behavior, public API, protocol handling, or validation in the distributed library.
- `Docs`: README, user guides, generated API docs, or other documentation-only changes.
- `Samples`: Examples, sample flows, sample scripts, or sample applications.
- `Tests`: Test suites, test fixtures, golden vectors, or verification data.
- `Tooling`: Developer/operator command-line tools and helper utilities.
- `CI`: Release checks, workflow scripts, or automation-only changes.

## [1.1.1] - 2026-06-29

### Changed
- Release: Bumped package metadata to `1.1.1`.
- Docs: Documented explicit named-address dtype requirements and `SlmpEndCodes.GetMessage` null behavior in existing user docs.
- Samples: Updated high-level and queued samples to use explicit dtype suffixes.

## [1.1.0] - 2026-06-29

### Changed
- Release: Bumped package metadata to `1.1.0`.
- Library: Multi-targeted the package for `net8.0`, `net9.0`, and `net10.0`.
- Library: Made named-address parsing and typed read/write helpers require explicit dtype suffixes such as `:U`, `:S`, `:D`, `:L`, `:F`, or `:BIT`; bare devices no longer default to `U`, `BIT`, or long-timer `D`.
- Library: Removed embedded localized SLMP end-code message text; end-code helpers now return stable code-derived keys while message lookup hooks return `null`.
- Docs: Corrected the SLMP .NET BIT helper documentation.
- Docs: Updated the SDK prerequisite guidance for the multi-target package.
- Samples: Made the high-level and queued samples require an explicit PLC profile instead of relying on implicit defaults.
- Samples: Updated safe write examples to restore the original PLC values after demonstration writes.
- Tests: Updated `Microsoft.NET.Test.Sdk` to `18.7.0`.
- Tests: Updated high-level address parser and shared-spec vectors for explicit dtype requirements.
- Tests: Updated SLMP end-code helper coverage for code-derived keys and non-embedded messages.
- Tests: Multi-targeted the library test project for `net8.0`, `net9.0`, and `net10.0`.
- CI: Installed .NET 8, .NET 9, and .NET 10 SDKs in CI, sample-build, and release workflows.

### Fixed
- Library: Made `BIT_IN_WORD` helper addresses require an explicit bit index such as `D100.0` through `D100.F`; `D100:BIT_IN_WORD` now fails in `ParseAddress`, `ReadNamedAsync`, and `WriteNamedAsync` instead of silently reading or writing bit 0.
- Tests: Added coverage for rejecting `BIT_IN_WORD` addresses without an explicit bit index.
- Tests: Adjusted an async guard test assertion so it remains compatible with the C# language version used by the `net8.0` target.

## [1.0.0] - 2026-06-24

### Changed
- Release: Bumped NuGet and sample project metadata to `1.0.0` for the first stable release line.

### Fixed
- Library: Reject `RemoteRunAsync` clear modes outside `0`, `1`, and `2` before building the SLMP request payload.
