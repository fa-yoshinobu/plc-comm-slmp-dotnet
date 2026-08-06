# .NET SLMP quality-overhaul decision and acceptance record

This maintainer record maps the approved workspace decisions to the .NET implementation. Breaking changes are intentional where compatibility conflicts with an explicit, profile-safe, single-request contract.

## SLMP-BIT-RMW-20260807 — Complete-route bit-in-word contract

- Scope: Direct and qualified Extended Device complete-word routes exposed by `SlmpClient`.
- Target contract: `WriteBitInWordAsync` prevalidates the exact immutable route and both requests, owns one FIFO turn, and uses one absolute post-admission deadline. A successful read always produces one write even when unchanged. The pair is non-PLC-atomic, never retries, and an unconfirmed possibly transmitted write is outcome unknown.
- Compatibility: the overload accepting `SlmpQualifiedDeviceAddress` adds U module-buffer and J link-direct parity; compound timeout no longer restarts between requests.
- Acceptance: invalid routes send zero requests; Direct and qualified routes each send one read then one write with the same route.
- [x] Implementation and targeted route tests completed.
- [x] Full repository release gate completed.
- [x] User, generated API, changelog, and migration sources updated.

Release-gate evidence (2026-08-07): `release_check.bat` passed the three-TFM test,
static-analysis, generated-document, package-consumer, source-archive, and
registry-duplicate checks for candidate `5.0.0`.

## D-001 / D-002 / D-004 — Explicit endpoint and target

- Scope: constructors, connection options, factory helpers, CLI, JSON and executable samples.
- Target contract: host, port `1..65535`, TCP/UDP transport, concrete PLC profile and complete target are required. Missing runtime fields never become port 1025, TCP or own station.
- Compatibility: former constructor, CLI and config defaults no longer work.
- Acceptance criteria: invalid construction fails before I/O; every runnable sample requires all endpoint fields; partial routes are rejected.
- [x] Implementation completed.
- [x] Tests and dry-run validation updated.
- [x] User and migration documentation updated.

## D-003 / D-005 / D-009 — Stable timing defaults

- Scope: TCP/UDP transport and request headers.
- Target contract: communication timeout defaults to 3 seconds, monitoring timer to `0x0010` (4 seconds), and TCP keepalive idle to 30 seconds. Non-positive timeout is rejected.
- Compatibility: previous timing defaults change.
- Acceptance criteria: options, client, and maintained samples expose the approved 3-second communication default; golden frames contain `0x0010`; timeout validation happens before I/O.
- [x] Implementation completed.
- [x] Tests completed.
- [x] Documentation updated.

## D-006 / D-018 — Profile-derived behavior cannot be overridden

- Scope: frame, compatibility, capability guards, password representation and device-range catalog.
- Target contract: a concrete required profile derives all normal protocol choices; no public strict-profile bypass or request/catalog profile override remains.
- Compatibility: bypass and profile-override callers must migrate.
- Acceptance criteria: public-surface scan finds no bypass; profile mismatch is rejected before transport; catalog reads use the connection profile.
- [x] Implementation completed.
- [x] Tests completed.
- [x] Documentation updated.

## D-011 / D-012 / D-034 — One explicit raw-command escape hatch

- Scope: arbitrary command access.
- Target contract: `RawCommandAsync(command, subcommand, payload)` is the sole arbitrary-command surface and always expects a response; all three inputs are required, including explicit empty payload.
- Compatibility: public `RequestAsync`, response flags and command-specific raw-payload wrappers are removed.
- Acceptance criteria: reflection/generated-reference scan shows only the generic surface; semantic methods construct fixed payloads internally.
- [x] Implementation completed.
- [x] Tests completed.
- [x] Raw API excluded from ordinary user workflows and recorded here.

## D-013 / B-10 — Request ownership, serials and cancellation

- Scope: base client TCP/UDP exchanges.
- Target contract: one client permits one in-flight exchange, allocates 4E serials inside that lock, matches response serials, and closes transport after timeout/cancellation or transport failure. The invalidated session rejects requests until the caller explicitly invokes `OpenAsync`. Maintainer trace failures cannot affect communication.
- Compatibility: callers cannot rely on concurrent pipelining or reuse a cancelled session.
- Acceptance criteria: concurrent 4E calls have unique serials; mismatched serials are ignored; UDP timeout closes the socket, prevents delayed-response reuse, rejects implicit reopen, and permits use only after explicit `OpenAsync`.
- [x] Implementation completed.
- [x] Tests completed.
- [x] Documentation updated.

## D-019 / D-020 — Random categories and write uniqueness

- Scope: normal and Extended Device random read/write.
- Target contract: category-specific word/DWord methods omit the unused category; every-category-empty requests fail; duplicate or overlapping write destinations fail before transport.
- Compatibility: callers may stop passing placeholder empty lists.
- Acceptance criteria: specialized methods exist on base and queued clients; null read/write category collections, all-empty requests, invalid values, and overlapping writes fail before transport; unused read result categories are allocated as empty arrays.
- [x] Implementation completed.
- [x] Tests completed.
- [x] Generated API reference updated.

## D-021 / D-022 / D-023 / D-037 — Block request integrity

- Scope: word/bit block read/write.
- Target contract: category-specific methods omit unused lists; mixed blocks remain one request; null or all-empty collections, malformed/wrong-unit blocks, and overlapping write ranges fail before transport; unused read result categories are empty arrays; no split flag exists.
- Compatibility: automatic split callers must issue separate requests and handle timing/partial success themselves.
- Acceptance criteria: one mixed call creates one frame; empty and overlap tests send zero frames; public surface has no split option.
- [x] Implementation completed.
- [x] Tests completed.
- [x] Documentation updated.

## D-024 / D-025 / D-026 — Explicit remote state changes

- Scope: Remote RUN and PAUSE.
- Target contract: required `SlmpRemoteMode` selects normal/force; RUN also requires `SlmpRemoteClearMode`; undefined enum values fail before transport.
- Compatibility: default Boolean and numeric-mode calls no longer compile.
- Acceptance criteria: required-parameter reflection tests, invalid-enum pre-transport tests, and frame vectors cover Normal/Force plus all three clear modes.
- [x] Implementation completed.
- [x] Tests completed.
- [x] Generated API reference updated.

## D-027 / D-028 — Fixed Remote RESET

- Scope: Remote RESET.
- Target contract: the API exposes no subcommand or response option; it sends command `0x1006`, subcommand `0x0000`, payload `0x0001`, closes the transport generation, and completes after send without treating absent success response as timeout. A new request requires explicit `OpenAsync`.
- Compatibility: configurable reset callers must migrate.
- Acceptance criteria: the shared frame vector is captured without requiring a response.
- [x] Implementation completed.
- [x] Tests completed.
- [x] Documentation updated.

## D-029 — Profile-derived remote password payload

- Scope: remote password lock/unlock.
- Target contract: callers provide a non-empty printable-ASCII profile-valid password only; payload form comes from the connection profile and no series argument exists. Encoding never replaces non-ASCII input with `?`.
- Compatibility: series override calls are removed.
- Acceptance criteria: fixed/variable length validation occurs before transport and profile vectors remain deterministic.
- [x] Implementation completed.
- [x] Tests completed.
- [x] Documentation updated.

## CLAUDE-SLMP-20260712-01 accepted .NET findings

- Typed write values use strict CLR type and range validation before request creation.
- Legacy direct device numbers fit the 24-bit field without truncation.
- Send-only RESET and failed exchanges invalidate transport ownership and require explicit reopen.
- Passwords are printable ASCII, timeout is at least 1 ms, and LZ index is 0 or 1.
- These contracts are covered by pre-transport tests and TCP/UDP local transport tests; no live PLC result is required.

## D-030 — Optional label abbreviations with validation

- Scope: array/random label read/write.
- Target contract: omission encodes zero abbreviations; explicit definitions are ordered `%1`, `%2`, and so on; empty/malformed/out-of-range references, empty points and count overflow fail before transport.
- Compatibility: malformed labels formerly encoded are rejected.
- Acceptance criteria: zero, multiple, malformed, empty and overflow tests pass.
- [x] Implementation completed.
- [x] Tests completed.
- [x] Documentation updated.

## D-031 / D-032 / D-033 — Explicit long-timer selection

- Scope: long timer/retentive timer helpers.
- Target contract: all long-family multi-point and state projection helpers require head and point count. Negative heads, zero counts, counts above 240 timers, and arithmetic-overflow counts fail before transport.
- Compatibility: implicit head zero and one-point defaults are removed.
- Acceptance criteria: parameters are non-optional and multi-point timer read uses one bounded request.
- [x] Implementation completed.
- [x] Tests completed.
- [x] Documentation updated.

## D-035 / D-036 — No hidden multi-request contiguous access

- Scope: continuous word/DWord reads and writes and high-level named batching.
- Target contract: one public read/write call emits at most one request and rejects counts or named routes that cannot fit the selected command; no chunked helper, split option, or partially successful named write is public.
- Compatibility: chunk/split callers must implement their own request and consistency policy.
- Acceptance criteria: public-surface scan has no chunk helpers; limit and incompatible named-route tests send zero requests; named reads and writes have a one-random-request-or-reject contract.
- [x] Implementation completed.
- [x] Tests completed.
- [x] Documentation updated.

## D-038 — No localized manual-derived end-code text

- Scope: end-code helpers and `SlmpError`.
- Target contract: numeric end code, stable derived key, command/subcommand and structured information remain; message lookup, language enum and message property are absent.
- Compatibility: message-property callers use the code/key or shared independent site descriptions.
- Acceptance criteria: exported-type/reference scans find no message/language API; known and unknown code keys remain deterministic.
- [x] Implementation completed.
- [x] Tests completed.
- [x] Documentation updated.

## D-039 / D-040 / D-042 / D-047 — .NET cancellation and semantic addresses

- Scope: async APIs, address utilities and Extended Device routes.
- Target contract: cancellation tokens remain optional .NET controls; every semantic address is bound to a concrete profile; Extended Device fields derive from qualified routes; only typed Z/LZ/indirect modification is public. Raw wire fields remain internal.
- Compatibility: profile-free address and public `SlmpExtensionSpec` calls are removed.
- Acceptance criteria: iQ-F/iQ-R radix fixtures pass; mismatched addresses fail before transport; exported surface contains no raw extension record/direct-memory property.
- [x] Implementation completed.
- [x] Tests completed.
- [x] Documentation updated.

## Batch acceptance checklist

- [x] Implementation completed in this affected repository.
- [x] Tests added or updated for each implemented acceptance criterion.
- [x] Relevant static checks, unit/integration tests, examples and package/build checks passed.
- [x] Codex self-review completed against the approved contract and cross-language consistency requirements.
- [x] Claude source review completed and findings recorded through the two user-authorized SLMP review batches.
- [x] Codex resolved or dispositioned every Claude finding and reran affected checks.
- [x] Required live-PLC checks passed, or each unavailable check has an explicit release disposition.
- [x] Documentation, migration notes, changelog and generated API reference agree with implementation.
- [x] Final acceptance criteria verified and item marked complete.

## Live-PLC disposition

These changes are API shape, pre-transport validation, deterministic frame generation, mock transport state or documentation behavior. No new physical PLC claim is made and no live PLC communication was authorized or performed. Existing hardware/profile compatibility evidence remains unchanged. Any later profile-specific live check remains `unverified` until separately proposed with target, endpoint, address and intent and explicitly authorized.

## Claude review package status

The approved decisions, repository diff, and final local results were reviewed in the two user-authorized SLMP Claude batches. Canonical results and dispositions are preserved in the archived workspace instruction records.

## Verification evidence

- `dotnet format PlcComm.Slmp.sln --no-restore --verify-no-changes`: PASS.
- `dotnet test PlcComm.Slmp.sln --configuration Release --no-restore`: PASS on `net8.0`, `net9.0`, and `net10.0`; 289 tests per target framework, zero failed or skipped.
- `python scripts/test_generate_api_reference.py`: PASS, 4 tests.
- generated API reference regeneration plus `--check`: PASS; 50 documented public types and maintainer-only raw command omitted through `EditorBrowsable(Never)`.
- `dotnet pack ... --configuration Release --no-build`: PASS; `.nupkg` and `.snupkg` created locally for packaging validation only.
- Multi-PLC and JSON polling `--dry-run`: PASS with explicit port, transport, target, profile and dtype; no PLC communication.
- CLI `--help`: PASS and documents mandatory endpoint, route and operation fields.
- public-surface/stale-name scan: PASS; no user-facing extension wire record, split/chunk option, localized end-code message/language, strict-profile bypass, raw request or thread-safety warning remains.
- `git diff --check`: PASS; line-ending conversion warnings only.

Codex self-review inspected the actual diff, exported API, constructor and validation order, profile/address binding, immutable target, write overlap rules, request locking, 4E matching, TCP/UDP timeout and cancellation invalidation, fixed Remote RESET, label input validation, tests, samples, generated documentation and packaging. It found and corrected two issues during review: missing profile checks on semantic Extended Device paths and mutable target routing after construction.

## 2026-07-12 D-128, D-129, D-131, and D-132 delta

### D-128 — Monitor expected-count contract

- Scope: direct and `QueuedSlmpClient` monitor registration/cycle APIs.
- Target: registration and every cycle remain one request; cycle counts are explicit, nonzero, and within the active profile's monitor-registration limit, with no implicit registration, retry, split, or fallback.
- Compatibility: zero/over-limit expected counts now fail before transport instead of accepting an impossible empty/oversized result contract.
- Acceptance: exact registration/cycle commands, three cycles, zero/over-limit rejection, PLC NG, response-size mismatch, request counts, and queued normal/qualified device-list snapshots are covered on net8.0, net9.0, and net10.0.

### D-129 — Preferred-client self-test parity

- Scope: direct and `QueuedSlmpClient` self-test APIs.
- Target: the queued wrapper exposes the same method; both require 1–960 ASCII `0-9/A-F` and compare declared length, actual length, and echo against the bytes snapshotted for transmission.
- Compatibility: trailing, short, wrong-length, and mismatched echoes now fail.
- Acceptance: direct malformed-response cases, direct in-flight caller mutation, queued pre-execution caller mutation, and queued exact-frame forwarding are covered on net8.0, net9.0, and net10.0.

### D-131 — Preferred-client Clear Error parity

- Scope: `QueuedSlmpClient.ClearErrorAsync` and the direct fixed command.
- Target: one `0x1617/0x0000` empty-payload request under the queue gate, with normal cancellation/transport/error behavior.
- Compatibility: callers no longer need `InnerClient` for this semantic command.
- Acceptance: exact queued request shape and one-request boundary are covered.

### D-132 — HG target ownership

- Scope: qualified Extended Device HG operations, Extend Unit operations, public aliases, and immutable target behavior.
- Target: `0x0601/0x1601` remain available only as `ExtendUnit*Async`; HG remains available only through qualified Extended Device APIs. Do not infer a target from `U3En`; do not reject cross-CPU reads, retry another CPU, or read back automatically.
- Compatibility: `SlmpCpuModule` and all direct/queued `CpuBuffer*Async` aliases are removed. Migrate those calls to `ExtendUnit*Async`; do not mechanically translate them to an HG address because live evidence proves the physical areas differ. Create a client with the explicit CPU target when an HG write must be reflected there.
- Acceptance: exported-surface tests reject the removed type and methods, Extend Unit and qualified HG exact-frame tests remain, `U3E1\HG` retains Own Station `0x03FF`, and only an explicitly CPU No.2 client emits `0x03E1`.

- [x] Local implementation and regression tests completed.
- [x] Build, 289 tests per target framework, formatting, generated API validation, NuGet packing, and release check passed.
- [x] User API, migration, changelog, generated API, and shared target guidance updated.
- [x] Claude review of this delta completed through `CLAUDE-SLMP-20260712-02`; all findings were dispositioned and affected checks rerun.
- [x] New public-API verification completed through deterministic regression coverage and the approved D-128/D-129/D-131 live checks.
- [x] D-132 Extend Unit versus HG physical-area classification completed: independent values remained stable through immediate, 50 ms, 250 ms, and 1 s cross-reads.
- [x] Removed the misleading CPU-buffer aliases and alias-only enum; retained distinct Extend Unit and qualified HG surfaces.

## NR-006: Lifetime traffic statistics

Scope: `SlmpClient.TrafficStats` and `QueuedSlmpClient.TrafficStats`, next release.

Target contract: the property returns a client-lifetime immutable snapshot. A request and its full
frame bytes count only after a complete transport send succeeds. A complete received frame/datagram
TCP response counts after assembly in the selected frame format; a UDP datagram counts on receipt.
Both count before serial, end-code, or payload validation. Unrecognized TCP subheaders, partial
sends/receives, and pre-send failures do not count. Close/reconnect does not reset counters.

Acceptance criteria:

- [x] Implementation and deterministic boundary tests completed.
- [x] API reference, usage guide, and Unreleased changelog agree.
- [x] Live PLC verification is unnecessary because deterministic transports observe every boundary.
- [x] Final next-release package and cross-language API comparison completed. Evidence: the `v4.0.0`
  tag equals repository HEAD, the GitHub Release and NuGet `PlcComm.Slmp` `4.0.0` package are public,
  tag-commit checks passed, and the final five-implementation source/API comparison was completed
  on 2026-07-18.

## QREV-20260714-002: Response target-route correlation

Implementation scope: `SlmpClient` TCP and UDP receive paths for 3E and 4E responses.

Target contract: after complete-frame structural validation, a response is eligible for the active
request only when its network, station, module I/O, and multidrop fields exactly match the immutable
request target. A structurally valid foreign-route response is discarded while the same linked
request deadline remains active. A malformed response is a protocol error and invalidates the
transport generation.

Compatibility impact: a gateway or peer that returns route fields different from the requested
target no longer has its payload or PLC end code accepted; the request waits for a matching response
and otherwise times out at its original deadline.

Acceptance criteria:

1. TCP and UDP, in both 3E and 4E, discard a response that differs in each individual route field and accept a subsequent exact match.
2. A continuous foreign-route response stream cannot extend the request deadline.
3. Recognized but structurally malformed responses raise `SlmpError`, close the transport, and require an explicit `OpenAsync` before reuse.
4. Received-frame statistics and trace boundaries remain before correlation filtering.

- [x] Implementation completed in this repository.
- [x] Tests added for every acceptance criterion on net8.0, net9.0, and net10.0.
- [x] Full build, static checks, 344 tests per target framework, NuGet package checks, and generated-document checks passed.
- [x] Codex source self-review completed against the target contract and cross-language field mapping.
- [x] Claude source review completed in the user-authorized 2026-07-14 batch; findings are preserved in the archived workspace record `claude_review_findings_20260714.md`.
- [x] Codex dispositioned every applicable Claude finding and reran affected checks; details are recorded below.
- [x] Live-PLC verification is not required because every correlation and invalidation boundary is deterministically observable with local TCP/UDP peers.
- [x] Changelog and maintainer contract agree with the implementation; no public API reference changed.
- [x] Final acceptance verified and the item marked complete after family-wide comparison.

## QREV-20260714-003: One absolute 4E response-correlation deadline

Implementation scope: `SlmpClient` TCP and UDP 4E receive loops.

Target contract: the linked cancellation source created once for an exchange remains the only
communication deadline while wrong-serial and foreign-route responses are discarded. No discarded
response may restart, replace, or extend that deadline.

Compatibility impact: none; this records and regression-locks the existing absolute-deadline
behavior while extending it to route correlation.

Acceptance criteria:

1. Continuous wrong-serial responses cannot extend the configured TCP or UDP timeout.
2. A matching serial and route received before the deadline completes normally.
3. Route filtering uses the same linked cancellation source and has the same deadline behavior.

- [x] Implementation behavior verified in this repository.
- [x] Deterministic TCP and UDP deadline regression tests added on all target frameworks.
- [x] Full build, static checks, 344 tests per target framework, NuGet package checks, and generated-document checks passed.
- [x] Codex source self-review confirmed one linked cancellation source per exchange.
- [x] Claude source review completed in the user-authorized 2026-07-14 batch; findings are preserved in the archived workspace record `claude_review_findings_20260714.md`.
- [x] Codex dispositioned every applicable Claude finding and reran affected checks; details are recorded below.
- [x] Live-PLC verification is not required because the deadline is a local transport state-machine contract.
- [x] Changelog and maintainer contract agree; no public API or migration action changed.
- [x] Final acceptance verified and the item marked complete after family-wide comparison.

### 2026-07-14 Claude finding disposition and re-verification

| Finding | Disposition and evidence |
|---|---|
| F-X1 | Accepted. The default profile import ref is `v2.1.0`; the root-only drift check downloaded that tag and reported both fixtures unchanged. |
| F-X2 | Accepted. `PROFILES.md` lists `melsec:mx-r:rj71en71`. |
| F-X5 / D-10 | Accepted. The changelog classifies the public profile as a `Library` addition. |
| D-1 | Accepted. Device-range catalogs use `MX-R via RJ71EN71`, locked by a direct catalog test. |
| D-2 | Duplicate of F-X2 and resolved by the profile table row. |
| D-3 | Accepted. The generated API reference contains `MxRRj71En71`; Debug and Release drift checks passed. |
| D-4 | Accepted. TCP tests cover successful split assembly and a header/body sequence whose individual waits are below 100 ms but cumulative delay exceeds the single deadline. |
| D-5 | Accepted. The 120 ms flood regressions require at least 105 ms elapsed. |
| D-6 | Accepted. The foreign response carries `0xAA`; only the matching response's `0xBB` is returned. |
| D-7 | Accepted. 3E and 4E UDP tests prove timeout closure, rejection before explicit reopen, and a clean successful exchange after `OpenAsync`. |
| D-8 | Accepted. Direct tests cover canonical ID parsing, client construction/defaults, and device-range catalog identity/label. |
| D-9 | Accepted. Parity now compares feature sources plus limit source and over-end-code. It exposed and corrected older iQ-R Ethernet-unit/MX-F source drift and swapped iQ-F direct word/bit over-end-codes. |
| D-11 | Accepted. The MX-R/RJ71EN71 one-off range special case was removed; the general range/address-profile fallback supplies the MX-R rules and the catalog wrapper preserves unit identity. |
| D-12 | Accepted. Short recognized UDP datagrams are reported as malformed and invalidate the transport. |
| D-13 | Superseded by `GOAL-SERIAL-DEFER-002` below. Explicit `OpenAsync` retains a connection deadline, while a lazy connection inside a request is included in that request's one absolute transaction deadline. |
| D-14 | Accepted as an inherent untagged-3E limitation. No automatic write retry or target switching was introduced; 4E remains the correlated choice where delayed-duplicate discrimination is required. |

Additional Codex self-review added explicit cancellation checks around each discard iteration and rejects non-zero 4E reserved response bytes as malformed on TCP and UDP.

Post-disposition evidence:

- `scripts/update_slmp_profile_jsons.ps1 -FailIfChanged`: both fixtures unchanged at `v2.1.0`.
- `run_ci.bat`: Debug build, release-version/generator checks, API drift, format, and 344 tests on each of net8.0, net9.0, and net10.0 passed.
- Release build and 344 tests on each target framework passed with zero warnings/errors; Release API drift passed.
- Ten focused deadline/split/reopen cases passed in five consecutive net8.0 runs.
- NuGet and symbol packages built successfully, package contents and version `3.1.0` passed integrity checks.
- `scripts/check_no_auto_publish.ps1` and `git diff --check`: passed.
- No live PLC communication was required or performed; every changed boundary is deterministic in local TCP/UDP tests.

## BH-LIVE-SLMP-20260729 — Supplemental bug-hunt live verification

Scope: commit `cb1670d3726682041177e3327877bdd5fbf33c06`, profile `melsec:iq-r`, TCP
`192.168.250.100:1025`.

Target contract: the library sends profile-catalog range exceedances that fit the wire format, uses
the Q/L layout for J link-direct extended random and monitor operations, and leaves every test
device in its documented final state.

Acceptance evidence:

- [x] `D100` one-word read succeeded with value `0`.
- [x] `R32768` reached the PLC and surfaced `SlmpError` end code `0x4031` for command `0x0401`,
  subcommand `0x0002`; no pre-send profile-range rejection occurred.
- [x] Extended random read of `J1\W10` succeeded with value `0`.
- [x] Extended random word write changed `J1\W10` from `0` to `0x6B2D`, read back `0x6B2D`,
  restored `0`, and confirmed the restoration.
- [x] Extended random bit write changed `J1\B10` to ON, read ON, reset it to OFF, and confirmed OFF.
- [x] Extended monitor registration for `J1\W10` and one monitor cycle succeeded with value `0`;
  the TCP session was then closed.
- [x] The repository working tree was clean after the live probes.

Disposition: all supplemental live checks passed. The `R32768` result is PLC-side address evidence,
not authority to add a communication-library profile-range guard.

## REM-20260731-001 — Array-label wire length

Implementation scope: array-label request builders, response parser, public label models, and tests.

Target contract: unit 0 is a logical bit count encoded in two-byte words
(`ceil(length / 16) * 2` bytes); unit 1 is a logical byte count padded to an even
wire length (`ceil(length / 2) * 2` bytes). Unit must be 0 or 1 and logical length
must be positive. A response must echo each requested unit and logical length.

Compatibility impact: incorrectly sized array writes and mismatched response metadata that were
formerly accepted now fail before I/O or as `SlmpError`.

Acceptance criteria: official six-bit and boundary vectors decode correctly; request and response
vectors cover 1/6/16/17/32 bits and 1/2/3/4 bytes; incorrect units, lengths, and data sizes fail
deterministically.

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Relevant static, unit, integration, documentation, and package checks passed.
- [x] Codex self-review completed against the approved contract and actual diff.
- [x] Live PLC is not required for deterministic wire arithmetic; no hardware claim is added.
- [x] User documentation, migration notes, changelog, and generated API reference agree.
- [x] Final acceptance criteria verified and the item marked complete.

## REM-20260731-002 — Random-label write and response integrity

Implementation scope: random-label write builder, random-label read parser, models, and tests.

Target contract: every random-label write contains a non-null, positive, even number of wire bytes.
Every random-label read response has the requested item count, bounded positive even data lengths,
and no trailing bytes. Unknown data-type and spare values are preserved without reinterpretation.

Compatibility impact: null, empty, odd write buffers and malformed responses formerly accepted or
reported through runtime exceptions now fail with argument errors or `SlmpError`.

Acceptance criteria: exact write vectors, invalid-input cases, unknown metadata, truncation, count,
odd/zero length, and trailing-data cases are regression tested.

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Relevant static, unit, integration, documentation, and package checks passed.
- [x] Codex self-review completed against the approved contract and actual diff.
- [x] Live PLC is not required for deterministic builder/parser boundaries; no hardware claim is added.
- [x] User documentation, migration notes, changelog, and generated API reference agree.
- [x] Final acceptance criteria verified and the item marked complete.

## REM-20260731-003 — Malformed label response errors

Implementation scope: array/random label response parsing and public read methods.

Target contract: bounded reads validate the complete payload and convert all label-payload structural
errors to `SlmpError`; `IndexOutOfRangeException`, `ArgumentOutOfRangeException`, and other internal
parser exceptions do not escape. Structurally valid unknown metadata remains data, not an error.

Compatibility impact: malformed peer payloads now have one stable library error boundary.

Acceptance criteria: empty, short, truncated, inconsistent, and trailing payloads are covered for
both label read commands and produce `SlmpError`.

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Relevant static, unit, integration, documentation, and package checks passed.
- [x] Codex self-review completed against the approved contract and actual diff.
- [x] Live PLC is not required because malformed payload injection is deterministic locally.
- [x] User documentation, migration notes, changelog, and generated API reference agree.
- [x] Final acceptance criteria verified and the item marked complete.

## REM-20260731-004 — Reproducible source archive

Implementation scope: `.gitattributes`, CI, release workflow, solution, and exported test project.

Target contract: the source archive contains every project referenced by `PlcComm.Slmp.sln`, while
maintainer-only and generated release files remain excluded. CI and the release gate restore, build,
and test only files extracted from `git archive`.

Compatibility impact: future source archives include `tests`; runtime and NuGet contents do not change.
Published tags and archives remain immutable.

Acceptance criteria: archive inventory contains the test project and fixtures, and the extracted
archive restores, builds, and tests all target frameworks successfully.

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Relevant static, unit, integration, documentation, and package checks passed.
- [x] Codex self-review completed against the approved contract and actual diff.
- [x] Live PLC is not relevant to source-archive reproducibility.
- [x] Maintainer documentation, changelog, and workflow behavior agree.
- [x] Final acceptance criteria verified and the item marked complete.

## REM-20260731-005 — Terminal disposal

Implementation scope: `SlmpClient` open/request/dispose state transitions and queued propagation.

Target contract: `Close` remains reopenable. `Dispose` and `DisposeAsync` are idempotent and terminal;
later open/read/write operations throw `ObjectDisposedException`. Disposal interrupts an active
transport without waiting on the request gate or disposing semaphores that still have waiters.

Compatibility impact: callers that reused a disposed client must use `Close` or construct a new client.

Acceptance criteria: close/reopen, double synchronous and asynchronous disposal, post-dispose
open/read/write, queued propagation, waiting requests, and active-request disposal are deterministic
and deadlock-free.

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion implemented locally.
- [x] Relevant static, unit, integration, documentation, and package checks passed.
- [x] Codex self-review completed against state, cancellation, timeout, and gate behavior.
- [x] Live PLC is not required because lifecycle behavior is covered with local transports.
- [x] User documentation, migration notes, changelog, and generated API reference agree.
- [x] Final acceptance criteria verified and the item marked complete.

## REM-20260731-006 — Public null contract

Implementation scope: public collection-consuming client APIs, queued pre-snapshot paths, parsers,
label/block nested values, named address/update collections, and tests.

Target contract: a null public argument produces `ArgumentNullException` with the corresponding
parameter name. A null collection element or required nested model value produces an argument error
that identifies the owning public argument. All such validation occurs before transport.

Compatibility impact: null inputs no longer leak `NullReferenceException` or begin an exchange.

Acceptance criteria: null collections, elements, nested label/block values, parser inputs, and queued
snapshot inputs are mapped to stable argument errors; traffic counters and open state prove no I/O.

- [x] Implementation completed in this repository.
- [x] Tests added or updated for the affected public entry points and nested values.
- [x] Relevant static, unit, integration, documentation, and package checks passed.
- [x] Codex self-review completed against the public reference-input inventory.
- [x] Live PLC is not required because every invalid input is rejected before transport.
- [x] User documentation, migration notes, changelog, and generated API reference agree.
- [x] Final acceptance criteria verified and the item marked complete.

## REM-20260731-007 — One timeout range

Implementation scope: `SlmpClient.Timeout`, `SlmpConnectionOptions.Timeout`, and factory validation.

Target contract: every entry point accepts 1 millisecond through `int.MaxValue` milliseconds inclusive
and rejects all smaller, non-positive, and larger values through one shared validator.

Compatibility impact: positive sub-millisecond option values formerly accepted are now rejected.

Acceptance criteria: zero, negative, one tick, 1 millisecond, maximum, and above-maximum boundaries
are tested without transport.

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Relevant static, unit, integration, documentation, and package checks passed.
- [x] Codex self-review completed against every timeout assignment path.
- [x] Live PLC is not required for local timer-domain validation.
- [x] User documentation, migration notes, changelog, and generated API reference agree.
- [x] Final acceptance criteria verified and the item marked complete.

## REM-20260731-008 — Concurrency documentation disposition

Implementation scope: `SlmpClient`, `QueuedSlmpClient`, XML comments, user guide, and generated API text.

Target contract: base-client request serialization and queued multi-step helper serialization remain
distinct and accurately documented.

Compatibility impact: none.

Acceptance criteria: the actual request gate, queued gate, XML comments, and user guidance describe
the same two levels of serialization.

Disposition: rejected as a duplicate/stale report finding. The current source already documents the
distinction and no runtime or documentation change is required for this item.

- [x] Implementation inspection completed; no defect was present.
- [x] Existing concurrency tests cover the two gate levels.
- [x] Relevant static, unit, integration, documentation, and package checks passed.
- [x] Codex self-review completed against implementation and generated docs.
- [x] Live PLC is not required for semaphore ownership and scheduling behavior.
- [x] Documentation and generated API reference verified unchanged in meaning.
- [x] Final disposition verified and the item marked complete.

## REM-20260731-009 — Request payload length boundary

Implementation scope: the common 3E/4E request path, Array Label Read/Write, Label Read/Write Random,
raw commands, validation utilities, public documentation, and tests.

Target contract: the request data-length field contains the six bytes for monitoring timer, command,
and subcommand plus the command payload. Therefore every request payload is limited to
`ushort.MaxValue - 6`, or 65,529 bytes. Label builders calculate their complete aggregate length
with bounded arithmetic and reject an oversized request before allocating its payload. The common
request entry rejects an oversized payload before taking the request gate or opening transport, and
frame construction repeats the same guard before allocation and serial mutation.

Self-review amendment: the 65,529-byte protocol limit is reachable only over TCP. This client uses
IPv4 UDP, whose maximum datagram is 65,507 bytes including the complete SLMP frame. The effective
UDP command-payload limits are therefore 65,492 bytes for 3E and 65,488 bytes for 4E. The approved
contract is reopened and corrected to apply the smaller transport/frame-specific limit before open.

Compatibility impact: oversized inputs that formerly allocated a payload, opened transport, or
leaked `OverflowException` now throw `ArgumentOutOfRangeException` with the actual and maximum
payload lengths. The library does not implicitly split a command into multiple frames.

Acceptance criteria:

1. A 65,529-byte raw TCP payload produces request data length 65,535 for both 3E and 4E.
2. TCP payload 65,530 and UDP payloads above 65,492 for 3E or 65,488 for 4E fail before transport,
   traffic statistics, trace, frame allocation,
   or 4E serial mutation.
3. UDP payloads at the effective limit produce a 65,507-byte datagram for both frame types.
4. All four label builders accept their largest protocol-representable even payload size, 65,528 bytes, and
   reject 65,530-byte aggregate shapes before payload allocation.
5. Oversized aggregate shapes cover point labels, abbreviation labels, multiple points, and write
   data; Random Label Write also explicitly rejects data that cannot fit its 16-bit item length.
6. No `OverflowException`, `OutOfMemoryException`, or array-length exception escapes for tested
   oversized shapes.

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Relevant static, unit, local transport, documentation, archive, and package checks passed.
- [x] Codex self-review completed and accepted findings corrected.
- [x] Live PLC is not required because the 16-bit frame boundary and pre-transport behavior are deterministic locally.
- [x] User documentation, migration notes, changelog, and generated API reference agree.
- [x] Final acceptance criteria verified and the item marked complete.

Verification evidence:

- `run_ci.bat` passed build, generator tests, generated API freshness, format, and 408 tests on
  each of net8.0, net9.0, and net10.0 with zero failures or skips.
- TCP 3E/4E accepted 65,529-byte command payloads and encoded data length 65,535. IPv4 UDP 3E/4E
  accepted 65,492/65,488-byte command payloads as complete 65,507-byte datagrams.
- Each exact upper-bound-plus-one case failed before open, trace, traffic statistics, request frame,
  or 4E serial consumption. All four label builders passed their 65,528-byte boundary and rejected
  aggregate oversize from labels, abbreviations, multiple points, and write data.
- Canonical profile drift, no-auto-publish, and `git diff --check` passed.
- A virtual Git tree containing the uncommitted delta produced a source archive whose extracted
  solution restored, built with zero warnings/errors, and passed 408 tests per target framework.
- Release-mode NuGet and symbol packages built successfully and contained no tests or fixtures.

Self-review disposition:

- Accepted and corrected: the report treated 65,529 bytes as reachable on every transport. The
  client uses IPv4 UDP, so 3E and 4E now enforce their smaller 65,492- and 65,488-byte limits.
- Accepted and corrected: Random Label Write lengths above the 16-bit item field now report that
  range error before the positive/even shape check, including an oversized odd length.
- Rejected with rationale: applying the UDP limit inside transport-independent label builders would
  make identical payload construction depend on unavailable client state. Builders enforce the SLMP
  protocol limit before allocation; the client enforces the smaller transport/frame limit before
  its gate and before open.
- No duplicate or deferred finding changes this contract.

The approved sources for these records are `D:\APP\REMEDIATION_REPORT.md` and
`D:\APP\REMEDIATION_REPORT2.md`, including their 2026-07-31 correction addenda. Published
`v4.0.1` artifacts remain immutable; these changes target the next release.

### 2026-07-31 Codex self-review findings

| Finding | Classification and disposition |
|---|---|
| SR-REM-001 | Accepted and fixed. `DisposeAsync` used only `inheritdoc`, leaving the generated reference without the terminal-disposal contract. Explicit XML summary and remarks were added and the API reference regenerated. |
| SR-REM-002 | Accepted and fixed. The API generator rendered an `int.MaxValue` cref as only `MaxValue`; timeout XML now uses an exact code literal. |
| SR-REM-003 | Accepted and fixed. The initial null audit did not cover string parser inputs, named collections, nested updates, or queued monitor snapshots. Guards and no-I/O tests now cover those paths. |
| SR-REM-004 | Duplicate. The report's concurrency-documentation finding is stale; source, XML, and user guidance already distinguish request serialization from the queued multi-step gate. No change was made. |
| SR-REM-005 | Accepted and resolved under workspace decision `GOAL-SLMP-LABEL-001`. The original comparison was incomplete because Node-RED was also affected. Node-RED, Python, Rust, and C++ were corrected and independently verified on the same dedicated overhaul branch; no publication was authorized or performed. |
| SR-REM-006 | Accepted and fixed. The second report found that aggregate label payloads could exceed the 16-bit request data-length field and leak `OverflowException` after transport open. Common and builder-specific guards now reject them deterministically before I/O. |
| SR-REM-007 | Accepted and fixed during review of SR-REM-006. The report's 65,529-byte boundary omitted IPv4 UDP datagram limits; transport/frame-specific guards and exact loopback vectors now cover them. |
| SR-REM-008 | Rejected with rationale. Transport-independent label builders must not depend on TCP/UDP or 3E/4E client state; they enforce the protocol maximum, while the client applies the smaller effective limit before opening transport. |

The self-review inspected the actual diff, public API and generated reference, validation order,
argument errors, request and open gates, close/dispose transitions, active and waiting request behavior,
timeout/cancellation interaction, label response bounds, unknown metadata preservation, tests, workflows,
source-archive contents, changelog, migration impact, and NuGet package construction.

Final local evidence for this delta:

- `dotnet build PlcComm.Slmp.sln -c Release --no-restore`: PASS with zero warnings and errors.
- `dotnet test PlcComm.Slmp.sln -c Release --no-build`: PASS, 408 tests on each of net8.0, net9.0, and net10.0; zero failed or skipped.
- Exported worktree archive: test project and fixtures present; extracted-only restore/build/test PASS with 408 tests per target framework.
- `dotnet format ... --verify-no-changes`, profile JSON drift, no-auto-publish, API-generator unit tests, generated API freshness, and `git diff --check`: PASS.
- Release-mode NuGet and symbol packages were built locally; version/metadata checks passed and the package contained no tests or fixtures. The temporary packages were deleted and no registry publication was attempted.
- No live PLC communication was requested, authorized, or performed. Parser arithmetic, malformed payloads, request-size limits, lifecycle, null, timeout, and archive behavior are locally deterministic; no new PLC/profile compatibility claim is made.

## GOAL-SERIAL-DEFER-006: ordinary-client FIFO admission and queued-wrapper removal

This approved target supersedes every earlier record in this file that required API parity with,
or continued availability of, `QueuedSlmpClient`. Those earlier sections remain only as historical
decision evidence and no longer define the supported public surface.

Target state: `SlmpClient` is the one public live client. Every admitted operation uses one
arrival-order FIFO queue per client instance, snapshots request inputs before waiting, and owns no
more than one complete wire transaction. Waiting cancellation sends nothing. Queue waiting does
not consume the transaction timeout. `Close` and disposal retire the active transport generation
and reject its active and queued operations. Separate clients have independent queues.

Compatibility impact: `QueuedSlmpClient`, `InnerClient`, its constructor, and every queued-specific
extension overload are removed without aliases. Both `OpenAndConnectAsync` factories now return
`SlmpClient`. Migration is a type replacement: keep the returned ordinary client and invoke the
same methods directly.

Acceptance criteria:

1. Concurrent ordinary-client operations are admitted and sent in arrival order with one active wire transaction.
2. Cancellation while waiting removes the operation without a request, counter, trace, or serial mutation.
3. Collection, nested collection, raw payload, route, target, and profile state are immutable by activation time.
4. Multi-step read-modify-write retains one queue turn and cannot be interleaved by a later operation.
5. Close/dispose reject active and queued work for the retired generation and prevent a queued post-close send.
6. Queue wait does not consume the transaction timeout, and different client instances progress independently.
7. The removed wrapper is absent from assemblies, source, samples, and generated API documentation.

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Full static, unit, sample, documentation, archive, and package checks passed.
- [x] Codex self-review completed and accepted findings corrected.
- [x] Live PLC verification is not required because admission, send order, cancellation, and lifecycle behavior are deterministic locally.
- [x] Documentation, migration notes, changelog, and generated API reference agree.
- [x] Final acceptance criteria verified and the item marked complete.

Verification evidence:

- `run_ci.bat` passed solution/sample builds, generator tests, generated API freshness,
  `dotnet format`, and 440 tests on each of net8.0, net9.0, and net10.0 with no failures or skips.
- Loopback tests proved FIFO wire order, queued cancellation with no send, deep input snapshots,
  active/queued close rejection, compound read-modify-write non-interleaving, queue-wait timeout
  exclusion, and independent progress by separate client instances.
- The generated API contains 50 public types and no `QueuedSlmpClient` or queued-only overload.
  The former queued sample was renamed to the ordinary-client concurrent sample.
- Release-mode NuGet inspection passed with 12 expected runtime/metadata files and no tests,
  samples, repository tooling, or full guide set.
- A virtual Git tree containing the uncommitted delta passed extracted source-archive restore,
  build, generator freshness, format, and 440 tests per target framework.
- No public registry command, commit, push, live PLC communication, or package publication was performed.

Self-review disposition:

- Accepted and corrected: disposing a retired generation token source from the active continuation
  could race the thread executing `Cancel`; explicit disposal was removed so the unreachable
  generation is reclaimed only after its continuations release it.
- Accepted and corrected: an operation admitted concurrently after generation retirement could
  otherwise race transport closure. Lifecycle transitions now hold new-generation admission until
  transport close completes, while still canceling an active connect/request before waiting on the
  transport lock.
- Accepted and corrected: retaining the old `QueuedSample` project name would preserve a misleading
  exported example after wrapper removal. It is now `PlcComm.Slmp.ConcurrentSample` and uses only
  the ordinary client.
- Rejected with rationale: a process-wide gate is not needed; per-instance queue state is required
  so independent clients can progress concurrently, as the loopback test proves.
- No duplicate or deferred finding changes this item.

## GOAL-SERIAL-DEFER-001: complete single-request capacity

Implementation scope: every public `SlmpClient` command and every public helper in
`SlmpClientExtensions`, for TCP/UDP, 3E/4E, the selected canonical PLC profile, label payloads,
and managed result allocation.

Target contract: a single-request operation is accepted only when its complete request and
worst protocol-bounded response fit the selected profile/command, SLMP 16-bit data-length field,
IPv4 transport frame, encoder, decoder, and result representation. This implementation uses
dynamic receive/result buffers, so no fixed internal or caller-owned output capacity further
lowers the protocol/profile maximum. No accepted single-request API creates another request.

Compatibility impact: oversized raw, label, point-count, named, and block operations now fail
before connection opening, 4E serial allocation, last-frame/trace/counter mutation, or send.
Callers must intentionally submit separate operations; there is no compatibility split path.

Acceptance criteria:

1. TCP request payload is at most 65,529 bytes; IPv4 UDP applies 65,492 bytes for 3E and 65,488 bytes for 4E.
2. Profile/command point limits and label aggregate arithmetic are checked before payload allocation or transport.
3. Exact limits produce one request; maximum-plus-one produces a stable argument-range error with no observable request state change.
4. Response framing is bounded by the SLMP length field and decoded into managed storage without truncation or partial success.
5. Generated API/user documentation classifies normal APIs as single-request and records the explicit read-modify-write exception.

- [x] Implementation completed in this repository.
- [x] Tests cover every acceptance criterion and relevant TCP/UDP plus 3E/4E boundary.
- [x] Static, unit, documentation, package, and source-archive checks passed.
- [x] Codex self-review completed and every accepted finding corrected.
- [x] Live PLC verification is not required because length, allocation, request count, and pre-send state are locally deterministic.
- [x] Documentation, changelog, migration notes, and generated API reference agree.
- [x] Final acceptance criteria verified and the item marked complete.

Evidence: exact TCP and IPv4 UDP 3E/4E payload boundaries, label aggregate arithmetic,
profile point limits, no-state-mutation maximum-plus-one cases, and exact request counts pass in
the 451-test suite on net8.0/net9.0/net10.0. Release NuGet contains 12 approved files. The virtual
worktree source tree contains 76 files and passed extracted-only build, generated API freshness,
format, and all 451 tests per target framework.

## GOAL-SERIAL-DEFER-002: one absolute transaction deadline

Implementation scope: explicit connect and every TCP/UDP request phase, including lazy IPv4
resolution/connect, send, TCP header/body assembly, UDP receive, 4E serial filtering, route
filtering, envelope validation, response parsing, cancellation, and transport retirement.

Target contract: the request snapshots `Timeout` at admission and creates one deadline when it
reaches the FIFO head. The same deadline covers lazy connection through completed decoding;
queue wait is excluded. Explicit `OpenAsync` uses one connection deadline. Timeout or active-I/O
cancellation closes the exact transport generation, permits no retry/resend, and requires an
explicit successful `OpenAsync` before reuse.

Compatibility impact: partial frames, wrong serials, foreign routes, and other progress no longer
restart a timeout. A timeout now closes the transport and uses a dedicated exception. Code that
relied on phase-by-phase timeouts or implicit post-timeout reconnect must migrate.

Acceptance criteria:

1. One linked deadline token covers lazy connection, send, complete receive, correlation, validation, and decode.
2. TCP split header/body, UDP silence, wrong-serial floods, and foreign-route floods cannot extend the configured deadline.
3. Queue wait consumes no transaction time and admission snapshots both timeout and monitoring timer.
4. Timeout/cancellation retires transport, delayed data cannot satisfy a later request, and reuse requires explicit open.
5. No timeout, cancellation, or transport failure automatically retries a request.

- [x] Implementation completed in this repository.
- [x] Tests cover every acceptance criterion on applicable TCP/UDP and 3E/4E paths.
- [x] Static, unit, documentation, package, and source-archive checks passed.
- [x] Codex self-review completed and every accepted finding corrected.
- [x] Live PLC verification is not required because deadline and generation behavior are deterministic with loopback transports.
- [x] Documentation, changelog, migration notes, and generated API reference agree.
- [x] Final acceptance criteria verified and the item marked complete.

Evidence: deterministic loopback tests cover TCP header/body delay, UDP silence, wrong-serial and
foreign-route floods, clean explicit reopen, FIFO wait exclusion, and admission-time timeout/timer
snapshots. `run_ci.bat`, package inspection, and the extracted virtual-worktree gate all passed.

## GOAL-ERROR-DEFER-001: machine-readable timeout and unknown outcome

Implementation scope: public connection/request error behavior, lifecycle interruption, PLC end
codes, malformed responses, caller cancellation, and every command that may change PLC state.

Target contract: configured deadline expiration is `SlmpTimeoutException`; retired-session use is
`SlmpNotConnectedException`; local close is `SlmpConnectionClosedException`. If a state-changing
request may have been sent but no definitive PLC response is known,
`SlmpOperationOutcomeUnknownException.Reason` distinguishes timeout, cancellation, close,
malformed response, and transport loss. PLC NG remains `SlmpError` with `EndCode`. No caller needs
message matching, and an unknown outcome is never automatically retried.

Compatibility impact: own-deadline cancellation and generic transport/protocol errors are replaced
by dedicated public classifications. Callers must catch unknown outcome separately, reconcile PLC
state, and must not treat it as a retryable timeout.

Acceptance criteria:

1. Timeout, caller cancellation, close, not-connected, transport loss, malformed response, PLC NG, and outcome unknown are machine distinguishable.
2. Read timeout remains timeout; post-send state-changing timeout/cancellation/close/transport/malformed response becomes structured outcome unknown.
3. Native exceptions remain available as inner causes without becoming the sole classification.
4. Timeout and ambiguous failures retire transport and never resend.
5. User guidance defines safe read retry and mandatory state reconciliation for unknown outcomes.

- [x] Implementation completed in this repository.
- [x] Tests cover every acceptance criterion and structured reason.
- [x] Static, unit, documentation, package, and source-archive checks passed.
- [x] Codex self-review completed and every accepted finding corrected.
- [x] Live PLC verification is not required because classification and possible-send boundaries are locally deterministic.
- [x] Documentation, changelog, migration notes, and generated API reference agree.
- [x] Final acceptance criteria verified and the item marked complete.

Evidence: loopback tests separately prove deadline timeout, caller cancellation, local close,
not-connected reuse, TCP EOF/transport loss, malformed response, PLC end code, and post-send
unknown-outcome reasons for timeout, cancellation, close, transport, and malformed response.
Unknown raw commands conservatively use the state-changing classification.

Self-review disposition:

- Accepted and corrected: `Timeout` and `MonitoringTimer` were originally read only after FIFO
  activation; both are now atomically snapshotted at call admission.
- Accepted and corrected: TCP EOF was originally a generic `SlmpError`, which made transport loss
  indistinguishable from malformed protocol data. It now reports `SlmpTransportException`.
- Accepted and corrected: a whitelist of known write commands could treat a future/unknown raw
  command as retryable read-only work. The classifier now whitelists known reads and treats every
  other raw command conservatively as state-changing.
- Rejected with rationale: `WriteBitInWordAsync` is an explicitly named two-request
  read-modify-write semantic helper, not a hidden aggregate split; its write phase uses the same
  unknown-outcome contract and the full helper retains one FIFO turn.

## GOAL-AGGREGATE-DEFER-001: no implicit state-changing split

Implementation scope: raw/direct/random/block/label operations plus `ReadNamedAsync`,
`WriteNamedAsync`, `PollAsync`, and the explicitly documented bit-in-word read-modify-write helper.

Target contract: normal and named APIs remain single-request. `ReadNamedAsync` rejects any plan
that cannot fit one random-read command; `WriteNamedAsync` rejects mixed bit and word/DWord
families or bit-in-word updates before transport. The library does not auto-split a read or write,
and no state-changing aggregate is silently converted into multiple write requests.
`WriteBitInWordAsync` remains an explicitly named and documented two-request semantic operation,
not an aggregate split; it owns one FIFO turn and exposes the normal outcome-unknown contract for
its write phase.

Compatibility impact: callers needing multiple independent writes issue and account for each
operation themselves. Named calls that require another command family fail instead of returning a
partial read or partially applying updates.

Acceptance criteria:

1. Every single-request API emits at most one request and rejects maximum-plus-one before transport.
2. Named-read plans validate completely and preserve declared input/result order in one request.
3. Named writes emit exactly one random-bit or one random-word/DWord request and reject a mixed plan before sending.
4. Failure exposes no partial named-read result and no partial named-write plan is started.
5. Documentation identifies the explicit read-modify-write helper and does not describe it as an atomic PLC operation.

- [x] Implementation completed in this repository.
- [x] Tests cover every acceptance criterion and exact request counts.
- [x] Static, unit, documentation, package, and source-archive checks passed.
- [x] Codex self-review completed and every accepted finding corrected.
- [x] Live PLC verification is not required because planning, request counts, order, and failure exposure are locally deterministic.
- [x] Documentation, changelog, migration notes, and generated API reference agree.
- [x] Final acceptance criteria verified and the item marked complete.

Evidence: named-read over-capacity/fallback plans and mixed named-write plans fail before opening
transport; valid named read/write plans use one random request. Maximum-plus-one block and direct
helpers remain pre-transport failures. The generated API classifies the ordinary surface as
single-request and documents the explicit read-modify-write exception.

## Accepted self-review findings — RMW documentation and packed consumer

The generated API named `WriteBitInWordAsync` as read-modify-write but did not explicitly state the
important concurrency boundary. Its XML documentation now records that the read and write occupy
one local client FIFO turn while remaining two SLMP requests, with no PLC atomicity against PLC
logic, another client, or an external writer.

The NuGet guard previously inspected entries without consuming the resulting artifact. It now
restores and runs an isolated net8.0 project whose only package source is the generated local NuGet
output. Both the documentation generation and packed-consumer gate passed on 2026-08-01; no
registry publication was performed.

The first final archive rerun exposed that its worktree-attribute option still
archived only the `HEAD` tree. This finding was accepted. The option now creates
a synthetic archive from all non-ignored current-worktree files while honoring
deletions and source-artifact exclusions. The corrected extracted archive ran
the full gate successfully, including 451 tests on each of net8.0, net9.0, and
net10.0.

The cross-ecosystem artifact review additionally found incomplete negative
coverage for repository-only NuGet material. The accepted correction now
rejects CI, cache/build, source, maintainer, release-output, and credential-like
paths/files. The hardened 12-file NuGet consumer gate passed.

## GOAL-CROSS-OS-CI-001-DOTNET-SLMP: bounded Linux lifecycle smoke

Implementation scope: the normal GitHub Actions workflow for the SLMP .NET repository only. The
existing Windows job remains the complete repository gate.

Target contract: one .NET 10 Linux job exercises a focused, deterministic TCP/UDP lifecycle subset
covering IPv4 hostname connection, explicit connection failure, split TCP response reception,
post-send timeout classification, disposal during pending I/O, timeout-driven UDP socket retirement,
explicit reconnect, rejection of a delayed response from the retired socket, and TCP/UDP response
association. The job has an explicit ten minute upper bound and does not duplicate the full
multi-TFM, package, formatting, generated-doc, or source-archive gates.

Compatibility impact: none. This changes CI coverage only and does not alter the package or runtime
contract.

Acceptance criteria:

1. The Linux job restores only the representative test project and runs only on `net10.0`.
2. The selected tests cover fragmented receive, connection failure, bounded timeout, pending-I/O
   disposal, retirement, reconnect, late-response rejection, and TCP/UDP response association.
3. All selected network tests use local controlled peers and never contact a PLC.
4. The job has an explicit timeout and the existing Windows full gate remains unchanged in scope.

- [x] Implementation completed in this repository.
- [x] Existing deterministic tests are selected for every acceptance criterion.
- [x] Relevant CI/static checks passed on the final source state.
- [x] Codex self-review completed after the requested verification run.
- [x] Live PLC verification is not required because the selected checks use controlled local transports.
- [x] Maintainer documentation agrees with the implemented CI scope; no user migration note,
  changelog entry, or generated API change is made for this CI-only item.
- [x] Final acceptance criteria verified and the item marked complete.

## GOAL-DOCUMENTED-API-DIFF-001-SLMP: classified stable-package API differences

Implementation scope: the public API inspector, immutable NuGet and stable-documentation baseline
policy, classification validator, its deterministic unit tests, the required Windows CI gate, and
release-major enforcement. Population of the actual classification set is tracked separately from
the detector implementation.

Target contract: compare the candidate `net8.0`, `net9.0`, and `net10.0` assemblies with the recorded
stable `PlcComm.Slmp` package whose exact version is intentionally retained here as historical
baseline identity and whose bytes are pinned by SHA-256. Every added, removed, or changed public
surface must be explicitly classified as `documented-contract`, `undocumented-public`, `additive`,
or `generated-or-noncontract`; stale or missing classifications fail CI. Documented-contract breaks
require an approved decision, migration, changelog, and machine-readable major-version disposition.
Each classification pins the exact before/after contract signatures, so a later signature drift
cannot reuse an earlier approval. `undocumented-public` and `documented-contract` are checked against
README, the five standard user pages, generated API reference, and maintained samples at the exact
stable source commit associated with the package baseline. The contract signature includes
editor-hidden and compiler-generated accessible members, protected contract surface, fully-qualified
types, default values, generic constraints, inheritance/interfaces, operators, indexer parameters,
property setter/init shape, attributes/modifiers used for nullable/tuple/required/extension/params/in
semantics, enum underlying types, and public constant/enum values. Generated API-reference freshness
is checked in the same required job; exception behavior, XML prose semantics, and package-symbol
content remain explicit Codex self-review responsibilities.

Compatibility impact: none for consumers. The change strengthens release admission. Classification
does not create a compatibility alias or silently permit a documented contract break.

Acceptance criteria:

1. Baseline package bytes are rejected unless their SHA-256 equals the recorded digest.
2. Candidate and baseline APIs are compared independently for all three supported TFMs.
3. Added, removed, and signature-changed surfaces fail when unclassified; duplicate and stale policy entries fail.
4. Duplicate API identities fail instead of being overwritten, and every classification is tied to
   exact before/after signatures.
5. Each classification has rationale and repository evidence; stable-baseline user documentation
   distinguishes documented from undocumented surface.
6. Documented-contract breaks require approval/migration/changelog fields and a major-version
   disposition; release CI enforces the candidate major.
7. The required CI job checks the policy implementation, generated API freshness, and the actual baseline comparison.
8. Final self-review covers exception behavior, XML/generated documentation, profile identifiers, package symbols, and every detector limitation.

- [x] Detector, policy schema, CI gate, policy tests, and release-major enforcement implemented.
- [x] Exact candidate differences generated and every entry classified against the stable contract.
- [x] Deterministic policy tests were added for the classification validator.
- [x] Relevant static, three-TFM unit, generated-document, package, sample, format, and extracted source-archive checks passed on the reviewed worktree state.
- [x] Codex self-review completed against the actual generated difference set, public source changes, package surface, documentation, and detector limitations.
- [x] Live PLC verification is not required because this is a static package/API contract gate.
- [x] Documentation, migration notes, changelog, generated API reference, and classifications agree with the final comparison.
- [x] The release-major gate correctly rejected current version `4.0.1` because documented incompatible changes require major `5`.
- [x] Update the actual release version to major `5` or later and record final release acceptance.

Current actual-diff disposition: the authorized comparison found 188 distinct API differences with
the same signatures on all three TFMs, expanded to 564 exact per-TFM classification records. The
101 removed queued-wrapper/type-specific APIs and two factory return-type changes are
`documented-contract` under `GOAL-SERIAL-DEFER-006` and require candidate major 5. The 17 new
structured lifecycle/outcome error APIs are `additive`. The remaining 68 differences are
`generated-or-noncontract`: after removing only the compiler-generated async/iterator state-machine
attribute, their callable signature, nullability, defaults, modifiers, and public contract are byte-
for-byte identical. Every record pins its complete before/after signature; no wildcard or blanket
namespace suppression is used.

Verification evidence (2026-08-01): the exact API gate passed all 564 classifications with no
unclassified or stale record. The worktree and extracted source archive each passed 451 tests on
net8.0, net9.0, and net10.0; all six net10.0 samples, generated API freshness, package consumer,
format, and source-archive validation passed. Candidate-major enforcement rejected `4.0.1` because
major `5` is required. No version was changed and no package was published.

Final release acceptance (2026-08-07): the actual candidate version is `5.0.0`, and the complete
repository release gate passed with the approved API classifications. No package was published.

## GOAL-DOTNET-SAMPLE-TFM-001-SLMP: user samples target .NET 10

Implementation scope: the six projects under `samples`, the sample README, user Getting Started
prerequisites, and the changelog.

Target contract: every user-facing sample targets `net10.0`, while the reusable library and test
projects continue to target `net8.0`, `net9.0`, and `net10.0`. No maintainer-only validation project
is changed merely for symmetry.

Compatibility impact: users building repository samples need the .NET 10 SDK. Package consumers and
the library's supported TFMs are unchanged.

Acceptance criteria:

1. All six user-facing sample project files target exactly `net10.0`.
2. The library and test projects retain `net8.0;net9.0;net10.0`.
3. Sample prerequisites and the compatibility impact are recorded in sample documentation and the changelog.

- [x] Implementation completed in this repository.
- [x] All six sample restore/build and relevant archive/package checks passed on the reviewed worktree state.
- [x] Codex self-review completed after the requested verification run.
- [x] Live PLC verification is not required because the sample TFM change does not alter PLC communication.
- [x] Documentation and changelog agree with the implemented target-framework change.
- [x] Final sample acceptance criteria verified by the executed worktree and extracted source-archive gates.

## GOAL-DOTNET-R1-20260801: single-request named reads and explicit long-timer routes

Stable identifier: `GOAL-SLMP-REVIEW-R1-001-DOTNET`.

Implementation scope: named-read planning/execution, polling plan reuse, runtime guidance,
tests, user documentation, generated API documentation, migration notes, and changelog.

Target contract: `ReadNamedAsync` and each `PollAsync` cycle emit exactly one Random Read or
reject the complete plan before connection, request state, trace, counters, or transport.
`LTN`, `LSTN`, `LTS`, `LTC`, `LSTS`, and `LSTC` never enter an implicit Direct Read fallback;
typed scalar and explicit long-timer helpers retain those supported routes.

Compatibility impact: callers that supplied a long-timer Direct Read family to a named read
must call `ReadTypedAsync`, `ReadLongTimerAsync`, or `ReadLongRetentiveTimerAsync` explicitly.

Acceptance criteria:

1. Mixed `D100:U` plus each long-timer Direct family fails as one complete zero-send plan.
2. Named execution contains no long-timer or generic fallback branch capable of another request.
3. Typed and explicit long-timer routes remain available and documented.
4. Runtime guidance, user docs, generated API, migration, and changelog agree.

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Relevant static, unit, sample, package, API, and generated-document checks passed.
- [x] Codex self-review completed and every accepted finding corrected.
- [x] Live PLC verification is not required; admission and request count are deterministic local properties.
- [x] Documentation, migration notes, changelog, and generated API agree.
- [x] Final acceptance criteria verified and the item marked complete.

## GOAL-DOTNET-D1-20260801: definitive result precedence across close and disposal

Stable identifier: `GOAL-SLMP-REVIEW-D1-001-DOTNET`.

Implementation scope: FIFO operation completion, command-specific decoding, close/disposal
races, success/end-code/error classification, tests, documentation, and changelog.

Target contract: after route/serial correlation, protocol/end-code validation, response-length
validation, and command-specific result construction, a success or PLC end-code is definitive
and cannot be replaced by concurrent `Close`, `CloseAsync`, `Dispose`, or `DisposeAsync`.
Before that point, an incomplete read is closed and a possibly transmitted state change is
outcome-unknown with reason `Closed`; retired queued work sends nothing.

Compatibility impact: narrow races can now return the already-established PLC result instead
of closed/disposed status, preventing unsafe retries of operations whose result is known.

Acceptance criteria:

1. Deterministic post-decode barriers preserve read success for all four lifecycle methods.
2. A framed PLC end-code and a structurally valid empty-payload acknowledgement retain their definitive results.
3. A non-empty ack-only success payload is malformed and remains outcome-unknown; close or timeout before acknowledgement decode retains the corresponding unknown reason.
4. Queued zero-send rejection, prompt transport retirement, reconnect, and terminal disposal remain intact.

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Relevant static, unit, sample, package, API, and generated-document checks passed.
- [x] Codex self-review completed and every accepted finding corrected.
- [x] Live PLC verification is not required; deterministic local transports cover lifecycle races.
- [x] Documentation, migration notes, changelog, and generated API agree.
- [x] Final acceptance criteria verified and the item marked complete.

## GOAL-DOTNET-D2-20260801: bounded public counts and textual numeric fields

Stable identifier: `GOAL-SLMP-REVIEW-D2-001-DOTNET`.

Implementation scope: Direct DWord/Float32 reads and writes, named-target parsing,
U-qualified parsing, CLI route/count parsing, tests, XML/generated API, user docs, and changelog.

Target contract: numeric DWord/Float32 counts fail with `ArgumentOutOfRangeException` naming
the public parameter before multiplication, narrowing, allocation, admission, or transport.
The public limit is 480 values for a 960-word profile limit. Malformed, negative, overflowing,
or field-width-invalid route text fails with `FormatException`: byte route fields are 0..255,
and module I/O plus U extension fields are 0..65535.

Compatibility impact: leaked `OverflowException` and internal word-unit count errors become the
stable public exception category for the supplied typed/count or textual input.

Acceptance criteria:

1. Read and write zero, one, 480, 481, 32768, and ushort-maximum boundaries are classified before unsafe arithmetic.
2. Named target field boundaries and invalid text identify the field and range without truncation.
3. U0000/UFFFF and J0/J255 parse; U/J signs, empty or malformed digits, width overflow, and oversized digits return field/range-bearing `FormatException`.
4. The CLI does not demonstrate checked narrowing of unbounded route or point text.

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Relevant static, unit, sample, package, API, and generated-document checks passed.
- [x] Codex self-review completed and every accepted finding corrected.
- [x] Live PLC verification is not required; numeric validation and zero-send are local properties.
- [x] Documentation, migration notes, changelog, and generated API agree.
- [x] Final acceptance criteria verified and the item marked complete.

## GOAL-DOTNET-N1-20260801: exact semantic device-unit APIs

Stable identifier: `GOAL-SLMP-REVIEW-N1-001-DOTNET`.

Implementation scope: canonical device metadata, Direct/Extended bit methods, Random bit writes,
Block categories, typed/named helpers, CLI routing, tests, docs, migration, and changelog.

Target contract: one exhaustive classifier assigns every public device code to bit or word.
Every semantic bit-unit or bit-entry API requires a bit device. Typed/named `BIT` requires bit,
and numeric types require word. Block categories are strict in both directions. Explicit low-level
word methods retain valid packed 16-bit access to bit devices, and `.n`/explicit RMW remains the
only word-device single-bit route. G/HG remain qualified word-only families.

Compatibility impact: invalid word-device bit calls and numeric bit-device typed/named calls now
fail locally with `ArgumentException`; intentional bit-device packing migrates to explicit word APIs.

Acceptance criteria:

1. One exhaustive classifier covers every enum value and is reused by semantic validators.
2. Every word family fails Direct, Extended/link, Random bit, and bit Block surfaces with zero transport.
3. Every bit family fails word Block and `WriteBitInWordAsync` surfaces; explicit packed word access to M remains encoded as word-unit Direct Read.
4. Typed/named write semantics and values are fully planned before FIFO admission; queued invalid writes fail immediately without client state or transport effects.
5. Typed/named mappings, bit-in-word guidance, docs, migration, and changelog agree.

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Relevant static, unit, sample, package, API, and generated-document checks passed.
- [x] Codex self-review completed and every accepted finding corrected.
- [x] Live PLC verification is not required; device classification and subcommand selection are local properties.
- [x] Documentation, migration notes, changelog, and generated API agree.
- [x] Final acceptance criteria verified and the item marked complete.

## Verification evidence and self-review — R1/D1/D2/N1 (2026-08-01)

Evidence: the final worktree and a synthetic extracted source archive each built without warnings
and passed 514 tests on each of `net8.0`, `net9.0`, and `net10.0`. The generated API reference was
fresh, all six samples built, formatting passed, the 12-file NuGet package restored and ran in an
isolated `net8.0` consumer, the profile fixtures reported no drift, and the registry-publication
guard passed. The immutable-package API comparison passed all 564 exact classifications across the
three TFMs. No PLC communication or public-registry publication was performed.

The implementation added no new unclassified public member addition or removal. Adding private
lifecycle/decode machinery changed 62 compiler-generated async state-machine ordinals on each of
three TFMs. The API-policy key sets still matched exactly, with zero stale or unclassified keys;
the 186 exact `after` signatures were synchronized while retaining their existing classifications
and rationale. Callable signatures, nullability, defaults, and public contract fields were unchanged
by that synchronization.

Accepted and corrected self-review findings:

- Command-specific decoding initially occurred after the transaction deadline scope. It now runs
  inside the correlated transaction before a result becomes definitive.
- Moving decode into the transaction initially retired a healthy transport for read-only semantic
  decode errors. An internal decode wrapper now preserves the original exception and connection
  when no cancellation or lifecycle transition occurred, while state-changing or cancelled work
  keeps the required failure classification.
- An empty named-read plan could return a zero-request success, and a structurally invalid internal
  compiled plan could reach transport before failing. Empty input and the complete compiled entry
  structure are now rejected in preflight.
- Empty named-target numeric fields still used the former generic `ArgumentException`. They now use
  the same field-specific, range-bearing `FormatException` as other malformed numeric text.
- U/J qualified parsers recognized only already-valid digit shapes, so signs, empty fields, and
  malformed digits fell through to the generic device parser. Qualified candidates now reach the
  field-width validator and report the U extension or J-direct network field and valid range.
- Ack-only commands initially treated any success payload as a definitive acknowledgement. They now
  require an empty command payload; malformed success payloads keep state-changing outcome-unknown
  classification, while `RawCommandAsync` and send-only remote reset retain their explicit contracts.
- `WriteBitInWordAsync` initially inherited packed word access and consequently accepted bit-device
  families. It now requires a canonical word device before FIFO admission, while low-level packed
  word methods remain available separately.
- Typed and named write semantic planning initially occurred after outer FIFO admission. Complete
  route, unit, shape, value, duplicate, overlap, and downstream command validation now occurs before
  the one underlying request is admitted; a deterministic occupied-FIFO test proves invalid plans do
  not wait or mutate client state.
- User guidance initially named a nonexistent `ReadBitInWordAsync`. It now documents `.n` named read
  notation and the existing word-only `WriteBitInWordAsync` helper.

Rejected with rationale: restoring the former post-result generation-retired check would replace a
fully decoded success or framed PLC end-code with a later close/dispose result and therefore violate
the approved D1 definitive-result boundary. No self-review findings were classified as duplicate or
deferred.

## GOAL-SLMP-SPAN-20260801 — Complete wire-address span admission

Stable identifier: `SLMP-SPAN-20260801-DOTNET`.

Implementation scope: .NET contiguous Direct word/bit/DWord/Float32 operations,
Random entries, Monitor registration, Block routes, applicable Extended Device
routes, long-timer Direct status blocks, validation ordering, tests, user and
generated API documentation, migration notes, and changelog.

Target contract: before connection, frame publication, request-counter mutation,
or transport, every applicable operation proves that its complete consumed
device span fits the selected address field. Q/L-compatible and link-direct wire
layouts use 24 bits and iQ-R layouts use 32 bits. Word devices consume one number
per word, packed word access to bit devices consumes 16 numbers per word,
ordinary DWord/Float32 values consume two word-device numbers, packed bit-device
DWords consume 32 numbers, bit blocks consume 16 bit-device numbers per block
point, and Direct LTN/LSTN status blocks consume one logical device per four wire
words. Random/Monitor long scalar entries retain their existing one-device
semantic width. This is wire representability only; canonical profile usable
ranges are not pre-send guards.

Compatibility impact: requests that previously wrapped or truncated their final
device number, or reached transport with an unrepresentable span, now fail
locally with `ArgumentOutOfRangeException`. Exact-boundary requests remain
admitted. No compatibility alias or silent split is retained.

Machine-verifiable acceptance criteria:

1. Q/L-compatible 24-bit and iQ-R 32-bit Direct word/bit read and write accept
   one point at the maximum and reject a two-point span from that maximum with
   zero client-state or transport effects.
2. Ordinary DWord/Float32 read and write accept one value at maximum-minus-one,
   reject two values there, and reject one value at the maximum.
3. Packed bit-device word/DWord and bit-block routes use 16/32-device expansion;
   Direct long-timer status blocks use one logical device per four wire words.
4. Random and Monitor DWord entries and word/bit Block reads and writes apply the
   same route-specific span rules, including Extended Device layouts where that
   contiguous-width contract applies.
5. Validation uses checked wide arithmetic, runs before connection/frame/counter
   mutation, and does not consult the profile device-range catalog.

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Relevant static checks, unit tests, integration tests, examples, and package/build checks passed.
- [x] Codex self-review completed against the approved contract and cross-language consistency requirements.
- [x] Live PLC checks are not required; wire-field arithmetic and zero-send admission are deterministic local properties.
- [x] Documentation, migration notes, changelog, and generated API reference agree with the implementation.
- [x] Final acceptance criteria verified and the item marked complete.

Self-review disposition (2026-08-01):

- Accepted: the initial shared width helper treated every `LTN`/`LSTN` use as a
  four-word Direct status block, which would have misclassified Random/Monitor
  scalar entries. Direct long-status semantics and scalar DWord-entry semantics
  are now explicit and independently tested.
- Accepted: existing Random/Extended Random and Block write-overlap checks still
  used fixed logical widths. They now use the same packed bit-device 16/32-width
  model as admission, with overlap regression tests and wide end arithmetic.
- Accepted: Block Read initially applied ordinary word width to Direct
  `LTN`/`LSTN` status blocks. It now applies the approved four-wire-words per
  logical-device width, with exact and overflowing Block boundaries tested.
- Accepted: Extended Random overlap and bit-duplicate identity initially compared
  nullable extension fields instead of the effective encoded extension. Null and
  explicit zero now identify the same wire route and reject overlap/duplicates.
- Accepted: long-timer helpers and typed, named, and polling reads initially
  completed span admission only after FIFO entry. Their exact underlying route is
  now validated before FIFO waiting; an occupied-FIFO regression proves invalid
  calls fail immediately with zero added state or transport effects.
- Accepted: the first typed-read preflight reused the public low-level Direct Bit
  guard for `LCS`/`LCC`, although the typed helper intentionally owns that Direct
  route. Typed preflight now mirrors its unchecked internal bit route while the
  public low-level rejection remains unchanged.
- Accepted: the first named-read preflight delegated its 256-entry count failure
  to the low-level Random guard and changed the established high-level diagnostic.
  The one-request named limit now runs first and retains its documented error.
- Accepted: `WriteBitInWordAsync` initially admitted its read before validating
  the eventual write. Read and write feature, policy, point, profile, and span
  admission now all complete before FIFO waiting, so an invalid target sends
  neither request.
- Accepted: initial coverage did not exercise packed bit-device DWord/Float32,
  link-direct 24-bit Extended Device, or long-timer exact-boundary behavior.
  Focused zero-send and exact-boundary tests now cover each applicable route.
- Accepted: initial positive-boundary coverage omitted the independent Extended
  methods and native Random/Monitor DWord families. Exact-boundary transport tests
  now cover the independent Extended read/write routes and native
  `LTN`/`LSTN`/`LCN`/`LZ` scalar width.
- Accepted: the generated API reference was stale after the final XML contract
  clarification. It was regenerated from the final public assembly/XML surface.
- Rejected: enforcing the configured PLC device-range catalog here would turn a
  wire-representability invariant into profile policy and contradict the
  approved contract; no such guard was added.
- Deferred: none. Live PLC communication is not required for deterministic
  arithmetic and pre-transport state assertions.

Verification evidence: after every accepted self-review correction,
`run_ci.bat` built all targets and samples without warnings, confirmed generated
API freshness, passed 528 tests on each of `net8.0`, `net9.0`, and `net10.0`, and
passed formatting. The separate Release NuGet package/isolated `net8.0` consumer
contract passed with the expected 12-file package. `git diff --check` passed; no
live PLC communication, commit, push, or publication was performed.

## GOAL-SLMP-ERROR-INFO-CORRELATION-20260802 — Correlate structured error information

Stable identifier: `SLMP-ERROR-INFO-CORRELATION-001`.

Implementation scope: the .NET `SlmpClient` TCP and UDP response paths for 3E
and 4E responses with a nonzero end code and at least nine bytes of structured
error information, including propagation through `QueuedSlmpClient`.

Target contract: when a nonzero-end-code response contains the nine-byte SLMP
error-information prefix, its network, station, module I/O, multidrop, command,
and subcommand fields must match the immutable target and command identity of
the active request. Any mismatch is a malformed response rather than a
definitive PLC end-code result. For a state-changing request, that mismatch
produces `SlmpOperationOutcomeUnknownException` with reason
`MalformedResponse`, invalidates the active transport generation, and requires
an explicit reopen before later communication. A matching prefix retains the
existing PLC-error behavior. Bytes following the required nine-byte prefix
remain permitted and are retained as the error information's additional data;
their presence alone is not malformed.

Compatibility impact: a peer, gateway, or delayed response whose structured
error information identifies another route or command is no longer exposed as
the current request's definitive PLC error, and the affected transport cannot
be reused. Applications that retried based on that former classification must
instead treat a state-changing request as outcome-unknown. The behavior of a
nonzero-end-code response that does not contain the complete nine-byte
error-information prefix is intentionally undecided and outside this item.

Machine-verifiable acceptance criteria:

1. TCP and UDP tests for both 3E and 4E independently mismatch network,
   station, module I/O, multidrop, command, and subcommand in an otherwise
   structurally valid nonzero-end-code response; every case is classified as
   malformed and never as the current request's definitive PLC error.
2. A state-changing request receiving each mismatched response fails with
   `SlmpOperationOutcomeUnknownException`, reason `MalformedResponse`, retires
   the transport generation, rejects implicit reuse, and permits communication
   only after explicit reopen.
3. A read-only request receiving a mismatched structured error follows the
   established malformed-response classification and transport-invalidation
   contract without exposing the mismatched PLC end code as definitive.
4. For each transport and frame format, an exactly matching route, command, and
   subcommand preserves the existing `SlmpError` end-code result.
5. Matching error information with zero, one, and multiple bytes after the
   nine-byte prefix remains accepted, and every additional byte is retained in
   `SlmpErrorInfo.Extra` without truncation.
6. A representative queued-client test proves the same exception, outcome
   reason, and transport-generation behavior as the direct client.
7. The acceptance suite passes independently on `net8.0`, `net9.0`, and
   `net10.0`; no criterion relies on live PLC communication.

- [x] Implementation completed in every affected repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Relevant static checks, unit tests, integration tests, examples, and package/build checks passed.
- [x] Codex self-review completed against the approved contract and cross-language consistency requirements.
- [x] Required live-PLC checks passed, or each unavailable check has an explicit release disposition.
- [x] Documentation, migration notes, changelog, and generated API reference agree with the implementation.
- [x] Final acceptance criteria verified and the item marked complete.

### Verification evidence and self-review disposition (2026-08-02)

- `run_ci.bat`: PASS. Build, release-version checks, API-generator tests,
  generated API freshness, formatting, and all 580 tests on each of `net8.0`,
  `net9.0`, and `net10.0` completed with zero failures or skips.
- Deterministic loopback fixtures cover TCP and UDP, 3E and 4E, all six
  independently mismatched identity fields, read-only and state-changing
  classifications, transport retirement, explicit reopen, and matching
  prefixes followed by zero, one, or three retained extra bytes.
- `QueuedSlmpClient` is non-applicable to acceptance criterion 6 because the
  approved `GOAL-SERIAL-DEFER-006` removed that type and expressly supersedes
  every historical parity requirement. Exported-surface tests continue to
  prove its absence; `SlmpClient` is the sole supported live-client path.
- Codex self-review inspected the response parse order, public error surface,
  immutable target and command comparison, short-error boundary, read/write
  classification, invalidation and reopen lifecycle, tests, documentation,
  generated API, and cross-language behavior. Accepted findings: none.
  Rejected findings: none. Duplicate findings: none. Deferred findings: none.
- Live PLC verification is not required for this item: correlation and
  lifecycle behavior are completely observable with deterministic transport
  fixtures, and no PLC/profile compatibility claim changed. No live PLC
  communication was performed.
