# TCP Concurrency Finding (2026-03-19)

## Scope

Practical TCP concurrency check against the current PLC endpoint:

- Host: `192.168.250.101`
- Port: `1025`
- Transport: `tcp`
- Profile: `frame=4e`, `series=iqr` (auto-resolved)

## Commands

```bash
dotnet run --project samples/PlcComm.Slmp.Cli -- tcp-concurrency --host 192.168.250.101 --port 1025 --transport tcp --series auto --frame-type auto --target SELF --clients 2 --iterations 100 --stagger-ms 200
dotnet run --project samples/PlcComm.Slmp.Cli -- tcp-concurrency --host 192.168.250.101 --port 1025 --transport tcp --series auto --frame-type auto --target SELF --clients 4 --iterations 100 --stagger-ms 200
```

## Result

- `clients=2`: no connection reject observed.
- `clients=4`: connection reject observed (`connect failed client=2`).

## Operational Recommendation

- Use `clients <= 2` for this environment.
- If higher parallelism is required, use connection pooling or single-connection multiplexing.
