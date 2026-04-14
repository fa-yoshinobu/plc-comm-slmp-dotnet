# Getting Started

## Start Here

Use this package when you want the shortest .NET path to Mitsubishi SLMP communication through the public high-level API.

Recommended first path:

1. Install `PlcComm.Slmp`.
2. Choose one explicit `SlmpPlcFamily`.
3. Open one client with `SlmpClientFactory.OpenAndConnectAsync`.
4. Read one safe `D` word.
5. Write only to a known-safe test word or bit after the first read is stable.

## First PLC Registers To Try

Start with these first:

- `D100`
- `D200:F`
- `D300:L`
- `D50.3`
- `M1000`

Do not start with these:

- module/extension routing
- large chunked reads
- future-tracked families such as `G`, `HG`, `LTS`, `LTC`, `LSTS`, `LSTC`, `LCS`, `LCC`, `LZ`

## Minimal Connection Pattern

```csharp
var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcFamily.IqR)
{
    Port = 1025,
};

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
```

## First Successful Run

Recommended order:

1. `ReadTypedAsync("D100", "U")`
2. `WriteTypedAsync("D100", "U", value)` only on a safe test word
3. `ReadNamedAsync(["D100", "D200:F", "D50.3"])`

## Common Beginner Checks

If the first read fails, check these in order:

- correct host and port
- correct `SlmpPlcFamily`
- start with `D` instead of a routed, module, or future-tracked family

## Next Pages

- [Supported PLC Registers](./SUPPORTED_REGISTERS.md)
- [Latest Communication Verification](./LATEST_COMMUNICATION_VERIFICATION.md)
- [User Guide](./USER_GUIDE.md)
