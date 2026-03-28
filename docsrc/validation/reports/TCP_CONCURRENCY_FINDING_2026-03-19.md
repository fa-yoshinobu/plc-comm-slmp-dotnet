# TCP Concurrency Finding (2026-03-19)

## Scope

Practical TCP concurrency check against the current PLC endpoint:

- Host: `192.168.250.100`
- Port: `1025`
- Transport: `tcp`
- Profile: `frame=4e`, `series=iqr` (auto-resolved)

## Commands

```bash
dotnet run --project samples/PlcComm.Slmp.Cli -- tcp-concurrency --host 192.168.250.100 --port 1025 --transport tcp --series auto --frame-type auto --target SELF --clients 2 --iterations 100 --stagger-ms 200
dotnet run --project samples/PlcComm.Slmp.Cli -- tcp-concurrency --host 192.168.250.100 --port 1025 --transport tcp --series auto --frame-type auto --target SELF --clients 4 --iterations 100 --stagger-ms 200
dotnet run --project samples/PlcComm.Slmp.Cli -- tcp-concurrency --host 192.168.250.100 --port 1025 --transport tcp --series auto --frame-type auto --target SELF --clients 1 --iterations 500 --stagger-ms 200 --quiet
```

## Result

- `clients=1`: stable (`Connect failures: 0`, `Read failures: 0`).
- `clients=2`: connection reject still observed on this endpoint.
- `clients=4`: connection reject observed.
- `single-connection-load` with `workers=4`: stable (`Read failures: 0`).

## Operational Recommendation

- Do not rely on direct multi-connection TCP for parallel work on this endpoint.
- If higher parallelism is required, use single-connection multiplexing / request queueing.
- Auto profile resolution is now cached per process, so `--series auto --frame-type auto` can remain enabled without repeated per-client probe overhead.
