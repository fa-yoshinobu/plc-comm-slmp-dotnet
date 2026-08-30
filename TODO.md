# TODO

Current active TODOs only.

## Current Status

### SLMP-DOTNET-TODO-1: Remove unused Memory / Extend Unit functions from the public API

Status: `approved`. Complete this public API cleanup in the next release. There are no users of these methods, so no compatibility alias or migration path is required.

- [ ] Remove these 10 public methods:
  - `MemoryReadWordsAsync`
  - `MemoryWriteWordsAsync`
  - `ExtendUnitReadBytesAsync`
  - `ExtendUnitReadWordsAsync`
  - `ExtendUnitReadWordAsync`
  - `ExtendUnitReadDWordAsync`
  - `ExtendUnitWriteBytesAsync`
  - `ExtendUnitWriteWordsAsync`
  - `ExtendUnitWriteWordAsync`
  - `ExtendUnitWriteDWordAsync`
- [ ] Do not retain public compatibility aliases or deprecated wrappers for commands `0x0601`, `0x0613`, `0x1601`, or `0x1613`.
- [ ] Keep command encoding/decoding private only if another internal path requires it; otherwise remove it.
- [ ] Update the public API baseline, tests, API reference, and changelog, then run the repository release gate and self-review.

The approved cross-library contract is recorded in [DECISION-SLMP-PUBLIC-API-001](https://github.com/fa-yoshinobu/plc-comm-publish/blob/main/slmp_library_next_improvement_goal_20260830.md#decision-slmp-public-api-001-未使用のmemory--extend-unit関数を非公開化する).
