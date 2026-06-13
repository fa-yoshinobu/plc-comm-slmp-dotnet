# TODO

This file tracks active follow-up items for the SLMP .NET library.

## 1. Active Follow-Up

- [x] **No active protocol follow-up**: Extended Specification `G/HG` live
  confirmation is no longer tracked as an open .NET item. The current
  `R120PCPU` target at `192.168.250.101:1025` passed read-only checks for
  `U1\G0` and `U3E0\HG0` through `U3E3\HG0`.

## 2. Cross-Stack API Alignment

- [x] **Finalize `PlcProfile` naming alignment**: The public PLC selector is `SlmpPlcProfile`, frame type and compatibility mode are derived from that profile on the standard route, and profile text accepts only canonical lowercase values such as `melsec:iq-r`. Short labels such as `iq-r`, `iqr`, `q`, `l`, and `qnudvcpu` are rejected.
