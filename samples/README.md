# SLMP .NET samples

These projects show how to connect to a MELSEC PLC with one explicit `SlmpPlcProfile` and the current high-level SLMP API.

## How to run

Use `192.168.250.100` and TCP port `1025` for the standard getting-started setup. Use UDP port `1035` when you intentionally test cable-pull recovery.

```powershell
dotnet run --project samples/PlcComm.Slmp.HighLevelSample -- 192.168.250.100 1025 melsec:iq-r
dotnet run --project samples/PlcComm.Slmp.PollingReconnectSample -- 192.168.250.100 1025 melsec:iq-r D100 U 1
dotnet run --project samples/PlcComm.Slmp.PollingReconnectSample -- 192.168.250.100 1035 melsec:iq-r D100 U 1 udp
dotnet run --project samples/PlcComm.Slmp.MultiPlcMonitorSample -- --plc line-a=192.168.250.101,melsec:iq-r,1035,udp --plc line-b=192.168.250.100,melsec:iq-f,1025,tcp --tag d100=D100:U
dotnet run --project samples/PlcComm.Slmp.ConfigPollingSample -- --config samples/PlcComm.Slmp.ConfigPollingSample/config_polling.example.json --dry-run
dotnet run --project samples/PlcComm.Slmp.QueuedSample -- 192.168.250.100 1025 melsec:iq-r 4 10
dotnet run --project samples/PlcComm.Slmp.Cli -- connection-check --plc-profile melsec:iq-r --host 192.168.250.100 --port 1025
```

Use only test addresses that are safe for your PLC program before you run any write example.

## Sample index

| Project | Purpose | Notes |
| --- | --- | --- |
| [PlcComm.Slmp.HighLevelSample](PlcComm.Slmp.HighLevelSample/) | Walks through the main high-level APIs in one program. | Shows `SlmpClientFactory.OpenAndConnectAsync`, typed reads and writes, block reads, bit-in-word updates, named snapshots, and polling. |
| [PlcComm.Slmp.PollingReconnectSample](PlcComm.Slmp.PollingReconnectSample/) | Read-only polling loop with automatic reconnect. | Logs `connected`, `lost`, `reconnecting`, and `recovered` transitions with exponential backoff. |
| [PlcComm.Slmp.MultiPlcMonitorSample](PlcComm.Slmp.MultiPlcMonitorSample/) | Read-only multi-PLC monitoring recipe. | Uses one task and reconnect loop per PLC so an offline endpoint does not block healthy PLC reads. |
| [PlcComm.Slmp.ConfigPollingSample](PlcComm.Slmp.ConfigPollingSample/) | Read-only JSON-configured polling recipe. | Supports `--dry-run` and writes optional long-form CSV rows as `timestamp,plc,tag,value`; YAML config is Python-only. |
| [PlcComm.Slmp.QueuedSample](PlcComm.Slmp.QueuedSample/) | Demonstrates one shared queued client across concurrent workers. | Uses `QueuedSlmpClient` returned by the factory so multiple tasks serialize access to one connection. |
| [PlcComm.Slmp.Cli](PlcComm.Slmp.Cli/) | Provides command-line checks and operational probes. | Includes `connection-check` and `device-range-catalog` commands for profile-selected PLC sessions. |

Included examples:

- one high-level walkthrough for typed reads and writes, block reads, named snapshots, and polling
- one shared queued client from `SlmpClientFactory.OpenAndConnectAsync`
- concurrent workers using only high-level helper APIs
- repeated typed and mixed named reads
- read-only operational recipes for multi-PLC monitoring and config-driven CSV polling

## Notes

- The high-level, polling reconnect, and queued samples are the recommended user-facing examples.
- The multi-PLC monitor and config polling recipes are read-only and call only read APIs.
- The newer explicit APIs such as `SlmpClientFactory.OpenAndConnectAsync`,
  `ReadWordsSingleRequestAsync`, and `ReadWordsChunkedAsync` use the same
  queued-client and device-string model shown in these samples.
- The CLI sample remains in the repository as an operational tool, but the user manual now centers on the high-level library APIs.
- The CLI sample includes `device-range-catalog` for user-selected PLC profile
  plus one profile-specific `SD` block read:
  `dotnet run --project samples/PlcComm.Slmp.Cli -- device-range-catalog --plc-profile melsec:iq-f --host 192.168.250.100 --port 1025`
- The CLI sample includes `extendeddevice-coverage` for Extended Specification
  live sweeps. It keeps OK/NG rows in a Markdown report and supports both word
  and bit extended devices:
  `dotnet run --project samples/PlcComm.Slmp.Cli -- extendeddevice-coverage --host 192.168.250.100 --port 1025 --plc-profile melsec:iq-r --device U3E0\G10 --points 1 --write-check`
- If the PLC port is protected by a remote password, pass
  `--remote-password <password>` or set `SLMP_REMOTE_PASSWORD=<password>`. The
  CLI unlocks before the sweep and locks the port again before exiting.

## CLI examples

| Command | Example |
| --- | --- |
| Connection check | `dotnet run --project samples/PlcComm.Slmp.Cli -- connection-check --plc-profile melsec:iq-r --host 192.168.250.100 --port 1025` |
| Device range catalog | `dotnet run --project samples/PlcComm.Slmp.Cli -- device-range-catalog --plc-profile melsec:iq-r --host 192.168.250.100 --port 1025` |
