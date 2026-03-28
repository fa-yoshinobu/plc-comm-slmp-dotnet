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

## 2. Practical Limits

- ASCII mode is intentionally out of scope.

