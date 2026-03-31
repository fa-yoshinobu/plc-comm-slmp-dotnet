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

## 3. Cross-Stack API Alignment

- [ ] **Stabilize the shared high-level contract**: Keep the public surface intentionally aligned with the sibling .NET libraries around `OpenAndConnectAsync`, `ReadTypedAsync`, `WriteTypedAsync`, `WriteBitInWordAsync`, `ReadNamedAsync`, and `PollAsync`.
- [ ] **Promote reusable address helpers**: Review whether the device/address normalization and formatting helpers should be exposed in a public, application-facing form so UI and adapter layers do not need to duplicate string handling.
- [ ] **Keep protocol-specific options explicit**: Preserve explicit `FrameType`, `CompatibilityMode`, and target-routing settings as first-class options instead of reintroducing automatic profile selection behavior.
- [ ] **Preserve semantic atomicity by default**: Do not silently split reads or writes that users would reasonably treat as one logical value or one logical block. Protocol-defined boundaries are acceptable, but fallback retries that change semantics should be opt-in and explicitly named.
- [ ] **Preserve semantic atomicity by default**: Do not silently split reads or writes that users would reasonably treat as one logical value or one logical block. Protocol-defined boundaries are acceptable, but fallback retries that change semantics should be opt-in and explicitly named.

## 3. Cross-Stack API Alignment

- [ ] **Stabilize the shared high-level contract**: Keep the public surface intentionally aligned with the sibling .NET libraries around `OpenAndConnectAsync`, `ReadTypedAsync`, `WriteTypedAsync`, `WriteBitInWordAsync`, `ReadNamedAsync`, and `PollAsync`.
- [ ] **Promote reusable address helpers**: Review whether the device/address normalization and formatting helpers should be exposed in a public, application-facing form so UI and adapter layers do not need to duplicate string handling.
- [ ] **Keep protocol-specific options explicit**: Preserve explicit `FrameType`, `CompatibilityMode`, and target-routing settings as first-class options instead of reintroducing automatic profile selection behavior.

