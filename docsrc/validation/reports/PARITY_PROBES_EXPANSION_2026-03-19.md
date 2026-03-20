# Parity Probe Expansion Validation (2026-03-19)

## Scope

Validate CLI parity tool expansion for `.NET`:

- `compatibility-matrix-render`
- `appendix1-device-recheck`
- `read-soak`
- `mixed-read-load`
- `tcp-concurrency`

## Local Validation

```bash
dotnet build PlcCommSlmp.sln
dotnet test PlcCommSlmp.sln
dotnet run --project samples/PlcComm.Slmp.Cli -- --help
run_ci.bat
```

## Result

- Build: PASS
- Unit tests: PASS (`6/6`)
- CLI command registration: PASS (`--help` includes new commands)
- Local CI script: PASS

## Notes

`PLC_COMPATIBILITY.md` remains generated from Python probe data in `plc-comm-slmp-python`.
Hardware evidence for the new `.NET` commands should be collected by on-device runs.
