# .NET SLMP quality-overhaul decision and acceptance record

This maintainer record maps the approved workspace decisions to the .NET implementation. Breaking changes are intentional where compatibility conflicts with an explicit, profile-safe, single-request contract.

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
| D-13 | Accepted as the defined lifecycle. Connection establishment has its own timeout; the one absolute request deadline begins after a session is open and covers send plus response correlation. |
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
