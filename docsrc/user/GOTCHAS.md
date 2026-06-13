# Gotchas

## LTN/LSTN/LCN/LZ reads return wrong values

These are 32-bit families. Always use the `:D` or `:L` suffix in named addresses.

```csharp
using System;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR) { Port = 1025 };
await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);

var value = await client.ReadTypedAsync("LTN0", "D");
Console.WriteLine(value);
```

Plain word access is rejected.

## LCS/LCC reads look incorrect

`LCS` and `LCC` use direct bit read. Writes go through random bit write (`0x1402`), not the word write path.

Fix: use `WriteBitInWordAsync` for word flags or `WriteTypedAsync` / `WriteNamedAsync` for long-family state devices. The library routes the request correctly.

## LTS/LTC/LSTS/LSTC write is rejected

Direct bit write is rejected for these families.

Fix: use `WriteBitInWordAsync` for word flags or `WriteTypedAsync` / `WriteNamedAsync` for long-family state devices. The library routes to the correct command.

## G or HG address returns an error

`G` and `HG` are not in the public high-level API.

Fix: use the low-level raw client methods for module buffer access.

## Mixed write (words and bits in one call) fails

The PLC rejects command `0x1406` for word and bit combinations.

Fix: separate word writes and bit writes into distinct `WriteNamedAsync` calls.

## DX or DY fails on iQ-F

`DX` and `DY` are not valid for `SlmpPlcProfile.IqF`.

Fix: use `X` and `Y` instead.

## Connection succeeds but all reads return an end code error

The `SlmpPlcProfile` does not match the actual PLC hardware.

Fix: verify the profile. It determines frame type and access mode.

## X or Y addresses do not match an iQ-F manual

iQ-F `X` and `Y` addresses use octal notation, while other profiles use hexadecimal notation.

Fix: use a connected client with `SlmpPlcProfile.IqF`, or call `SlmpAddress.Parse(text, SlmpPlcProfile.IqF)` when normalizing address text outside a client.
