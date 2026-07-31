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
| IPv6 endpoint | Construction fails for an IPv6 literal, or a hostname has no usable address. | SLMP TCP and UDP are IPv4-only. Use an IPv4 literal or a hostname with an IPv4 result; the first IPv4 result is used and the library never falls back to IPv6. |
| Request ordering | Multiple async callers share one connection. | The ordinary `SlmpClient` uses one FIFO queue and never overlaps complete operations. Queue wait does not consume the transaction timeout; use separate clients for independent parallel sessions. |
| Timeout after a read | `SlmpTimeoutException` is thrown and the transport is closed. | Call `OpenAsync` before another request. A read does not change PLC state, so retry only under the application's normal read-consistency policy. |
| TCP/UDP connection or I/O failure | `SlmpTransportException` is thrown for a read or pre-send failure. | Inspect `InnerException` for native diagnostics, repair the endpoint/network cause, and explicitly reconnect as required. |
| Interrupted write or remote operation | `SlmpOperationOutcomeUnknownException` is thrown. | Do not retry automatically. Inspect `Reason`, reconcile the PLC/application state through a controlled read or process-specific handshake, then explicitly reopen when it is safe. |
| Request after a retired transaction | `SlmpNotConnectedException` is thrown. | The prior timeout, cancellation, malformed response, or transport failure retired that connection generation. Call `OpenAsync`; the library never resends the prior request. |

```csharp
var first = client.ReadTypedAsync("D100", "U");
var second = client.ReadTypedAsync("D101", "U");
var values = await Task.WhenAll(first, second);
```
