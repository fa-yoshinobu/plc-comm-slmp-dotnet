# Gotchas

Use this page only for library-specific caveats.

Shared SLMP setup, profile, point-limit, and end-code symptoms live in the shared
[SLMP Troubleshooting & Codes](https://fa-yoshinobu.github.io/plc-comm-docs-site/plc-setup/slmp/troubleshooting-codes/)
page. For profile limits and device availability, use the shared
[SLMP Profile Parameters](https://fa-yoshinobu.github.io/plc-comm-docs-site/slmp/profile-reference/parameters/)
page.

## Current library-specific caveats

| Area | Symptom | Guidance |
| --- | --- | --- |
| Request ordering | Multiple async callers using one raw `SlmpClient` can produce overlapping requests or inconsistent responses. | A raw SLMP connection is one ordered frame stream. Use the queued client returned by `SlmpClientFactory.OpenAndConnectAsync`. |

```csharp
var first = client.ReadTypedAsync("D100", "U");
var second = client.ReadTypedAsync("D101", "U");
var values = await Task.WhenAll(first, second);
```
