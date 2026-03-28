// PlcComm.Slmp.HighLevelSample
// ============================
// Demonstrates all high-level SLMP APIs:
//   OpenAndConnectAsync, ReadWordsAsync (with allowSplit), ReadDWordsAsync,
//   ReadTypedAsync, WriteTypedAsync, WriteBitInWordAsync,
//   ReadNamedAsync, PollAsync, and SlmpClient options.
//
// Usage:
//   dotnet run --project samples/PlcComm.Slmp.HighLevelSample -- [host] [port] [series] [frame]
//
// Common SLMP port values:
//   1025  iQ-R / iQ-F built-in Ethernet SLMP (default here)
//   5000  GX Works3 / GX Works2 simulator
//   5007  Q/L series built-in Ethernet SLMP

using PlcComm.Slmp;

var host = args.Length > 0 ? args[0] : "192.168.250.100";
var port = args.Length > 1 ? int.Parse(args[1]) : 1025;
var seriesArg = args.Length > 2 ? args[2].ToLowerInvariant() : "iqr";
var frameArg = args.Length > 3 ? args[3].ToLowerInvariant() : "4e";
if (seriesArg is not ("iqr" or "ql"))
    throw new ArgumentException("series must be 'iqr' or 'ql'");
if (frameArg is not ("3e" or "4e"))
    throw new ArgumentException("frame must be '3e' or '4e'");
var compatibilityMode = seriesArg == "ql" ? SlmpCompatibilityMode.Legacy : SlmpCompatibilityMode.Iqr;
var frameType = frameArg == "3e" ? SlmpFrameType.Frame3E : SlmpFrameType.Frame4E;

// -------------------------------------------------------------------------
// 1. OpenAndConnectAsync  (recommended entry point)
//
// OpenAndConnectAsync opens a QueuedSlmpClient with explicit stable settings.
// QueuedSlmpClient is a thread-safe wrapper that serializes all requests
// through a SemaphoreSlim, so multiple concurrent Tasks can share one TCP
// connection without interleaving protocol frames. High-level helpers can
// be called directly on the queued client.
//
// port options:
//   1025  iQ-R / iQ-F Ethernet module SLMP port (default)
//   5000  GX Works3 / GX Works2 simulation port
//   5007  Q/L series Ethernet module SLMP port
// -------------------------------------------------------------------------
Console.WriteLine($"Connecting to {host}:{port} with frame={frameArg} series={seriesArg} ...");
await using var client = await SlmpClient.OpenAndConnectAsync(host, port, frameType, compatibilityMode);
Console.WriteLine($"[OpenAndConnectAsync] frame={client.FrameType}  series={client.CompatibilityMode}");

// -------------------------------------------------------------------------
// QueuedSlmpClient properties you can adjust after connection:
//
//   Timeout         - communication timeout (default 3 s)
//   MonitoringTimer - how long the PLC waits before aborting a request,
//                     in units of 250 ms (default 0x0010 = 4 s)
//   TargetAddress   - routing info for multi-network topologies
//                     (Network, Station, ModuleIo, Multidrop)
//   TraceHook       - optional Action<SlmpTraceFrame> for raw-frame logging;
//                     set to a lambda to capture every send/receive byte
//   FrameType       - SlmpFrameType.Frame3E or Frame4E
//   CompatibilityMode - Legacy (Q/L) or Iqr (iQ-R)
// -------------------------------------------------------------------------
client.Timeout = TimeSpan.FromSeconds(5);
client.MonitoringTimer = 0x0040;  // 16 s
// client.TraceHook = frame =>
//     Console.WriteLine($"[TRACE] {frame.Direction} {frame.Data.Length} B");

// -------------------------------------------------------------------------
// 2. ReadTypedAsync / WriteTypedAsync
//
// Read or write a single device with automatic type conversion.
// device - SlmpDeviceAddress parsed from a string, e.g. "D100"
// dtype  - "U" unsigned-16, "S" signed-16,
//          "D" unsigned-32, "L" signed-32, "F" float32
//
// Use case: reading a float32 sensor value from D200-D201 or writing a
//           signed 32-bit position preset to D300-D301.
// -------------------------------------------------------------------------
var valU = await client.ReadTypedAsync("D100", "U");
var valF = await client.ReadTypedAsync("D200", "F");
var valL = await client.ReadTypedAsync("D300", "L");
Console.WriteLine($"[ReadTypedAsync] D100(U)={valU}  D200(F)={valF}  D300(L)={valL}");

await client.WriteTypedAsync("D100", "U", (ushort)42);
await client.WriteTypedAsync("D200", "F", 3.14f);
await client.WriteTypedAsync("D300", "L", -100);
Console.WriteLine("[WriteTypedAsync] Wrote 42->D100, 3.14->D200, -100->D300");

// -------------------------------------------------------------------------
// 3. ReadWordsAsync (chunked read)
//
// Reads a contiguous block of word devices.
// maxPerRequest - maximum words per SLMP request (protocol limit 960).
// allowSplit    - when false (default) throws ArgumentException if
//                 count > maxPerRequest; when true, automatically chunks
//                 the read across multiple requests.
//
// Use case: reading a recipe table of 1200 words that exceeds the 960-word
//           SLMP limit - set allowSplit: true to handle it transparently.
// -------------------------------------------------------------------------
ushort[] words10 = await client.ReadWordsAsync("D0", 10);
Console.WriteLine($"[ReadWordsAsync] D0-D9 = [{string.Join(", ", words10)}]");

// Large read split across multiple SLMP requests:
ushort[] largeWords = await client.ReadWordsAsync("D0", 1000, allowSplit: true);
Console.WriteLine($"[ReadWordsAsync allowSplit] D0-D999: {largeWords.Length} words read");

// -------------------------------------------------------------------------
// 4. ReadDWordsAsync (chunked 32-bit read)
//
// Reads contiguous DWord (32-bit unsigned) values.
// Each DWord occupies two consecutive word registers (low-word first).
//
// Use case: reading an array of 32-bit position values that would require
//           960+ words if read as individual words.
// -------------------------------------------------------------------------
uint[] dwords = await client.ReadDWordsAsync("D0", 4);
Console.WriteLine($"[ReadDWordsAsync] D0-D7 as uint32[4] = [{string.Join(", ", dwords)}]");

// -------------------------------------------------------------------------
// 5. WriteBitInWordAsync
//
// Sets or clears a single bit inside a word device (read-modify-write).
// bitIndex 0 = LSB, 15 = MSB.
//
// Use case: toggling a request flag in a PLC control word without
//           disturbing the other 15 status bits.
// -------------------------------------------------------------------------
await client.WriteBitInWordAsync("D50", bitIndex: 3, value: true);
Console.WriteLine("[WriteBitInWordAsync] Set   bit 3 of D50");
await client.WriteBitInWordAsync("D50", bitIndex: 3, value: false);
Console.WriteLine("[WriteBitInWordAsync] Clear bit 3 of D50");

// -------------------------------------------------------------------------
// 6. ReadNamedAsync
//
// Reads multiple devices by address string with optional type suffix.
// Returns IReadOnlyDictionary<string, object>.
//
// Address notation:
//   "D100"    unsigned 16-bit (ushort)
//   "D100:F"  float32
//   "D100:S"  signed 16-bit (short)
//   "D100:D"  unsigned 32-bit (uint)
//   "D100:L"  signed 32-bit (int)
//   "D100.3"  bit 3 inside D100 (bool)
//
// Use case: reading a heterogeneous parameter set (speed float, error code
//           short, alarm bit bool) in a single call.
// -------------------------------------------------------------------------
var snapshot = await client.ReadNamedAsync(["D100", "D200:F", "D300:L", "D50.3"]);
foreach (var (addr, value) in snapshot)
    Console.WriteLine($"[ReadNamedAsync] {addr} = {value}");

// -------------------------------------------------------------------------
// 7. PollAsync
//
// Async iterator that yields a snapshot dict every interval.
// Uses CancellationToken to stop.
//
// Use case: background monitoring loop in a .NET application; drives a
//           live-update UI or historian with minimal overhead.
// -------------------------------------------------------------------------
Console.WriteLine("\nPolling 3 snapshots (1 s interval):");
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var pollCount = 0;
await foreach (var snap in client.PollAsync(
    ["D100", "D200:F", "D50.3"],
    TimeSpan.FromSeconds(1),
    cts.Token))
{
    Console.WriteLine($"  [{++pollCount}] D100={snap["D100"]}  D200:F={snap["D200:F"]}  D50.3={snap["D50.3"]}");
    if (pollCount >= 3)
        break;
}

Console.WriteLine("Done.");
