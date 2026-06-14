# PLC profiles

The canonical profile is the stable configuration value for PLC selection.
`SlmpPlcProfile` is the .NET API selector used by the client.

## Profiles

| Canonical profile | Human label | .NET selector | Frame | Mode | Notes |
| --- | --- | --- | --- | --- | --- |
| `melsec:iq-f` | MELSEC iQ-F | `SlmpPlcProfile.IqF` | `Frame3E` | `Legacy` | `X` and `Y` use octal notation. |
| `melsec:iq-r` | MELSEC iQ-R | `SlmpPlcProfile.IqR` | `Frame4E` | `Iqr` | Standard iQ-R profile. |
| `melsec:iq-l` | MELSEC iQ-L | `SlmpPlcProfile.IqL` | `Frame4E` | `Iqr` | Use for MELSEC iQ-L targets. |
| `melsec:mx-f` | MELSEC MX-F | `SlmpPlcProfile.MxF` | `Frame4E` | `Iqr` | MX-F profile. |
| `melsec:mx-r` | MELSEC MX-R | `SlmpPlcProfile.MxR` | `Frame4E` | `Iqr` | MX-R profile. |
| `melsec:qcpu` | MELSEC QCPU | `SlmpPlcProfile.QCpu` | `Frame3E` | `Legacy` | Legacy 3E profile. |
| `melsec:lcpu` | MELSEC LCPU | `SlmpPlcProfile.LCpu` | `Frame3E` | `Legacy` | Legacy 3E profile. |
| `melsec:qnu` | MELSEC QnU | `SlmpPlcProfile.QnU` | `Frame3E` | `Legacy` | Legacy 3E profile. |
| `melsec:qnudv` | MELSEC QnUDV | `SlmpPlcProfile.QnUDV` | `Frame3E` | `Legacy` | Legacy 3E profile. |

## How to select

```csharp
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR);
```

## Profile-specific cautions

| Canonical profile | Human label | Caution |
| --- | --- | --- |
| `melsec:iq-f` | MELSEC iQ-F | Frame 3E, legacy mode. `DX` and `DY` are not valid. `X` and `Y` use octal notation. |
| `melsec:iq-r` | MELSEC iQ-R | Frame 4E, iQ-R mode. |
| `melsec:iq-l` | MELSEC iQ-L | Frame 4E, iQ-R mode. |
| `melsec:mx-f` | MELSEC MX-F | Frame 4E, iQ-R mode. |
| `melsec:mx-r` | MELSEC MX-R | Frame 4E, iQ-R mode. |
| `melsec:qcpu` | MELSEC QCPU | Frame 3E, legacy mode. |
| `melsec:lcpu` | MELSEC LCPU | Frame 3E, legacy mode. |
| `melsec:qnu` | MELSEC QnU | Frame 3E, legacy mode. |
| `melsec:qnudv` | MELSEC QnUDV | Frame 3E, legacy mode. |
