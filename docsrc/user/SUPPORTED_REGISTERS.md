# Supported registers

This page lists the high-level string device families accepted by the public helper layer.

## Bit device families

| Device | Meaning | Numbering | Notes |
| --- | --- | --- | --- |
| `SM` | Special relay | Decimal | System bit device. |
| `X` | Input | Profile-aware | iQ-F uses octal notation; other profiles use hexadecimal notation. |
| `Y` | Output | Profile-aware | iQ-F uses octal notation; other profiles use hexadecimal notation. |
| `M` | Internal relay | Decimal | General internal bit device. |
| `L` | Latch relay | Decimal | Latched bit device. |
| `F` | Annunciator | Decimal | Alarm or annunciator bit device. |
| `V` | Edge relay | Decimal | Not supported by every PLC profile. |
| `B` | Link relay | Hexadecimal | Link bit device. |
| `TS` | Timer contact | Decimal | Standard timer contact. |
| `TC` | Timer coil | Decimal | Standard timer coil. |
| `LTS` | Long timer contact | Decimal | State device; writes are routed through random bit write. |
| `LTC` | Long timer coil | Decimal | State device; writes are routed through random bit write. |
| `STS` | Retentive timer contact | Decimal | Retentive timer contact. |
| `STC` | Retentive timer coil | Decimal | Retentive timer coil. |
| `LSTS` | Long retentive timer contact | Decimal | State device; writes are routed through random bit write. |
| `LSTC` | Long retentive timer coil | Decimal | State device; writes are routed through random bit write. |
| `CS` | Counter contact | Decimal | Standard counter contact. |
| `CC` | Counter coil | Decimal | Standard counter coil. |
| `LCS` | Long counter contact | Decimal | Direct bit read; writes are routed through random bit write. |
| `LCC` | Long counter coil | Decimal | Direct bit read; writes are routed through random bit write. |
| `SB` | Link special relay | Hexadecimal | Link special bit device. |
| `DX` | Direct input | Hexadecimal | Not valid for `SlmpPlcProfile.IqF`. |
| `DY` | Direct output | Hexadecimal | Not valid for `SlmpPlcProfile.IqF`. |

## Word device families

| Device | Meaning | Numbering | Notes |
| --- | --- | --- | --- |
| `SD` | Special register | Decimal | System word device. |
| `D` | Data register | Decimal | Recommended first smoke-test word family. |
| `W` | Link register | Hexadecimal | Link word device. |
| `TN` | Timer current value | Decimal | Standard timer current value. |
| `LTN` | Long timer current value | Decimal | 32-bit family; use `:D` or `:L`. |
| `STN` | Retentive timer current value | Decimal | Retentive timer current value. |
| `LSTN` | Long retentive timer current value | Decimal | 32-bit family; use `:D` or `:L`. |
| `CN` | Counter current value | Decimal | Standard counter current value. |
| `LCN` | Long counter current value | Decimal | 32-bit family; use `:D` or `:L`. |
| `SW` | Link special register | Hexadecimal | Link special word device. |
| `Z` | Index register | Decimal | 16-bit index register. |
| `LZ` | Long index register | Decimal | 32-bit family; use `:D` or `:L`. |
| `R` | File register | Decimal | File register. |
| `ZR` | File register continuous | Decimal | Continuous file register. |
| `RD` | Refresh data register | Decimal | Not supported by every PLC profile. |

## Type suffixes

| Suffix | Meaning | .NET value | Size |
| --- | --- | --- | --- |
| Plain word | `D100` | `ushort` | 1 word |
| `:U` | Unsigned 16-bit | `ushort` | 1 word |
| `:S` | Signed 16-bit | `short` | 1 word |
| `:D` | Unsigned 32-bit | `uint` | 2 words |
| `:L` | Signed 32-bit | `int` | 2 words |
| `:F` | Float32 | `float` | 2 words |
| `.n` | Bit inside word | `bool` | 1 bit from one word |

## Addressing notes

| Topic | Rule |
| --- | --- |
| Long 32-bit families | `LTN`, `LSTN`, `LCN`, and `LZ` require `:D` or `:L`; plain word access is rejected. |
| iQ-F direct devices | `DX` and `DY` are not valid for `SlmpPlcProfile.IqF`. |
| Module buffer devices | `G` and `HG` are not in the public high-level surface. Use low-level raw client methods for module buffer access. |
| Bit-in-word syntax | `.n` is valid only on word devices, for example `D50.3`. Address bit devices directly, for example `M1000`. |
| Profile-aware `X` and `Y` | Use a connected client or `SlmpAddress.Parse(text, profile)` so iQ-F octal rules are applied. |

See [PLC profiles](PROFILES.md) for the profile strings and frame modes.
