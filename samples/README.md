# SLMP .NET Samples

This folder contains the recommended high-level sample projects.

## Main Samples

### `PlcComm.Slmp.HighLevelSample`

```powershell
dotnet run --project samples/PlcComm.Slmp.HighLevelSample -- 192.168.250.100 1025 iqr 4e
```

Included examples:

- explicit high-level connect
- typed reads and writes
- chunked word and dword reads
- bit-in-word updates
- mixed `ReadNamedAsync`
- `PollAsync`

### `PlcComm.Slmp.QueuedSample`

```powershell
dotnet run --project samples/PlcComm.Slmp.QueuedSample -- 192.168.250.100 1025 4 10
```

Included examples:

- one shared `QueuedSlmpClient`
- concurrent workers using only high-level helper APIs
- repeated typed and mixed named reads

## Notes

- These two projects are the recommended user-facing examples.
- The CLI sample remains in the repository as an operational tool, but the user manual now centers on the high-level library APIs.
