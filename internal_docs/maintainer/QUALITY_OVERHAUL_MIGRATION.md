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
- [ ] Claude source review completed and findings recorded. **Pending explicit user authorization; do not invoke automatically.**
- [ ] Codex resolved or dispositioned every Claude finding and reran affected checks.
- [x] Required live-PLC checks passed, or each unavailable check has an explicit release disposition.
- [x] Documentation, migration notes, changelog and generated API reference agree with implementation.
- [ ] Final acceptance criteria verified and item marked complete.

## Live-PLC disposition

These changes are API shape, pre-transport validation, deterministic frame generation, mock transport state or documentation behavior. No new physical PLC claim is made and no live PLC communication was authorized or performed. Existing hardware/profile compatibility evidence remains unchanged. Any later profile-specific live check remains `unverified` until separately proposed with target, endpoint, address and intent and explicitly authorized.

## Claude review package status

The approved decisions, this record, repository diff and final local results will form the review package. Claude execution is pending explicit user authorization for a named diff scope and must not be started automatically.

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
- [ ] Claude review of this delta completed — pending a separately authorized batch.
- [ ] New public-API live verification completed — deferred until after Claude review.
- [x] D-132 Extend Unit versus HG physical-area classification completed: independent values remained stable through immediate, 50 ms, 250 ms, and 1 s cross-reads.
- [x] Removed the misleading CPU-buffer aliases and alias-only enum; retained distinct Extend Unit and qualified HG surfaces.
