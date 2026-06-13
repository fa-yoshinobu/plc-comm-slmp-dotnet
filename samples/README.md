# SLMP .NET samples

These projects show how to connect to a Mitsubishi PLC with one explicit `SlmpPlcProfile` and the current high-level SLMP API.

## How to run

Use `192.168.250.100` and TCP port `1025` for the standard getting-started setup.

```powershell
dotnet run --project samples/PlcComm.Slmp.HighLevelSample -- 192.168.250.100 1025 melsec:iq-r
dotnet run --project samples/PlcComm.Slmp.QueuedSample -- 192.168.250.100 1025 melsec:iq-r 4 10
dotnet run --project samples/PlcComm.Slmp.Cli -- connection-check --plc-profile melsec:iq-r --host 192.168.250.100 --port 1025
```

Use only test addresses that are safe for your PLC program before you run any write example.

## Sample index

| Project | Purpose | Notes |
| --- | --- | --- |
| [PlcComm.Slmp.HighLevelSample](PlcComm.Slmp.HighLevelSample/) | Walks through the main high-level APIs in one program. | Shows `SlmpClientFactory.OpenAndConnectAsync`, typed reads and writes, block reads, bit-in-word updates, named snapshots, and polling. |
| [PlcComm.Slmp.QueuedSample](PlcComm.Slmp.QueuedSample/) | Demonstrates one shared queued client across concurrent workers. | Uses `QueuedSlmpClient` returned by the factory so multiple tasks serialize access to one connection. |
| [PlcComm.Slmp.Cli](PlcComm.Slmp.Cli/) | Provides command-line checks and operational probes. | Includes `connection-check` and `device-range-catalog` commands for profile-selected PLC sessions. |

## CLI examples

| Command | Example |
| --- | --- |
| Connection check | `dotnet run --project samples/PlcComm.Slmp.Cli -- connection-check --plc-profile melsec:iq-r --host 192.168.250.100 --port 1025` |
| Device range catalog | `dotnet run --project samples/PlcComm.Slmp.Cli -- device-range-catalog --plc-profile melsec:iq-r --host 192.168.250.100 --port 1025` |
