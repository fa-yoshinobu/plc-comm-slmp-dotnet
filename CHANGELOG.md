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

## [1.2.0] - 2026-07-05

### Changed
- Release: Bumped package metadata to `1.2.0`.
- Tooling: Normalized line-ending handling in the canonical profile JSON update script so `-SourceRoot` runs no longer report false changes.
- Library: Synced the embedded SLMP capability fixture to `plc-comm-slmp-profiles` `v1.2.1`, including `display_name` labels and Ethernet unit profiles for RJ71EN71, LJ71E71-100, and QJ71E71-100 variants.
- Library: Added `SlmpPlcProfiles.GetDisplayName(profile)` as the public UI-label helper while keeping stored PLC profile values canonical.
- Docs: Documented the profile display-name helper and canonical-ID storage guidance.
- Tests: Added canonical fixture parity coverage for profile `display_name` values.
- Samples: Added read-only multi-PLC monitoring and JSON config polling recipes with independent reconnect loops, dry-run validation, and long-form CSV output.
- Docs: Added generated .NET API reference from the public assembly surface and XML documentation comments, with CI freshness validation.
- Library: Added non-breaking SLMP specification-audit updates for manual-conformant request framing, point-limit guards, response correlation, and PLC error diagnostics.
- Library: Exposed structured PLC error information on `SlmpError.ErrorInfo` when a non-zero end-code response carries the 9-byte error information block.
- Library: Enforced documented point limits before transport: iQ-F direct bit access is limited to 3584 points, and 008x extended random/monitor routes use the 96-point / weighted-960 / 94-bit limits.
- Library: Routed long timer, long retentive timer, and long counter status reads through the dedicated long-state helper path instead of the normal bit-read path.
- Tooling: Changed the canonical profile update script default ref from `v1.0.0` to `v1.1.0`.
- Library: Kept long counter contact and coil reads on the direct bit helper used by the long-state helper path.
- Library: Added SLMP step relay `S` device parsing and read support.
- Library: Added built-in SLMP capability profiles from `plc-comm-slmp-profiles` v1.0.0 and `SlmpConnectionOptions.StrictProfile` (default `true`) so high-level APIs reject profile `blocked` / `unverified` features before transport.
- Library: Added `SlmpProfileFeatureException` for profile guard failures with profile ID, feature key, state, evidence, and the `StrictProfile=false` bypass hint.
- Library: Moved direct/random point limits to the capability table for all canonical built-in Ethernet profiles, including `melsec:qcpu` and `melsec:qnu`.
- Library: Kept the 008x extended random/monitor limits at 96 points, weighted 960, and 94 bits even when the selected profile allows larger plain random/monitor counts.
- Library: Added canonical weighted random-word write limits for `melsec:iq-l` and `melsec:iq-f`, so mixed word/dword random writes are guarded before transport.
- Library: Enforced capability write policies independently of `StrictProfile`; `S` is read-only on iQ-R/iQ-L/MX/Q/L profiles and read-write on iQ-F.
- Library: Rejected profile-unsupported device families before transport while leaving device address upper-bound checks to application/live-probe code.
- Library: Moved Q/L profile Read Block (`0x0406`) and Write Block (`0x1406`) rejection to the capability profile guard so `StrictProfile=false` can intentionally send the request and let the PLC answer.
- Library: Batched named plain-bit reads through random word-read only for `SM/X/Y/M/L/F/V/B/SB`; `TS/TC/STS/STC/CS/CC/DX/DY` stay on direct bit reads.
- Docs: Documented the Q-series Read Block (`0x0406`) and Write Block (`0x1406`) profile guard in user profiles and gotchas.
- Docs: Removed duplicated SLMP supported-register and device-range user pages and linked users to the shared SLMP Profile Reference.
- Docs: Added a Usage Guide example showing how to read `SlmpError.EndCode` and structured `ErrorInfo`.
- Docs: Added Usage Guide examples for `U...` module access, `U...HG` CPU-buffer access, and `J...` link direct extended devices.
- Docs: Removed the manual page-navigation block from Getting Started and rely on site navigation instead.
- Docs: Moved shared SLMP gotcha items to the common troubleshooting page and kept Gotchas focused on .NET-specific behavior.
- Docs: Slimmed Gotchas to library-specific items and moved shared setup/end-code symptoms to the PLC Setup Guide.
- Docs: Standardized the Gotchas page structure with KV Host Link so library-specific caveats have the same destination across protocols.
- Docs: Cleaned up obsolete maintainer notes and normalized the root TODO.
- Release: Excluded maintainer-only files, scripts, and tests from generated source archives via `.gitattributes`.
- Tooling: Changed the canonical profile update script default ref from `main` to fixed tag `v1.0.0`; `SLMP_PROFILES_REF` can still override it.

### Fixed
- Library: Aligned standard 008x extended device specifications with the manual 11-byte Q/L and 13-byte iQ-R layouts.
- Library: Matched 4E responses by request serial and discarded mismatched D4 responses before parsing the response payload.
- Library: Reject SLMP step relay `S` writes only when the selected profile marks `S` as read-only.
- Library: Reject standalone `G` and `HG` device access, including random bit writes; callers must use qualified `Un\Gn` / `Un\HGn` routes.
- Docs: Documented profile-specific `S` write policy in supported-register and gotcha guidance.
- Tests: Added coverage for long-state helper routing, `S` write rejection, and standalone `G` / `HG` random bit write rejection.
- Tests: Added canonical capability fixture comparison plus strict-profile coverage for qnudv/lcpu block/type-name guards, qnudv `StrictProfile=false`, iQ-F link-direct, iQ-F `U\G`, iQ-L HG, profile limits, and profile write policies.
- Tests: Added regression coverage that profile-specific plain random/monitor limits do not relax 008x extended command limits.
- Tests: Updated coverage so `melsec:qcpu` and `melsec:qnu` reject block read/write through the capability profile guard.
- Tests: Added named-read planning coverage for random-word-safe plain bit families versus direct-bit-only families.

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
