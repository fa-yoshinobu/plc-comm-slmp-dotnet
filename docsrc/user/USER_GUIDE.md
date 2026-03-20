# SLMP .NET User Guide

This guide covers the basic usage of the SLMP (MC Protocol) client library for .NET.

## Getting Started

### 1. Installation
Add the project reference or DLL to your solution. (NuGet package coming soon).

### 2. Basic Connection
The `SlmpClient` is the primary entry point.

```csharp
using PlcComm.Slmp;

// Setup client (IP, Port, Mode)
using var client = new SlmpClient("192.168.1.10", 1025, SlmpTransportMode.Tcp);

// Open connection
await client.OpenAsync();

// Read D100 (1 word)
var data = await client.ReadWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 100), 1);
Console.WriteLine($"D100 value: {data[0]}");
```

## Advanced Features

### Queued Access
For multi-threaded environments, use `QueuedSlmpClient` to prevent connection collisions.

```csharp
await using var queuedClient = new QueuedSlmpClient(client);
var results = await Task.WhenAll(
    queuedClient.ReadWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 100), 1),
    queuedClient.ReadWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 200), 1)
);
```

### Device Parsing
You can use string-based parsing for convenience:
- `D100` -> Data Register 100
- `X0` -> Input 0 (Hex)
- `M100` -> Internal Relay 100

## Troubleshooting
- **Timeout**: Check PLC Network settings (Ethernet module configuration).
- **End Code 0xC059**: Check if the device range is valid for your PLC model.
