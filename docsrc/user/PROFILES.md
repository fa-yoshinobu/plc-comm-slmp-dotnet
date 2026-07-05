# PLC profiles

The canonical profile is the stable configuration value for PLC selection.
`SlmpPlcProfile` is the .NET API selector used by the client.

For cross-profile capability and device-range details, see the [SLMP Profile Reference](https://fa-yoshinobu.github.io/plc-comm-docs-site/slmp/profile-reference/).

## Profiles

| Canonical profile | Human label | .NET selector | Frame | Mode | Notes |
| --- | --- | --- | --- | --- | --- |
| `melsec:iq-f` | MELSEC iQ-F | `SlmpPlcProfile.IqF` | `Frame3E` | `Legacy` | `X` and `Y` use octal notation. |
| `melsec:iq-r` | MELSEC iQ-R | `SlmpPlcProfile.IqR` | `Frame4E` | `Iqr` | Standard iQ-R profile. |
| `melsec:iq-r:rj71en71` | MELSEC iQ-R via RJ71EN71 | `SlmpPlcProfile.IqRRj71En71` | `Frame4E` | `Iqr` | Ethernet-unit profile using iQ-R compatibility. |
| `melsec:iq-l` | MELSEC iQ-L | `SlmpPlcProfile.IqL` | `Frame4E` | `Iqr` | Use for MELSEC iQ-L targets. |
| `melsec:mx-f` | MELSEC MX-F | `SlmpPlcProfile.MxF` | `Frame4E` | `Iqr` | MX-F profile. |
| `melsec:mx-r` | MELSEC MX-R | `SlmpPlcProfile.MxR` | `Frame4E` | `Iqr` | MX-R profile. |
| `melsec:lcpu` | MELSEC LCPU | `SlmpPlcProfile.LCpu` | `Frame3E` | `Legacy` | Legacy 3E profile. |
| `melsec:lcpu:lj71e71-100` | MELSEC LCPU via LJ71E71-100 | `SlmpPlcProfile.LCpuLj71E71100` | `Frame4E` | `Legacy` | Ethernet-unit profile. |
| `melsec:qnu` | MELSEC QnU | `SlmpPlcProfile.QnU` | `Frame3E` | `Legacy` | Legacy 3E profile. Use direct or random device commands for normal access. |
| `melsec:qnu:qj71e71-100` | MELSEC QnU via QJ71E71-100 | `SlmpPlcProfile.QnUQj71E71100` | `Frame4E` | `Legacy` | Ethernet-unit profile. |
| `melsec:qnudv` | MELSEC QnUDV | `SlmpPlcProfile.QnUDV` | `Frame3E` | `Legacy` | Legacy 3E profile. Use direct or random device commands for normal access. |
| `melsec:qnudv:qj71e71-100` | MELSEC QnUDV via QJ71E71-100 | `SlmpPlcProfile.QnUDVQj71E71100` | `Frame4E` | `Legacy` | Ethernet-unit profile. |
| `melsec:qcpu:qj71e71-100` | MELSEC QCPU via QJ71E71-100 | `SlmpPlcProfile.QCpuQj71E71100` | `Frame4E` | `Legacy` | Ethernet-unit profile. |

`melsec:qcpu` is base-only and remains as an internal profile for QCPU address and device-range behavior, but it is not a selectable connection profile. Use `melsec:qcpu:qj71e71-100` for QCPU Ethernet-unit communication.

## How to select

```csharp
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR);
```
