# iQ-L Extended Device Coverage 2026-05-03

- Target: `192.168.250.100:1025` TCP
- CLI options: `--series iqr --frame-type 4e`
- Scope: read-only Extended Specification coverage

## Result

| Device | Points | Direct | Result | Detail |
|---|---:|---:|---|---|
| `U3E0\G10` | 1 | `0xF8` | OK | read returned `0x0000` |
| `U3E0\G10` | 2 | `0xF8` | OK | read returned `0x0000, 0x0000` |

`HG` and `J` paths are not executable coverage targets on this PLC. Do not
count them as remaining iQ-L Extended Specification coverage for this target.
