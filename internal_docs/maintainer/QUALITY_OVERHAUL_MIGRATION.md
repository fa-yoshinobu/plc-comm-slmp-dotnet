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
- Acceptance criteria: options and client expose the approved defaults; golden frames contain `0x0010`; timeout validation happens before I/O.
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
- Target contract: one client permits one in-flight exchange, allocates 4E serials inside that lock, matches response serials, and closes transport after timeout/cancellation or transport failure. Maintainer trace failures cannot affect communication.
- Compatibility: callers cannot rely on concurrent pipelining or reuse a cancelled session.
- Acceptance criteria: concurrent 4E calls have unique serials; mismatched serials are ignored; UDP timeout closes the socket and prevents delayed-response reuse.
- [x] Implementation completed.
- [x] Tests completed.
- [x] Documentation updated.

## D-019 / D-020 — Random categories and write uniqueness

- Scope: normal and Extended Device random read/write.
- Target contract: category-specific word/DWord methods omit the unused category; every-category-empty requests fail; duplicate or overlapping write destinations fail before transport.
- Compatibility: callers may stop passing placeholder empty lists.
- Acceptance criteria: specialized methods exist on base and queued clients; empty and overlap tests leave the client unopened.
- [x] Implementation completed.
- [x] Tests completed.
- [x] Generated API reference updated.

## D-021 / D-022 / D-023 / D-037 — Block request integrity

- Scope: word/bit block read/write.
- Target contract: category-specific methods omit unused lists; mixed blocks remain one request; all-empty requests and overlapping write ranges fail before transport; no split flag exists.
- Compatibility: automatic split callers must issue separate requests and handle timing/partial success themselves.
- Acceptance criteria: one mixed call creates one frame; empty and overlap tests send zero frames; public surface has no split option.
- [x] Implementation completed.
- [x] Tests completed.
- [x] Documentation updated.

## D-024 / D-025 / D-026 — Explicit remote state changes

- Scope: Remote RUN and PAUSE.
- Target contract: required `SlmpRemoteMode` selects normal/force; RUN also requires `SlmpRemoteClearMode`; undefined enum values fail before transport.
- Compatibility: default Boolean and numeric-mode calls no longer compile.
- Acceptance criteria: required-parameter reflection tests and frame vectors cover the defined values.
- [x] Implementation completed.
- [x] Tests completed.
- [x] Generated API reference updated.

## D-027 / D-028 — Fixed Remote RESET

- Scope: Remote RESET.
- Target contract: the API exposes no subcommand or response option; it sends command `0x1006`, subcommand `0x0000`, payload `0x0001`, and completes after send without treating absent success response as timeout.
- Compatibility: configurable reset callers must migrate.
- Acceptance criteria: the shared frame vector is captured without requiring a response.
- [x] Implementation completed.
- [x] Tests completed.
- [x] Documentation updated.

## D-029 — Profile-derived remote password payload

- Scope: remote password lock/unlock.
- Target contract: callers provide a non-empty profile-valid password only; payload form comes from the connection profile and no series argument exists.
- Compatibility: series override calls are removed.
- Acceptance criteria: fixed/variable length validation occurs before transport and profile vectors remain deterministic.
- [x] Implementation completed.
- [x] Tests completed.
- [x] Documentation updated.

## D-030 — Optional label abbreviations with validation

- Scope: array/random label read/write.
- Target contract: omission encodes zero abbreviations; explicit definitions are ordered `%1`, `%2`, and so on; empty/malformed/out-of-range references, empty points and count overflow fail before transport.
- Compatibility: malformed labels formerly encoded are rejected.
- Acceptance criteria: zero, multiple, malformed, empty and overflow tests pass.
- [x] Implementation completed.
- [x] Tests completed.
- [x] Documentation updated.

## D-031 / D-032 / D-033 — Explicit CPU and long-timer selection

- Scope: CPU-buffer and long timer/retentive timer helpers.
- Target contract: CPU-buffer calls require `SlmpCpuModule.Cpu1` through `Cpu4`; long-family multi-point helpers require head and point count. Undefined module and zero/over-limit counts fail before transport.
- Compatibility: implicit CPU1, head zero and one-point defaults are removed.
- Acceptance criteria: parameters are non-optional, module wire values are typed, and multi-point timer read uses one bounded request.
- [x] Implementation completed.
- [x] Tests completed.
- [x] Documentation updated.

## D-035 / D-036 — No hidden multi-request contiguous access

- Scope: continuous word/DWord reads and writes and high-level named batching.
- Target contract: one contiguous API call emits at most one request and rejects counts above the selected profile limit; no chunked helper or split option is public. Mixed named command families are explicitly documented as non-atomic and named writes as potentially partially successful.
- Compatibility: chunk/split callers must implement their own request and consistency policy.
- Acceptance criteria: public-surface scan has no chunk helpers; limit tests send zero requests; docs do not describe mixed named values as one PLC instant.
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
- `dotnet test PlcComm.Slmp.sln --configuration Release --no-restore`: PASS on `net8.0`, `net9.0`, and `net10.0`; 281 tests per target framework, zero failed or skipped.
- `python scripts/test_generate_api_reference.py`: PASS, 4 tests.
- generated API reference regeneration plus `--check`: PASS; 50 documented public types and maintainer-only raw command omitted through `EditorBrowsable(Never)`.
- `dotnet pack ... --configuration Release --no-build`: PASS; `.nupkg` and `.snupkg` created locally for packaging validation only.
- Multi-PLC and JSON polling `--dry-run`: PASS with explicit port, transport, target, profile and dtype; no PLC communication.
- CLI `--help`: PASS and documents mandatory endpoint, route and operation fields.
- public-surface/stale-name scan: PASS; no user-facing extension wire record, split/chunk option, localized end-code message/language, strict-profile bypass, raw request or thread-safety warning remains.
- `git diff --check`: PASS; line-ending conversion warnings only.

Codex self-review inspected the actual diff, exported API, constructor and validation order, profile/address binding, immutable target, write overlap rules, request locking, 4E matching, TCP/UDP timeout and cancellation invalidation, fixed Remote RESET, label input validation, tests, samples, generated documentation and packaging. It found and corrected two issues during review: missing profile checks on semantic Extended Device paths and mutable target routing after construction.
