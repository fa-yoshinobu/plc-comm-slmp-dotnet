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

## [Unreleased]

## [5.0.0] - 2026-08-07

- Library: `PollAsync` now prepares and validates its immutable Random Read payload and compact decode indexes once per stream, then reuses them for every FIFO-controlled cycle without changing timing, cancellation, close, or error behavior.
- Library: Typed command decoders now parse a private `ReadOnlyMemory<byte>` view over the owned response frame; public raw/trace/error and byte-result surfaces remain owned. Extended Random and Monitor builders now use a validated exact-size two-pass encoder with one final payload allocation and no per-device encoded arrays.
- Tests: Added allocation/encoding counters and regressions for one-time polling preparation, compact indexed decode, typed/raw response ownership, and exact-size Extended payload construction.
- Docs: Corrected generated `init` accessors, timeout lifecycle wording, the Multi-PLC target argument, the recommended-entry table, and state-changing extended-device/Clear Error examples.
- Tests: Added real mutable/init-only generator fixtures and executable documentation-contract checks, including the exact Multi-PLC dry-run command.
- Library: Nonzero-end-code responses that contain structured error information now require the embedded route, command, and subcommand to match the active request. A mismatch is malformed, retires the transport, and makes a transmitted state change outcome-unknown; matching additional error bytes are retained in `SlmpErrorInfo.Extra`.
- BREAKING: Contiguous Direct, Random, Monitor-registration, Block, and applicable Extended Device operations now reject a request whose consumed device span exceeds the selected 24-bit or 32-bit wire address field before client state or transport activity. Packed word access to bit devices consumes 16 device numbers per word, ordinary DWord/Float32 access consumes two word devices per value, and bit blocks consume 16 bit devices per block point; no configured PLC device-range limit is inferred.
- Library: Typed, named, polling, long-timer, and bit-in-word helpers now finish route/span/profile/writable-target admission before FIFO waiting; invalid bit-in-word targets send neither the read nor write request.
- Library: Named reads now reject `LTN`/`LSTN` Direct Read families during complete pre-transport planning, remove unreachable long-timer named execution state, and direct callers to typed or explicit long-timer helpers.
- Library: Semantic bit APIs now use one exhaustive device-unit classifier, reject every word-addressable family before transport, retain explicit packed word access to bit devices, and require exact typed/named `BIT` versus numeric device units.
- Library: A fully correlated and command-decoded success or framed PLC end-code now wins a concurrent close or disposal; incomplete reads remain closed and possibly transmitted state changes remain outcome-unknown with reason `Closed`.
- Library: Direct DWord/Float32 counts are validated in public value units before conversion or allocation, and named-target plus U/J-qualified numeric text now reports field-specific bounded `FormatException` failures without overflow or truncation.
- Library: Ack-only commands now reject non-empty success payloads as an unknown malformed-response outcome; raw commands retain their explicit response payload and remote reset retains its send-only contract.
- Library: Typed and named write plans now complete semantic validation before FIFO admission, and `WriteBitInWordAsync` accepts word devices only.
- Samples: The CLI now reuses bounded named-target parsing for route fields, validates ushort counts explicitly, and uses the library's canonical device-unit classifier.
- Tests: Added exhaustive device-unit surfaces, named long-timer zero-send plans, DWord/Float32 boundaries, bounded textual fields, and deterministic close/dispose result-precedence races.
- Samples: All six user-facing repository samples now target `net10.0`; building the samples requires the .NET 10 SDK. The library package remains multi-targeted for `net8.0`, `net9.0`, and `net10.0`.
- Library: One immutable absolute transaction deadline now covers lazy IPv4 connection, TCP/UDP send, complete response framing, route/serial correlation, and response decoding. FIFO queue wait remains outside that deadline, and `Timeout` plus `MonitoringTimer` are snapshotted when the call is admitted.
- Library: Added dedicated `SlmpTimeoutException`, `SlmpTransportException`, `SlmpNotConnectedException`, and `SlmpOperationOutcomeUnknownException` classifications. A state-changing request interrupted after bytes may have been sent reports a structured timeout, cancellation, close, malformed-response, or transport reason and is never retried automatically.
- Tests: Added state-changing timeout/cancellation/close/transport/malformed-response classification, admission-time option snapshots, and full-deadline regressions across TCP/UDP and 3E/4E.
- Library: Ordinary `SlmpClient` operations now use one built-in arrival-order FIFO admission queue. One client permits one complete wire transaction at a time; waiting cancellation sends nothing, queue wait does not consume the transaction timeout, and `Close` or disposal rejects the active and queued transport generation.
- Library: Multi-step helpers such as bit-in-word read-modify-write retain one exclusive client turn, while separate `SlmpClient` instances remain independent.
- Tests: Added deterministic FIFO order, queued cancellation/no-send, close-generation rejection, argument snapshot, compound-operation non-interleaving, queue-wait timeout, and separate-client concurrency coverage.
- Docs: Generated API documentation now states that bit-in-word updates are two SLMP requests held in one local FIFO turn and are not PLC-atomic against PLC logic or other clients.
- CI: The NuGet package gate now restores and runs an isolated net8.0 consumer using only the generated local package.
- CI: The NuGet guard now rejects CI, cache/build, source, maintainer, release-output, and credential-like material in addition to its consumer-file allowlist.
- CI: Source-archive validation can now synthesize the complete current worktree so pre-commit review includes new files, modifications, and deletions rather than stale `HEAD` contents.
- Release: Aligned artifact roles so the registry package contains consumer runtime, native API metadata, license, README, and ecosystem-native examples where applicable while excluding repository tests and maintainer tooling; the GitHub source archive retains tracked non-hardware validation and maintainer inputs.
- Library: Audited every live API that accepts a profile-bound address: its exact canonical profile, including unit-specific profiles, must equal the client profile before request construction, counters, trace state, serial allocation, or transport activity.
- Tests: Profile-mismatch coverage verifies pre-transport rejection for direct and Extended Device paths without reducing unit profiles to their base family.
- Library: Device-range catalogs now use only canonical profile rules and the single required SD-register window. Communication failures are propagated without converting PLC errors into inferred address limits or hidden boundary probes.
- Tests: Added device-range catalog coverage proving canonical values use one SD read and Q-series unknown ranges remain unknown without runtime probing.
- Library: Audited every direct, extended, random, named, typed, and bit-in-word write entry: individual bit values remain CLR `bool` values with no numeric, string, truthy, or compatibility overload. Packed bit-block words remain a separate `ushort` wire-level API.
- Docs: README documentation links now include the shared Performance and Choosing a Language pages, and package registry metadata was expanded for discoverability. No functional change.
- Library: TCP and UDP connections are now IPv4-only. IPv6 literals are rejected before socket creation, and hostnames use the first IPv4 resolver result without falling back to IPv6.
- Library: Corrected array-label bit/byte wire sizing, enforced exact two-byte-padded array-write data, and rejected null, empty, or odd random-label write data before transport.
- Library: Label response parsing now validates item counts, echoed unit/length fields, bounded data, even random-data lengths, and trailing bytes, and reports malformed payloads as `SlmpError` while preserving unknown data-type and spare bytes.
- Library: `Dispose` and `DisposeAsync` are now terminal and idempotent; unlike `Close`, disposed clients reject reopening and all later requests with `ObjectDisposedException`.
- Library: Public collection inputs and required nested values now report stable argument exceptions before transport, and connection options use the same 1 ms through `int.MaxValue` ms timeout range as `SlmpClient`.
- Library: Request payloads now enforce the 16-bit SLMP data-length boundary before transport; IPv4 UDP additionally applies its smaller 3E/4E datagram limits, and oversized label aggregates fail before payload allocation.
- CI: Source archives now include the test project referenced by the solution, and CI/release gates restore, build, and test an extracted `git archive`.
- Tests: Added protocol vectors, malformed-response coverage, lifecycle race checks, timeout boundaries, null-input/no-I/O contracts, and TCP/UDP request-payload boundary tests.
- Library: Undefined `SlmpDeviceCode` values are rejected by semantic addresses and raw encoders instead of being truncated into another legacy device code.
- Library: Typed scalar APIs now require bit devices to use `BIT` and word devices to use a word/DWord dtype, preventing command/unit mismatches before transport.
- Library: Named target strings now preserve empty comma-separated fields and reject every empty, missing, or extra route field instead of silently shifting the route.
- Library: `SlmpClient` snapshots every collection argument when submitted, including nested block values and label data buffers, so later caller mutation cannot change the transmitted request.
- Library: Direct and extended bit reads now require the exact packed-byte count and reject any used nibble other than `0` or `1`.
- Tests: Added regressions for undefined device codes, typed device/dtype mismatch, strict target fields, FIFO deep snapshots, and exact packed-bit decoding.

### BREAKING

- Library: `WriteBitInWordAsync` now covers Direct and qualified U module-buffer / J link-direct complete-word routes, prevalidates the immutable route, owns one FIFO turn, and uses one absolute post-admission deadline for the mandatory read followed by write. The write is sent even when the bit is unchanged; the pair is not PLC-atomic, never retries, and a possibly transmitted unconfirmed write raises `SlmpOperationOutcomeUnknownException`.
- Library: Deadline expiration is now reported as `SlmpTimeoutException` instead of caller cancellation or a generic transport/protocol error. After a possibly transmitted state-changing request, timeout, cancellation, close, malformed response, or transport loss now throws `SlmpOperationOutcomeUnknownException`; callers must reconcile PLC state instead of automatically retrying.
- Library: Removed `QueuedSlmpClient`, its constructor, `InnerClient`, and all queued-specific extension overloads. `SlmpClientFactory.OpenAndConnectAsync` and `SlmpClient.OpenAndConnectAsync` now return the ordinary `SlmpClient`; callers replace the wrapper type with `SlmpClient` and call the same operations directly. No compatibility alias remains.
- Library: Callers using an IPv6 endpoint must migrate to an IPv4 literal or a hostname that resolves to IPv4.
- Library: Code that called `OpenAsync` or issued requests after disposing a client must retain the client and use `Close` when a reopenable session is required. Invalid label write buffers, null inputs, and oversized raw or label payloads that previously reached transport or leaked runtime exceptions now fail before I/O; oversized commands are not split automatically.
- Library: Undefined device codes, typed device/dtype unit mismatches, and malformed target-route strings that were previously accepted now fail before transport. Ordinary client calls observe collection values at submission time.

## [4.0.1] - 2026-07-29

- Release: Bumped .NET package metadata to `4.0.1`.
- Release: GitHub Release drafts now prepend this version's changelog section to generated notes and repair a missing section on workflow reruns.

### Fixed

- Library: J link-direct extended random read/write and monitor registration now use the Q/L subcommands, including the one-byte Q/L bit-value representation; requests that mix 11-byte J and 13-byte iQ-R entry layouts are rejected before transport.
- Library: Empty iQ-F octal X/Y addresses and link-direct addresses wider than the 24-bit wire field are rejected before transport instead of being accepted or truncated.
- Library: Decimal and hexadecimal device parsers reject signs and embedded whitespace, and J-direct network numbers use the stable decimal `0..255` contract.
- Library: Extended random-bit writes report null input as `ArgumentNullException`, and long-timer helpers apply device-family and selected wire-width validation before transport.
- Library: Profile device-range upper bounds are not used as transport send guards; protocol representation and command limits remain validated.
- Tests: Added exact subcommand, payload-vector, empty-address, and wire-width regression coverage.

## [4.0.0] - 2026-07-17

- Release: Bumped .NET package metadata to `4.0.0`.

- Library: Added immutable lifetime traffic counters through `SlmpClient.TrafficStats` and `QueuedSlmpClient.TrafficStats`.
- Library: TCP and UDP responses now require an exact network, station, module I/O, and multidrop match with the immutable request target. Structurally valid foreign-route responses are discarded within the original request deadline; malformed responses invalidate the transport.
- Tests: Added deterministic TCP/UDP and 3E/4E response-correlation coverage, including every route field, foreign-route-only timeout, wrong-serial flooding, matching responses within the deadline, and malformed responses.
- Library: Added the public `melsec:mx-r:rj71en71` profile (`SlmpPlcProfile.MxRRj71En71`) with canonical capability and device-range behavior.
- Library: Capability evidence sources and iQ-F point-limit end-code metadata now exactly match the pinned canonical profile data.
- Tooling: Refreshed canonical SLMP profile fixtures for 2026-07-14 and pinned imports to profile tag `v2.1.0`.

## [3.1.0] - 2026-07-13

- Library: `QueuedSlmpClient` now exposes self-test loopback and fixed Clear Error semantic APIs instead of requiring access through the inner client.
- Library: Monitor cycle expected counts must total at least one and stay within the selected profile limit; queued monitor registration snapshots device lists when submitted.
- Library: Self-test loopback now rejects declared-length, actual-length, trailing-data, and echo mismatches against the transmitted input snapshot.
- Docs: Clarified explicit monitor counts and that `U3En\HG` never changes or retries the immutable user-selected request target.
- Tests: Removed vendored cross-repository vector JSON and its dedicated runners. Cross-implementation comparison is executed independently of this library repository.
- Library: Long-timer and long-retentive-timer helpers require explicit heads and counts and reject negative heads, zero counts, one-request-limit overflow, and arithmetic overflow before transport.

### BREAKING
- Library: Removed `SlmpCpuModule` and all direct/queued `CpuBuffer*Async` aliases. Live R120PCPU cross-writes proved that Extend Unit `0x0601/0x1601` and qualified `U3E0\HG` access different physical areas. Use `ExtendUnit*Async` for Extend Unit commands and qualified Extended Device APIs for HG.
- Library: Connection constructors and options now require port, transport, complete target, and concrete PLC profile; timeout defaults to 3 seconds and the SLMP monitoring timer defaults to 4 seconds.
- Samples: The high-level sample uses the approved 3-second communication timeout instead of silently selecting 5 seconds.
- Library: The selected target route is immutable for the lifetime of a client; create a separate client for a different route.
- Library: Removed public profile-check bypasses, command-specific raw payload overloads, split/chunk helpers, block auto-splitting, end-code messages/language selection, and the profile override on device-range catalog reads.
- Library: Replaced public Extended Device wire fields with qualified semantic routes and typed Z/LZ/indirect modifiers.
- Library: Remote RUN and PAUSE now require typed mode choices, RUN requires a typed clear mode, and Remote RESET uses its fixed payload without waiting for a success response.
- Library: Long timer helpers require both head and point count.
- Library: Typed writes no longer use `Convert.*`; BIT requires `bool`, integer dtypes require integral CLR values in range, and F requires a finite numeric value representable as float32.
- Library: A timeout/cancellation/transport failure or send-only Remote RESET invalidates the session until the caller explicitly invokes `OpenAsync`; RESET also closes the transport so a residual 3E NG response cannot satisfy the next request.
- Library: Legacy device numbers above the 24-bit wire field, non-printable/non-ASCII passwords, timeout values below 1 ms, and LZ modifier indexes above 1 are rejected before transport.
- Library: `ReadNamedAsync`, `PollAsync`, and `WriteNamedAsync` now emit one random request per call/cycle or reject the complete operation before transport; hidden fallback reads, mixed write families, and bit-in-word read-modify-write are no longer performed.

### Added
- Library: Added `SlmpPlcProfileDescriptor` and `SlmpPlcProfiles.GetProfileDescriptors()` for canonical SLMP profile metadata.

### Changed
- Library: One client now serializes all request/response exchanges, assigns unique 4E serials, and closes TCP or UDP transport after timeout/cancellation so a delayed response cannot contaminate a later request.
- Library: Random and block operations expose category-specific APIs, reject all-empty requests, and reject duplicate or overlapping write destinations before transport.
- Library: Normal and Extended Device random reads and word writes now reject null category collections explicitly instead of dereferencing them; category-specific APIs omit the unused collection and return only the requested value type.
- Library: Block reads and writes now reject null word/bit block collections explicitly; category-specific APIs omit the unused collection, read results allocate the unused category as an empty array, and overlapping write ranges remain pre-transport errors.
- Library: Label abbreviation definitions remain optional and encode zero when omitted; malformed references, empty points, and count overflow are rejected before transport.
- Samples: Runtime connection fields, targets, polling addresses, and dtypes are explicit; configuration files no longer infer port or transport.

- Release: Bumped .NET package metadata to `3.1.0`.
- Tooling: Pinned canonical SLMP profile imports to published profile tag `v2.0.0`.

### Fixed
- Library: Long timer multi-point reads use one bounded request instead of issuing one request per point.
- Library: Direct connection options validate host, port, transport, profile, and timeout at construction.

- CI: Required an existing exact release tag checkout and verified tag, manifest, runtime assembly, `.nupkg`, and `.snupkg` versions before GitHub Release upload.
- Docs: Fixed XML `cref` labels in the generated API reference by excluding method parameter lists and added generator regression tests.
- Docs: Removed hand-maintained page navigation from `GETTING_STARTED.md`.

### Tests
- Tests: Added cross-contract tests for aggregate empty input, overlapping writes, Extended Device public surface, required state-changing arguments, CPU selection, label abbreviation validation, concurrent 4E serial assignment, and UDP cancellation isolation.

## [3.0.0] - 2026-07-10

### Changed
- Release: Bumped .NET package metadata to `3.0.0`.
- Packaging: Marked samples, CLI, and validation tools non-packable so only the library package is produced.
- Docs: Replaced relative README links with absolute URLs so they resolve on package registry pages.

### Added
- Library: Added `SlmpPlcProfiles.AvailableProfiles()` for connection-selectable profile enumeration, excluding `Unspecified` and the base-only `QCpu` profile.
- Docs: Documented the connection profile enumeration API.

## [2.0.0] - 2026-07-06

### BREAKING
- Library: Removed short `SlmpModuleIo` aliases in favor of the canonical module I/O vocabulary.

| Removed name | Use instead |
| --- | --- |
| `ControlCpu`, `ConnectedCpu`, `Default` | `OwnStation` |
| `ActiveCpu` | `ControlSystemCpu` |
| `StandbyCpu` | `StandbySystemCpu` |
| `TypeACpu` | `SystemACpu` |
| `TypeBCpu` | `SystemBCpu` |
| `Cpu1` to `Cpu4` | `MultipleCpu1` to `MultipleCpu4` |
| `SELF-CPU1` to `SELF-CPU4` | `SELF-MULTIPLE-CPU-1` to `SELF-MULTIPLE-CPU-4` |

### Changed
- Release: Bumped package metadata to `2.0.0`.
- Library: Added `SlmpModuleIo` named constants for multi-CPU target routing while keeping the default own-station target unchanged.
- Library: Synced the embedded SLMP capability fixture to `plc-comm-slmp-profiles` `v1.2.2`.
- Tooling: Moved .NET project version metadata to `Directory.Build.props` and added common `plc-comm` package tags.

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
- Tests: Updated high-level address parser tests for explicit dtype requirements.
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
