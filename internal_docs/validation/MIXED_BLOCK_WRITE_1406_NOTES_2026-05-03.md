# Mixed Block Write 1406 Notes 2026-05-03

> **RESOLVED 2026-06-12:** the mixed/multi-block write failures were caused by
> a client payload layout bug, not by a protocol-side ban on ordinary mixed
> word+bit `0x1406` requests. Fixed in
> `src/PlcComm.Slmp/SlmpClient.cs::WriteBlockAsync`.

Scope:

- Command: `DeviceWriteBlock` (`0x1406`)
- iQ-R/iQ-L style subcommand: `0x0002`
- Q/L/FX 3E-style subcommand: `0x0000`
- Focus: one-request mixed word-block + bit-block write behavior

## Root Cause

The old implementation emitted:

```text
[word block count][bit block count][all block specs][all word data][all bit data]
```

The SLMP manual requires each block's data to be inline after that block's own
device spec and point count:

```text
[word block count][bit block count][block spec + point count + block data]...
```

Single-block requests appear to work in either layout because there is only one
spec/data pair. Mixed word+bit and multi-block requests make the PLC consume
payload bytes as the wrong field and return target-dependent end codes.

Manual references used for the fix:

- English local manual: `D:\_github_plc\sh080956engn.pdf`
  - PDF page 33: command list and subcommand overview
  - PDF pages 76-78: `Write Block (command: 1406)` request and example
- Japanese local manual: `D:\_github_plc\sh080931r.pdf`
  - PDF page 32: command list and subcommand overview
  - PDF pages 75-77: `Write Block(コマンド: 1406)` request and example

## Live Re-Validation Summary

Fixed Python/Rust/.NET clients were re-verified live on 2026-06-12. The same
manual-conformant layout also passes cross-verify block cases for C++ minimal
and Node-RED.

| Target/path | Old layout result | Fixed layout result |
| --- | --- | --- |
| R08CPU built-in Ethernet | `0xC05B` mixed, `0xC051` multi-word | `0x0000`, readback match |
| L02SCPU via LJ71E71-100 | `0xC056` | `0x0000`, readback match |
| Q06UDVCPU via QJ71E71-100 | `0xC056` | `0x0000`, readback match |
| L16HCPU built-in Ethernet | `0xC05B` mixed, `0xC051` multi-word | `0x0000`, readback match |
| FX5UC-32MT/D, 3E path | `0xC061` | `0x0000`, readback match |
| QnUDV built-in Ethernet | `0xC059` for block commands | still `0xC059`; use non-block commands |

`0xC059` on the QnUDV built-in Ethernet path is separate from this bug: that
path rejects block commands themselves. Use `0x0401`/`0x1401` or random
read/write commands for that route.

## Current Library Behavior

- .NET encodes `0x1406` blocks in manual order in `WriteBlockAsync`.
- Automatic mixed-write retry is not part of `SlmpBlockWriteOptions`.
- If the PLC rejects one mixed `0x1406` request, the original end code is
  returned unchanged.
- If a caller intentionally wants two block requests, use
  `new SlmpBlockWriteOptions(SplitMixedBlocks: true)`.
- Non-live tests verify that `0xC05B` and `0xC056` do not trigger automatic
  split retry.
