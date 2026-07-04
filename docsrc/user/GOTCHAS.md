# Gotchas

Use this page as a short symptom index. For PLC response codes, use the shared
[SLMP Troubleshooting & End Codes](https://fa-yoshinobu.github.io/plc-comm-docs-site/plc-setup/slmp/troubleshooting-end-codes/)
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

## Large requests fail with point-limit end codes

| Symptom | Root cause | Fix |
| --- | --- | --- |
| A large read, write, random request, or monitor request fails with `C051`, `C052`, `C053`, or `C054`. | The request exceeds the selected profile's per-request point limit. | Split the request or use the chunked helper. Check the shared profile parameter table for the limit. |

```csharp
ushort[] words = await client.ReadWordsChunkedAsync("D1000", 2000, maxWordsPerRequest: 480);
```

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

## Concurrent callers interleave responses

| Symptom | Root cause | Fix |
| --- | --- | --- |
| Multiple async callers using one raw `SlmpClient` produce overlapping requests or inconsistent responses. | A raw SLMP connection is one ordered frame stream. | Use the queued client returned by `SlmpClientFactory.OpenAndConnectAsync`. |

```csharp
var first = client.ReadTypedAsync("D100", "U");
var second = client.ReadTypedAsync("D101", "U");
var values = await Task.WhenAll(first, second);
```
