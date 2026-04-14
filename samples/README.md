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
- explicit `single-request` and `chunked` contiguous reads
- bit-in-word updates
- mixed `ReadNamedAsync`
- `PollAsync`

### `PlcComm.Slmp.QueuedSample`

```powershell
dotnet run --project samples/PlcComm.Slmp.QueuedSample -- 192.168.250.100 1025 4 10
```

Included examples:

- one shared queued client from `SlmpClientFactory.OpenAndConnectAsync`
- concurrent workers using only high-level helper APIs
- repeated typed and mixed named reads

## Notes

- These two projects are the recommended user-facing examples.
- The newer explicit APIs such as `SlmpClientFactory.OpenAndConnectAsync`,
  `ReadWordsSingleRequestAsync`, and `ReadWordsChunkedAsync` use the same
  queued-client and device-string model shown in these samples.
- The CLI sample remains in the repository as an operational tool, but the user manual now centers on the high-level library APIs.
- The CLI sample includes `connection-profile-probe` to test `ReadTypeNameAsync`
  plus one family-specific `SD` block read for each frame and compatibility candidate:
  `dotnet run --project samples/PlcComm.Slmp.Cli -- connection-profile-probe --host 192.168.250.100 --port 1025`
- The CLI sample includes `device-range-catalog` for user-selected PLC family
  plus one family-specific `SD` block read:
  `dotnet run --project samples/PlcComm.Slmp.Cli -- device-range-catalog --plc-type iq-f --host 192.168.250.100 --port 1025 --series ql --frame-type 3e`
