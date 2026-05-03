# TODO

This file tracks active follow-up items for the SLMP .NET library.

## 1. Active Follow-Up

- [ ] **Extended Specification live coverage expansion**: Run the expanded
  `extendeddevice-coverage` sweep across the remaining PLC-family and transport
  matrix. Keep OK/NG rows visible in the generated report. QnUDV has no `HG`;
  `U0\G10` read-only on the current QnUDV target returned `0xC070` with command
  `0x0401` subcommand `0x0080`. On the current iQ-L target, `U3E0\G...` is the
  valid Extended Specification live-coverage path; `HG` and `J` paths are not
  part of that PLC's executable coverage set.
