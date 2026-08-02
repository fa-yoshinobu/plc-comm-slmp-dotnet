# .NET SLMP performance optimization acceptance record

This record applies the approved workspace contracts PERF2-002, PERF2-004,
and PERF2-008 to the .NET implementation. These are internal optimizations;
wire bytes, public results, validation/error order, FIFO behavior, lifecycle,
timeouts, cancellation, serial handling, tracing, and error ownership remain
unchanged.

## PERF2-002 — Prepared named polling

- Scope: `PollAsync`, named-read planning, and Random Read execution.
- Target contract: validate and encode one client/profile/frame/compatibility-bound immutable Random Read payload and compact decode-index plan once per polling stream, then reuse both for every cycle.
- Compatibility impact: none; no public signature or behavior changes.
- Acceptance criteria: two cycles report one preparation, transmit identical payload bytes, decode by stored indexes without per-cycle address maps, and retain the normal client transaction path.
- [x] Implementation completed in this repository.
- [x] Tests added for every acceptance criterion available without a live PLC.
- [x] Multi-target build and targeted tests passed.
- [x] Codex self-review completed against the approved contract.
- [x] Live PLC checks are not required because wire bytes and transport behavior are unchanged and covered by deterministic tests.
- [x] User/generated API documentation and changelog agree with the implementation.
- [x] Final acceptance criteria verified.

## PERF2-004 — Response-frame view decoding

- Scope: private TCP/UDP response parsing and typed command decoders.
- Target contract: typed decoders consume a private `ReadOnlyMemory<byte>` view into the owned response frame; the view cannot escape. Public raw, trace, error, and byte-result surfaces retain owned data.
- Compatibility impact: none.
- Acceptance criteria: typed polling does not materialize a response-payload copy, raw access does, byte-returning results remain owned, and malformed/error behavior is unchanged.
- [x] Implementation completed in this repository.
- [x] Tests added for typed and raw ownership behavior.
- [x] Multi-target build and targeted tests passed.
- [x] Codex self-review completed against the approved contract.
- [x] Live PLC checks are not required for private ownership behavior.
- [x] User/generated API documentation and changelog agree with the implementation.
- [x] Final acceptance criteria verified.

## PERF2-008 — Exact-size Extended payload encoding

- Scope: Extended Random read/write and Extended Monitor registration.
- Target contract: first resolve and validate every entry while calculating the checked exact size, then allocate one final payload and encode directly into it; the second pass performs no validation and creates no per-device encoded byte array.
- Compatibility impact: none; accepted bytes and validation/error order remain unchanged.
- Acceptance criteria: golden bytes remain identical, each valid builder records one final payload and zero owned device-spec encodes, and invalid input fails before final allocation.
- [x] Implementation completed in this repository.
- [x] Tests added for all four builders, validation boundary, and byte equality.
- [x] Multi-target build and targeted tests passed.
- [x] Codex self-review completed against the approved contract.
- [x] Live PLC checks are not required because protocol bytes are unchanged.
- [x] User/generated API documentation and changelog agree with the implementation.
- [x] Final acceptance criteria verified.
