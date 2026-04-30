# TODO

This file tracks active follow-up items for the SLMP .NET library.

## 1. Active Follow-Up

- **`G/HG` Extended Specification live coverage expansion**
  The current capture-aligned implementation is working on the validated paths,
  but broader address-range, transport, and PLC-family coverage is still open.

- **Mixed block write root cause**
  The practical fallback is implemented, but the reason some validated PLC
  paths reject the first one-request mixed `1406` write with `0xC05B` is still
  not fully explained.

- **`1617` Clear Error operator-visible effect**
  Transport-level acceptance is confirmed, but the operator-visible behavior on
  real hardware still needs better evidence.

- **Device-range runtime exploration method**
  Some MELSEC families report device range values that can exceed the range
  accepted by device read commands. Fix the range resolution method by adding
  reusable runtime exploration that verifies actual readable bounds with device
  reads and caches the result per PLC family/device. This must be a general
  mechanism, not hard-coded correction for individual devices. Q/Qn-series
  `Z`, `R`, and `ZR` are known examples where PLC-reported ranges can be
  misleading.

## 2. Practical Limits

- ASCII mode is intentionally out of scope.

## 3. Completed Recently

- [x] **Stabilize the shared high-level contract**: The public surface is aligned with the sibling .NET libraries around `OpenAndConnectAsync`, `ReadTypedAsync`, `WriteTypedAsync`, `WriteBitInWordAsync`, `ReadNamedAsync`, and `PollAsync`.
- [x] **Promote reusable address helpers**: Address normalization and formatting helpers are exposed in the public `SlmpAddress` surface for application-facing code.
- [x] **Keep protocol-specific options explicit**: `FrameType`, `CompatibilityMode`, and target routing remain first-class connection options.
- [x] **Preserve semantic atomicity by default**: Explicit `*SingleRequestAsync` and `*ChunkedAsync` helpers now separate one-request operations from opt-in multi-request transfers.
