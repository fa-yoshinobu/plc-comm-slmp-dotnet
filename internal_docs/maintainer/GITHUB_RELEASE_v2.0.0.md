# v2.0.0

## BREAKING

Short `SlmpModuleIo` aliases were removed in favor of canonical module I/O names.

| Old name | New name |
| --- | --- |
| `ControlCpu`, `ConnectedCpu`, `Default` | `OwnStation` |
| `ActiveCpu` | `ControlSystemCpu` |
| `StandbyCpu` | `StandbySystemCpu` |
| `TypeACpu` | `SystemACpu` |
| `TypeBCpu` | `SystemBCpu` |
| `Cpu1` to `Cpu4` | `MultipleCpu1` to `MultipleCpu4` |

## Package Name

| Registry | Package |
| --- | --- |
| NuGet | `PlcComm.Slmp` unchanged |

## Highlights

- Version metadata bumped to 2.0.0.
- SLMP profile fixture synced to `plc-comm-slmp-profiles` v1.2.2.
- README links to the plc-comm package matrix.

Package matrix: https://fa-yoshinobu.github.io/plc-comm-docs-site/package-matrix/
