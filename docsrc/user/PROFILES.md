# PLC profiles

The canonical profile is the stable configuration value for PLC selection.
`SlmpPlcProfile` is the .NET API selector used by the client.

For cross-profile capability and device-range details, see the [SLMP Profile Reference](https://fa-yoshinobu.github.io/plc-comm-docs-site/slmp/profile-reference/).

## Profiles

| Canonical profile | Human label | .NET selector | Frame | Mode | Notes |
| --- | --- | --- | --- | --- | --- |
| `melsec:iq-f` | MELSEC iQ-F | `SlmpPlcProfile.IqF` | `Frame3E` | `Legacy` | `X` and `Y` use octal notation. |
| `melsec:iq-r` | MELSEC iQ-R | `SlmpPlcProfile.IqR` | `Frame4E` | `Iqr` | Standard iQ-R profile. |
| `melsec:iq-l` | MELSEC iQ-L | `SlmpPlcProfile.IqL` | `Frame4E` | `Iqr` | Use for MELSEC iQ-L targets. |
| `melsec:mx-f` | MELSEC MX-F | `SlmpPlcProfile.MxF` | `Frame4E` | `Iqr` | MX-F profile. |
| `melsec:mx-r` | MELSEC MX-R | `SlmpPlcProfile.MxR` | `Frame4E` | `Iqr` | MX-R profile. |
| `melsec:qcpu` | MELSEC QCPU | `SlmpPlcProfile.QCpu` | `Frame3E` | `Legacy` | Legacy 3E profile. Use direct or random device commands for normal access. |
| `melsec:lcpu` | MELSEC LCPU | `SlmpPlcProfile.LCpu` | `Frame3E` | `Legacy` | Legacy 3E profile. |
| `melsec:qnu` | MELSEC QnU | `SlmpPlcProfile.QnU` | `Frame3E` | `Legacy` | Legacy 3E profile. Use direct or random device commands for normal access. |
| `melsec:qnudv` | MELSEC QnUDV | `SlmpPlcProfile.QnUDV` | `Frame3E` | `Legacy` | Legacy 3E profile. Use direct or random device commands for normal access. |

## How to select

```csharp
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR);
```
