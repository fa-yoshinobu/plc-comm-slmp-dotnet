# TODO

This file tracks active follow-up items for the SLMP .NET library.

## 1. Active Follow-Up

- [ ] **Extended Specification live coverage expansion**: Run the expanded
  `extendeddevice-coverage` sweep across the remaining PLC-family and transport
  matrix. Keep OK/NG rows visible in the generated report. QnUDV has no `HG`;
  QnUDV `U0\G10` read-only was live-checked on 2026-05-15 against `Q06UDVCPU`
  and returned `[0]` across Python, Node-RED, .NET, Rust, and C++ Minimal. QCPU
  `U0\G10` read-only was live-checked on
  2026-05-15 against `Q12HCPU` and returned `[0]` across Python, Node-RED,
  .NET, Rust, and C++ Minimal. QnU `U0\G10` read-only was live-checked on
  2026-05-15 against `Q26UDEHCPU` and returned `[0]` across the same five
  stacks.
  On the current iQ-L target, `U3E0\G...` is the valid Extended Specification
  live-coverage path; `HG` and `J` paths are not part of that PLC's executable
  coverage set.
