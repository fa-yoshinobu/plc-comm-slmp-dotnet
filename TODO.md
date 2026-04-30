# TODO

This file tracks active follow-up items for the SLMP .NET library.

## 1. Active Follow-Up

- **Extended Specification live coverage expansion**
  The current capture-aligned implementation is working on the validated paths,
  but broader address-range, transport, and PLC-family coverage is still open.
  QnUDV has no `HG`; `U0\G10` read-only on the current QnUDV target returned
  `0xC070` with command `0x0401` subcommand `0x0080`.

- **Mixed block write root cause**
  The practical fallback is implemented, but the reason some validated PLC
  paths reject the first one-request mixed `1406` write with `0xC05B` is still
  not fully explained. On the current QnUDV target, word-only, bit-only, and
  mixed `1406` block writes returned `0xC059`, so this appears to be block-write
  command support rather than a mixed-only rejection on that target.

## 2. Practical Limits

- ASCII mode is intentionally out of scope.

## 3. Completed Recently

- [x] **Resolve Q-series runtime device ranges**: QCPU/LCPU/QnU/QnUDV `ZR` ranges are selected by probing readable addresses, `R` follows the probed `ZR` count capped at `R32767`, QCPU `Z` is selected by probing `Z15`, and LCPU/QnU/QnUDV `Z` is fixed at 20 points.
- [x] **Stabilize the shared high-level contract**: The public surface is aligned with the sibling .NET libraries around `OpenAndConnectAsync`, `ReadTypedAsync`, `WriteTypedAsync`, `WriteBitInWordAsync`, `ReadNamedAsync`, and `PollAsync`.
- [x] **Promote reusable address helpers**: Address normalization and formatting helpers are exposed in the public `SlmpAddress` surface for application-facing code.
- [x] **Keep protocol-specific options explicit**: `FrameType`, `CompatibilityMode`, and target routing remain first-class connection options.
- [x] **Preserve semantic atomicity by default**: Explicit `*SingleRequestAsync` and `*ChunkedAsync` helpers now separate one-request operations from opt-in multi-request transfers.
