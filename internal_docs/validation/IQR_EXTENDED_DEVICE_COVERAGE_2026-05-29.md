# iQ-R Extended Device Coverage 2026-05-29

- Target: `192.168.250.100:1025` TCP and `192.168.250.100:1027` UDP
- PLC: Mitsubishi `R08CPU`
- Profile: `--series iqr --frame-type 4e`
- Scope: Extended Specification write-check coverage
- Remote password: enabled on the port; validation used
  `SLMP_REMOTE_PASSWORD` to unlock before the sweep and lock again before exit.

## Command

```bash
SLMP_REMOTE_PASSWORD=<password> \
dotnet run --project samples/PlcComm.Slmp.Cli -- extendeddevice-coverage \
  --host 192.168.250.100 \
  --port 1025 \
  --transport tcp \
  --series iqr \
  --frame-type 4e \
  --device 'U3E0\G10' \
  --points 1 \
  --points 2 \
  --write-check

SLMP_REMOTE_PASSWORD=<password> \
dotnet run --project samples/PlcComm.Slmp.Cli -- extendeddevice-coverage \
  --host 192.168.250.100 \
  --port 1027 \
  --transport udp \
  --series iqr \
  --frame-type 4e \
  --device 'U3E0\G10' \
  --points 1 \
  --points 2 \
  --write-check
```

## Result

| Target | Transport | Device | Points | Direct | Result | Detail |
|---|---|---|---:|---:|---|---|
| `SELF` | TCP | `U3E0\G10` | 1 | `0xF8` | OK | before `0x0000`, wrote/read back `0x001E`, restore OK |
| `SELF` | TCP | `U3E0\G10` | 2 | `0xF8` | OK | before `0x0000, 0x0000`, wrote/read back `0x001E, 0x001F`, restore OK |
| `SELF` | UDP | `U3E0\G10` | 1 | `0xF8` | OK | before `0x0000`, wrote/read back `0x001E`, restore OK |
| `SELF` | UDP | `U3E0\G10` | 2 | `0xF8` | OK | before `0x0000, 0x0000`, wrote/read back `0x001E, 0x001F`, restore OK |

`HG` requires a multi-CPU coverage target and is not part of this R08CPU
single-CPU executable coverage set. `J` paths were not tested because no routed
network target was provided.
