# TODO

This file tracks active follow-up items for the SLMP .NET library.

## 1. Active Follow-Up

- [ ] **Extended Specification live coverage expansion**: The current capture-aligned implementation is working on the validated paths,
  but broader address-range, transport, and PLC-family coverage is still open.
  QnUDV has no `HG`; `U0\G10` read-only on the current QnUDV target returned
  `0xC070` with command `0x0401` subcommand `0x0080`.

- [ ] **Mixed block write root cause**: The practical fallback is implemented, but the reason some validated PLC
  paths reject the first one-request mixed `1406` write with `0xC05B` is still
  not fully explained. On the current QnUDV target, word-only, bit-only, and
  mixed `1406` block writes returned `0xC059`, so this appears to be block-write
  command support rather than a mixed-only rejection on that target.

## 2. Practical Limits

- [x] **ASCII mode out of scope**: ASCII mode is intentionally out of scope.
