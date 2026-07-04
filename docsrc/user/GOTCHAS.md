# Gotchas

Use this page as a short symptom index. For PLC response codes, use the shared
[SLMP Troubleshooting & End Codes](https://fa-yoshinobu.github.io/plc-comm-docs-site/slmp/profile-reference/troubleshooting-end-codes/)
page. For profile limits and device availability, use the shared
[SLMP Profile Parameters](https://fa-yoshinobu.github.io/plc-comm-docs-site/slmp/profile-reference/parameters/)
page.
For PLC-side Ethernet settings, use the shared
[MELSEC SLMP PLC Setup Guide](https://fa-yoshinobu.github.io/plc-comm-docs-site/plc-setup/slmp/).
Check Binary communication data code, port/open settings, and RUN-time write permission there before debugging application code.

## Connection fails or times out

| Symptom | Root cause | Fix |
| --- | --- | --- |
| `OpenAndConnectAsync` cannot open the PLC connection. | Host, port, transport, PLC Ethernet setting, or network route is wrong. | Check the PLC setup guide first. Built-in Ethernet examples use TCP `192.168.250.100:1025`; use UDP only when the PLC port is configured for UDP. |

## Connection opens but every request returns an end code

| Symptom | Root cause | Fix |
| --- | --- | --- |
| Simple reads such as `D100:U` connect but fail with an SLMP end code. | The selected `SlmpPlcProfile` does not match the PLC, or the PLC port data code does not match the library request format. | Select the canonical profile for the PLC and confirm the PLC Ethernet port is configured for Binary SLMP. Use the shared end-code page for codes such as `C050`, `C059`, and `4031`. |

## Reads work but writes fail

| Symptom | Root cause | Fix |
| --- | --- | --- |
| Reads work, but writes are rejected. | PLC-side write permission during RUN, remote password state, or profile write policy blocks the write. | Check RUN-time write permission in the PLC setup guide and the selected profile's write policy. `S` is read-only except on iQ-F profiles. |

## Large requests fail with point-limit end codes

| Symptom | Root cause | Fix |
| --- | --- | --- |
| A large read, write, random request, or monitor request fails with `C051`, `C052`, `C053`, or `C054`. | The request exceeds the selected profile's per-request point limit. | Split the request or use the chunked helper. Check the shared profile parameter table for the limit. |

```csharp
ushort[] words = await client.ReadWordsChunkedAsync("D1000", 2000, maxWordsPerRequest: 480);
```

## Block commands are rejected on Q/L profiles

| Symptom | Root cause | Fix |
| --- | --- | --- |
| `ReadBlockAsync` or `WriteBlockAsync` fails for `melsec:qcpu`, `melsec:qnu`, `melsec:qnudv`, or `melsec:lcpu`. | These profiles do not use block commands for normal high-level access. | Use normal direct/random read and write helpers. Disable strict profile only for deliberate compatibility investigation. |

## Mixed word and bit write fails

| Symptom | Root cause | Fix |
| --- | --- | --- |
| One write containing word values and bit values fails. | Some PLC paths reject mixed word and bit block writes. | Send word writes and bit writes as separate calls. |

```csharp
await client.WriteNamedAsync(new Dictionary<string, object> { ["D9000:U"] = (ushort)1234 });
await client.WriteNamedAsync(new Dictionary<string, object> { ["M9000:BIT"] = true });
```

## iQ-F X/Y or DX/DY addresses fail

| Symptom | Root cause | Fix |
| --- | --- | --- |
| `X`/`Y` points look shifted, or `DX`/`DY` is rejected on iQ-F. | iQ-F uses octal text for `X`/`Y`, and the iQ-F profile does not support `DX`/`DY`. | Parse and format addresses with `SlmpPlcProfile.IqF`; use `X` and `Y` on iQ-F. |

```csharp
var parsed = SlmpAddress.Parse("X20", SlmpPlcProfile.IqF);
```

## Long timer/counter/index values look wrong

| Symptom | Root cause | Fix |
| --- | --- | --- |
| `LTN`, `LSTN`, `LCN`, or `LZ` looks truncated or shifted. | These current-value families are 32-bit values. | Use `:D` or `:L` in named addresses, or pass `D`/`L` to typed helpers. |
| `LCS` or `LCC` behaves unlike a word value. | Long counter state devices are bits. | Read or write them as `BIT`. |

```csharp
var values = await client.ReadNamedAsync(["LTN0:D", "LSTN0:L", "LCN0:D", "LZ0:L", "LCS0:BIT"]);
```

## G/HG fails as a normal address

| Symptom | Root cause | Fix |
| --- | --- | --- |
| `G` or `HG` fails in high-level typed or named access. | Module buffer memory is not a standalone normal device route. | Use qualified routed forms such as `U3\G100` through the low-level extended/module-buffer APIs. `HG` CPU-buffer access is profile-specific. |

## Missing or unspecified profile is rejected

| Symptom | Root cause | Fix |
| --- | --- | --- |
| `SlmpPlcProfile.Unspecified` fails before any PLC request. | There is no safe default PLC profile. | Select a concrete profile such as `SlmpPlcProfile.IqR`; the library does not auto-detect the model. |

## Concurrent callers interleave responses

| Symptom | Root cause | Fix |
| --- | --- | --- |
| Multiple async callers using one raw `SlmpClient` produce overlapping requests or inconsistent responses. | A raw SLMP connection is one ordered frame stream. | Use the queued client returned by `SlmpClientFactory.OpenAndConnectAsync`. |

```csharp
var first = client.ReadTypedAsync("D100", "U");
var second = client.ReadTypedAsync("D101", "U");
var values = await Task.WhenAll(first, second);
```
