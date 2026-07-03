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
| `melsec:qcpu` | MELSEC QCPU | `SlmpPlcProfile.QCpu` | `Frame3E` | `Legacy` | Legacy 3E profile. Read Block (`0x0406`) and Write Block (`0x1406`) are rejected; use direct or random device commands. |
| `melsec:lcpu` | MELSEC LCPU | `SlmpPlcProfile.LCpu` | `Frame3E` | `Legacy` | Legacy 3E profile. |
| `melsec:qnu` | MELSEC QnU | `SlmpPlcProfile.QnU` | `Frame3E` | `Legacy` | Legacy 3E profile. Read Block (`0x0406`) and Write Block (`0x1406`) are rejected; use direct or random device commands. |
| `melsec:qnudv` | MELSEC QnUDV | `SlmpPlcProfile.QnUDV` | `Frame3E` | `Legacy` | Legacy 3E profile. Read Block (`0x0406`) and Write Block (`0x1406`) are rejected; use direct or random device commands. |

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
| `melsec:qcpu` | MELSEC QCPU | Frame 3E, legacy mode. Strict profile rejects block commands `0x0406` / `0x1406`. |
| `melsec:lcpu` | MELSEC LCPU | Frame 3E, legacy mode. |
| `melsec:qnu` | MELSEC QnU | Frame 3E, legacy mode. Strict profile rejects block commands `0x0406` / `0x1406`. |
| `melsec:qnudv` | MELSEC QnUDV | Frame 3E, legacy mode. Strict profile rejects Read Type Name (`0x0101`) and block commands `0x0406` / `0x1406`; disabling strict profile sends them and lets the PLC respond. |
