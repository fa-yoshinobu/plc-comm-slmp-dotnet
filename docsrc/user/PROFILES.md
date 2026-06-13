# PLC profiles

The profile string selects frame type, access mode, and device ranges.

## Profiles

| `SlmpPlcProfile` enum value | Hardware | Frame | Mode | Notes |
| --- | --- | --- | --- | --- |
| `IqF` | MELSEC iQ-F / FX5 | `Frame3E` | `Legacy` | Canonical string `melsec:iq-f`; `X` and `Y` use octal notation. |
| `IqR` | MELSEC iQ-R | `Frame4E` | `Iqr` | Canonical string `melsec:iq-r`. |
| `IqL` | MELSEC iQ-L | `Frame4E` | `Iqr` | Canonical string `melsec:iq-l`; address family resolves through iQ-R rules. |
| `MxF` | MELSEC MX-F | `Frame4E` | `Iqr` | Canonical string `melsec:mx-f`. |
| `MxR` | MELSEC MX-R | `Frame4E` | `Iqr` | Canonical string `melsec:mx-r`. |
| `QCpu` | MELSEC-Q CPU | `Frame3E` | `Legacy` | Canonical string `melsec:qcpu`. |
| `LCpu` | MELSEC-L CPU | `Frame3E` | `Legacy` | Canonical string `melsec:lcpu`. |
| `QnU` | MELSEC QnU CPU | `Frame3E` | `Legacy` | Canonical string `melsec:qnu`. |
| `QnUDV` | MELSEC QnUDV CPU | `Frame3E` | `Legacy` | Canonical string `melsec:qnudv`. |

## How to select

```csharp
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR);
```

## Profile-specific cautions

| Profile | Caution |
| --- | --- |
| `IqF` | Frame 3E, legacy mode. `DX` and `DY` are not valid. `X` and `Y` use octal notation. |
| `IqR` / `IqL` | Frame 4E, iQ-R mode. `IqL` uses iQ-L device-range rules. |
| `MxF` / `MxR` | Frame 4E, iQ-R mode. |
| `QCpu` / `LCpu` / `QnU` / `QnUDV` | Frame 3E, legacy mode. |
