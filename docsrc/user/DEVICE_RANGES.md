# Device Range Catalog

Device range rule definitions are maintained in the shared SLMP profile reference, not in this .NET library documentation.

- [SLMP profile comparison](https://fa-yoshinobu.github.io/plc-comm-docs-site/slmp/profile-reference/profile-comparison/)
- [SLMP device range rules](https://fa-yoshinobu.github.io/plc-comm-docs-site/slmp/profile-reference/device-range-rules/)

This page only describes the .NET API surface.

## Purpose

`ReadDeviceRangeCatalogAsync()` reads live device range bounds after you connect with an explicit `SlmpPlcProfile`.

The catalog is a connected diagnostics and application-layer validation aid. Normal read/write APIs do not use device-range upper bounds to decide whether an address can be sent. Applications that need PLC-specific range validation should read the catalog and apply that policy outside the transport operation.

## Profile Policy

The client does not infer `SlmpPlcProfile` from `ReadTypeNameAsync()`, model text, or model code. Choose the profile in your application, configuration UI, or operator workflow.

## API

Both `SlmpClient` and `QueuedSlmpClient` expose the standard connected-profile catalog method.

| Method | Use |
| --- | --- |
| `ReadDeviceRangeCatalogAsync(CancellationToken cancellationToken = default)` | Read the catalog for the client profile selected in `SlmpConnectionOptions`. |

Example:

```csharp
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqF)
{
    Port = 1025,
};

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
var catalog = await client.ReadDeviceRangeCatalogAsync();

Console.WriteLine($"selected={catalog.Model} -> {catalog.PlcProfile}");
foreach (var entry in catalog.Entries)
{
    if (!entry.Supported) continue;
    Console.WriteLine(
        $"{entry.Device}: points={entry.PointCount}, range={entry.AddressRange}, source={entry.Source}");
}
```

Returned types:

- `SlmpDeviceRangeCatalog`
- `SlmpDeviceRangeEntry`
- `SlmpPlcProfile`
- `SlmpDeviceRangeCategory`
- `SlmpDeviceRangeNotation`

`SlmpDeviceRangeEntry.Notation` uses `Base10`, `Base8`, or `Base16` for the public address text this library expects.

If you need an explicit low-level override, both clients also expose another overload.

| Method | Use |
| --- | --- |
| `ReadDeviceRangeCatalogAsync(SlmpPlcProfile plcProfile, CancellationToken cancellationToken = default)` | Read a catalog for a canonical profile other than the client's configured profile. |

That overload is not the standard application route.
