# Device Range Catalog

This document replaces `plc_device_range_registers - base.csv`.

This catalog is a connected-diagnostics catalog, not an editor-side address
range validator. It is only accurate after a PLC profile has been selected and
the client can read the family-specific registers or apply a fixed family rule.
Node-RED uses the supported/unsupported device-code part of this table to avoid
sending clearly unsupported device codes for a selected family, but it does not
pre-check PLC model-specific address upper bounds.

The library now owns the device-range rules in source code and reads live upper
bounds from the PLC itself after the caller chooses the PLC profile:

1. caller selects `SlmpPlcProfile`
2. read the family-specific `SD` register window
3. build a `SlmpDeviceRangeCatalog` with point counts and 0-based address ranges

The standard high-level API does not auto-resolve the family through
`ReadTypeNameAsync()`. `ReadDeviceRangeCatalogAsync()` reads the configured
family only.

`PointCount` is the usable point count reported by the PLC or by a fixed family
rule. `UpperBound` is the inclusive last address, so for 0-based devices it is
`PointCount - 1`. `AddressRange` is preformatted text such as `X000-X1FF`.

`Supported = false` means the family does not expose that device.
`Supported = true` with `PointCount = null` means the PLC profile supports the
device but does not publish a finite bound register.
Family-aware string parsing also rejects `DX` and `DY` for `SlmpPlcProfile.IqF`
before transport.

## API

Both `SlmpClient` and `QueuedSlmpClient` expose:

```csharp
Task<SlmpDeviceRangeCatalog> ReadDeviceRangeCatalogAsync(
    CancellationToken cancellationToken = default)
```

Example:

```csharp
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqF)
{
    Port = 1025,
};

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
var catalog = await client.ReadDeviceRangeCatalogAsync();

Console.WriteLine($"selected={catalog.Model} -> {catalog.Family}");
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
- `SlmpDeviceRangeFamily`
- `SlmpDeviceRangeCategory`
- `SlmpDeviceRangeNotation`

`SlmpDeviceRangeEntry.Notation` uses `Base10`, `Base8`, or `Base16` for the public
address text this library expects.

If you need an explicit low-level override, both clients also expose:

```csharp
Task<SlmpDeviceRangeCatalog> ReadDeviceRangeCatalogAsync(
    SlmpDeviceRangeFamily family,
    CancellationToken cancellationToken = default)
```

That overload is for manual compatibility work. It is not the standard
application route.

Embedded model-code tables cover the known codes shared during implementation
for these families:

- `IqR`
- `MxR`
- `IqF`
- `QCpu`
- `LCpu`
- `QnU`
- `QnUDV`

`MxF` currently resolves by normalized model text prefixes:

- `MXF100-8-N32`
- `MXF100-16-N32`
- `MXF100-8-P32`
- `MXF100-16-P32`
- generic fallback prefix `MXF`

Other notable model-name fallback groups:

- `R...` -> `IqR`
- `FX5U...`, `FX5UC...`, `FX5UJ...`, `FX5S...` -> `IqF`
- `Q00U...`, `Q03UD...`, `Q50UDEH...` -> `QnU`
- `Q03UDV...`, `Q04UDPV...` -> `QnUDV`
- `L04HCPU`, `L08HCPU`, `L16HCPU`, `L32HCPU` -> `IqL`
- `L02...`, `L06CPU`, `L26CPU`, `LJ72GF15-T2` -> `LCpu`

Unknown models return `SlmpError`.

## Range Rules

`Addr` is the library-facing address notation, not necessarily the notation
used in Mitsubishi manuals.

For iQ-R-series range rows, the `SD` register value is the PLC-configured
current point count. The catalog must cap that value at the iQ-R-series maximum
below, then derive `UpperBound = PointCount - 1`. In other words, use
`PointCount = min(SD point count, MaxPointCount)` for the rows listed here.
The iQ-R-series `0002/0003` device access format uses a 4-byte device number,
so these maximum addresses are representable by the protocol format.

| Item | Devices | Max address | MaxPointCount | Setting unit |
| --- | --- | --- | --- | --- |
| `X` | `X` | `X2FFF` | `12288` (`0x3000`) | n/a |
| `Y` | `Y` | `Y2FFF` | `12288` (`0x3000`) | n/a |
| `M` | `M` | `M94674943` | `94674944` (`0x5A4A000`) | 64 points |
| `B` | `B` | `B5A49FFF` | `94674944` (`0x5A4A000`) | 64 points |
| `F` | `F` | `F32767` | `32768` | 64 points |
| `SB` | `SB` | `SB5A49FFF` | `94674944` (`0x5A4A000`) | 64 points |
| `V` | `V` | `V32767` | `32768` | 64 points |
| `L` | `L` | `L32767` | `32768` | 64 points |
| `T` | `TS`, `TC`, `TN` | `T5259711` | `5259712` | 32 points |
| `ST` | `STS`, `STC`, `STN` | `ST5259711` | `5259712` | 32 points |
| `LT` | `LTS`, `LTC`, `LTN` | `LT1479295` | `1479296` | 1 point |
| `LST` | `LSTS`, `LSTC`, `LSTN` | `LST1479295` | `1479296` | 1 point |
| `C` | `CS`, `CC`, `CN` | `C5259711` | `5259712` | 32 points |
| `LC` | `LCS`, `LCC`, `LCN` | `LC2784543` | `2784544` | 32 points |
| `D` | `D` | `D5917183` | `5917184` (`0x5A4A00`) | 4 points |
| `W` | `W` | `W5A49FF` | `5917184` (`0x5A4A00`) | 4 points |
| `SW` | `SW` | `SW5A49FF` | `5917184` (`0x5A4A00`) | 4 points |

| Item | Devices | Addr | iQ-R | MX-F | MX-R | iQ-F | QCPU | LCPU / QnU / QnUDV |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `X` | `X` | base16 | `SD260-SD261` | `SD260-SD261` | `SD260-SD261` | `SD260-SD261` | `SD290` | `SD290` |
| `Y` | `Y` | base16 | `SD262-SD263` | `SD262-SD263` | `SD262-SD263` | `SD262-SD263` | `SD291` | `SD291` |
| `M` | `M` | base10 | `SD264-SD265` | `SD264-SD265` | `SD264-SD265` | `SD264-SD265` | `SD292`, clip `32768` | `SD286-SD287` |
| `B` | `B` | base16 | `SD266-SD267` | `SD266-SD267` | `SD266-SD267` | `SD266-SD267` | `SD294`, clip `32768` | `SD288-SD289` |
| `SB` | `SB` | base16 | `SD268-SD269` | `SD268-SD269` | `SD268-SD269` | `SD268-SD269` | `SD296` | `SD296` |
| `F` | `F` | base10 | `SD270-SD271` | `SD270-SD271` | `SD270-SD271` | `SD270-SD271` | `SD295` | `SD295` |
| `V` | `V` | base10 | `SD272-SD273` | `SD272-SD273` | `SD272-SD273` | `n/a` | `SD297` | `SD297` |
| `L` | `L` | base10 | `SD274-SD275` | `SD274-SD275` | `SD274-SD275` | `SD274-SD275` | `SD293` | `SD293` |
| `S` | `S` | base10 | `SD276-SD277` | `n/a` | `n/a` | `n/a` | `SD298` | `SD298` |
| `D` | `D` | base10 | `SD280-SD281` | `SD280-SD281` | `SD280-SD281` | `SD280-SD281` | `SD302`, clip `32768` | `SD308-SD309` |
| `W` | `W` | base16 | `SD282-SD283` | `SD282-SD283` | `SD282-SD283` | `SD282-SD283` | `SD303`, clip `32768` | `SD310-SD311` |
| `SW` | `SW` | base16 | `SD284-SD285` | `SD284-SD285` | `SD284-SD285` | `SD284-SD285` | `SD304` | `SD304` |
| `R` | `R` | base10 | `SD306-SD307`, clip `32768` | `SD306-SD307`, clip `32768` | `SD306-SD307`, clip `32768` | `SD304-SD305` | fixed `32768` | `SD306-SD307`, clip `32768` |
| `T` | `TS`, `TC`, `TN` | base10 | `SD288-SD289` | `SD288-SD289` | `SD288-SD289` | `SD288-SD289` | `SD299` | `SD299` |
| `ST` | `STS`, `STC`, `STN` | base10 | `SD290-SD291` | `SD290-SD291` | `SD290-SD291` | `SD290-SD291` | `SD300` | `SD300` |
| `C` | `CS`, `CC`, `CN` | base10 | `SD292-SD293` | `SD292-SD293` | `SD292-SD293` | `SD292-SD293` | `SD301` | `SD301` |
| `LT` | `LTS`, `LTC`, `LTN` | base10 | `SD294-SD295` | `SD294-SD295` | `SD294-SD295` | `n/a` | `n/a` | `n/a` |
| `LST` | `LSTS`, `LSTC`, `LSTN` | base10 | `SD296-SD297` | `SD296-SD297` | `SD296-SD297` | `n/a` | `n/a` | `n/a` |
| `LC` | `LCS`, `LCC`, `LCN` | base10 | `SD298-SD299` | `SD298-SD299` | `SD298-SD299` | `SD298-SD299` | `n/a` | `n/a` |
| `Z` | `Z` | base10 | `SD300` | `SD300` | `SD300` | `SD300` | fixed `10` | fixed `20` |
| `LZ` | `LZ` | base10 | `SD302` | `SD302` | `SD302` | `SD302` | `n/a` | `n/a` |
| `ZR` | `ZR` | base10 | `SD306-SD307` | `SD306-SD307` | `SD306-SD307` | `n/a` | open | `SD306-SD307` |
| `RD` | `RD` | base10 | `SD308-SD309` | `SD308-SD309` | `SD308-SD309` | `n/a` | `n/a` | `n/a` |
| `SM` | `SM` | base10 | fixed `4096` | fixed `10000` | fixed `4496` | fixed `10000` | fixed `1024` | fixed `2048` |
| `SD` | `SD` | base10 | fixed `4096` | fixed `10000` | fixed `4496` | fixed `12000` | fixed `1024` | fixed `2048` |

## Notes

- `QCPU` `D` and `W` exclude the extended area and are clipped to `32768`.
- `QCPU` `ZR` remains supported, but there is no finite upper-bound register, so
  the catalog reports `Supported = true`, `PointCount = null`, and `AddressRange = null`.
- `LCPU`, `QnU`, and `QnUDV` share the same range-register layout in this
  implementation.
- `Z` on `LCPU`, `QnU`, and `QnUDV` is treated as the fixed `Z0-Z19` family
  limit. `QnUDV` was live-checked on 2026-05-01: `Z19` read successfully and
  `Z20` returned `0x4031`.
- `ZR` and `R` on `LCPU`, `QnU`, and `QnUDV` are runtime-limited by probing
  readable `ZR` addresses. `QnUDV` was live-checked on 2026-05-01 with
  `ZR393215` accepted, `ZR393216` returning `0x4031`, `R32767` accepted, and
  `R32768` returning `0x4031`.
- `iQ-F` `X` and `Y` are documented in Mitsubishi manuals with octal addressing.
  This library emits `Base8` and formats ranges such as `X0000-X1777`.

