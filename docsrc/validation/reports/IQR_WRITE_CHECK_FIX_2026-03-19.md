# iQ-R Write-Check Validation (2026-03-19)

## Scope

Validate `.NET` CLI write-check behavior against iQ-R (TCP/1025) after fixing:

- `1402` random bit write payload encoding for iQ-R
- `1406` mixed block write retry behavior on PLC reject end codes

## Executed Commands

```bash
dotnet run --project samples/PlcComm.Slmp.Cli -- random-check --host 192.168.250.101 --port 1025 --transport tcp --target SELF --write-check
dotnet run --project samples/PlcComm.Slmp.Cli -- block-check --host 192.168.250.101 --port 1025 --transport tcp --target SELF --write-check
```

## Result

- `random-check --write-check`: PASS
- `block-check --write-check`: PASS

## Captured Output Summary

- `[INFO] Resolved frame=4e, series=iqr`
- `[OK] random-read words=[4660] dwords=[305419896]`
- `[OK] random-write completed`
- `[OK] block-read words=[0, 0] bit_words=[0]`
- `[OK] block-write completed`

## Notes

This report confirms that the previous `0xC061` failures are resolved for the tested iQ-R path.
