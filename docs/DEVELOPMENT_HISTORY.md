# Development History

## 2026-06-11 Archived Refactor Plan

The previous `refactor-instructions.md` was archived into this history file.

### Scope

- Library: .NET SLMP package.
- Primary task: add direct characterization tests for pure payload builders/decoders in `SlmpClient.cs`, then move static builder logic into an internal helper class.
- Optional small task: move read-plan internals from `SlmpClientExtensions.cs`.

### Contracts To Preserve

- All public types, methods, signatures, defaults, and NuGet-facing API.
- Exact transmitted frame bytes covered by `SlmpFrameVectorTests` and shared vectors.
- Guard behavior and exception contracts covered by `SlmpClientGuardTests`.
- `QueuedSlmpClient` serialization semantics used by downstream apps.
- Semantic atomicity from the high-level API contract.
- NuGet package ID, version, changelog, and packaging metadata.

### Debt Notes

- D1: `SlmpClientPayloadTests` had minimal direct coverage of internal builders.
- D2: `SlmpClient.cs` mixed TCP/UDP transport, frame send/receive, and pure payload construction.
- D3: read-plan optimization internals lived in `SlmpClientExtensions.cs` and could be moved if low risk.

### Planned Verification

- Capture baseline test results.
- Add characterization tests for representative builder inputs using current byte output.
- Move static builders into internal `SlmpPayloads`; leave instance-dependent builders in place unless safely parameterized.
- Run the full test suite after each phase and record moved declarations or skipped work.

### Out Of Scope

- Public API changes.
- Frame-byte or guard-behavior changes.
- Package metadata, version, changelog, or downstream app changes.
