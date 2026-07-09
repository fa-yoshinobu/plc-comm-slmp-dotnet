# PLC profiles

The canonical profile is the stable configuration value for PLC selection.
`SlmpPlcProfile` is the .NET API selector used by the client.
Use `SlmpPlcProfiles.GetDisplayName(profile)` for UI labels. Store the
canonical profile string from `SlmpPlcProfiles.ToCanonicalString(profile)`, not
the display name. Use `SlmpPlcProfiles.AvailableProfiles()` when a selector
should list only profiles accepted by the standard connection helpers.

For cross-profile capability and device-range details, see the [SLMP Profile Reference](https://fa-yoshinobu.github.io/plc-comm-docs-site/slmp/profile-reference/).

## Profiles

| Canonical profile | Display name | .NET selector | Frame | Mode | Notes |
| --- | --- | --- | --- | --- | --- |
| `melsec:iq-f` | MELSEC iQ-F (built-in) | `SlmpPlcProfile.IqF` | `Frame3E` | `Legacy` | `X` and `Y` use octal notation. |
| `melsec:iq-r` | MELSEC iQ-R (built-in) | `SlmpPlcProfile.IqR` | `Frame4E` | `Iqr` | Standard iQ-R profile. |
| `melsec:iq-r:rj71en71` | MELSEC iQ-R (RJ71EN71) | `SlmpPlcProfile.IqRRj71En71` | `Frame4E` | `Iqr` | Ethernet-unit profile using iQ-R compatibility. |
| `melsec:iq-l` | MELSEC iQ-L (built-in) | `SlmpPlcProfile.IqL` | `Frame4E` | `Iqr` | Use for MELSEC iQ-L targets. |
| `melsec:mx-f` | MELSEC MX-F (built-in) | `SlmpPlcProfile.MxF` | `Frame4E` | `Iqr` | MX-F profile. |
| `melsec:mx-r` | MELSEC MX-R (built-in) | `SlmpPlcProfile.MxR` | `Frame4E` | `Iqr` | MX-R profile. |
| `melsec:lcpu` | MELSEC-L (built-in) | `SlmpPlcProfile.LCpu` | `Frame3E` | `Legacy` | Legacy 3E profile. |
| `melsec:lcpu:lj71e71-100` | MELSEC-L (LJ71E71-100) | `SlmpPlcProfile.LCpuLj71E71100` | `Frame4E` | `Legacy` | Ethernet-unit profile. |
| `melsec:qnu` | MELSEC QnU (built-in) | `SlmpPlcProfile.QnU` | `Frame3E` | `Legacy` | Legacy 3E profile. Use direct or random device commands for normal access. |
| `melsec:qnu:qj71e71-100` | MELSEC QnU (QJ71E71-100) | `SlmpPlcProfile.QnUQj71E71100` | `Frame4E` | `Legacy` | Ethernet-unit profile. |
| `melsec:qnudv` | MELSEC QnUDV (built-in) | `SlmpPlcProfile.QnUDV` | `Frame3E` | `Legacy` | Legacy 3E profile. Use direct or random device commands for normal access. |
| `melsec:qnudv:qj71e71-100` | MELSEC QnUDV (QJ71E71-100) | `SlmpPlcProfile.QnUDVQj71E71100` | `Frame4E` | `Legacy` | Ethernet-unit profile. |
| `melsec:qcpu:qj71e71-100` | MELSEC-Q (QJ71E71-100) | `SlmpPlcProfile.QCpuQj71E71100` | `Frame4E` | `Legacy` | Ethernet-unit profile. |

`melsec:qcpu` is base-only and remains as an internal profile for QCPU address and device-range behavior, but it is not a selectable connection profile. Use `melsec:qcpu:qj71e71-100` for QCPU Ethernet-unit communication.

## How to select

```csharp
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR);
```
