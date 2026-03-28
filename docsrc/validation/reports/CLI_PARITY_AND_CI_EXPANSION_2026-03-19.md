# CLI Parity and CI Expansion Validation (2026-03-19)

## Scope

Validate newly added parity-oriented CLI features and CI workflow scaffolding:

- Extended target parsing (`SELF-CPU1..4`, repeatable `--target`)
- `compatibility-probe` command
- `g-hg-ExtendedDevice-coverage` command
- GitHub Actions CI pipeline (`.github/workflows/ci.yml`)

## Local Verification

```bash
dotnet build PlcCommSlmp.sln
dotnet test PlcCommSlmp.sln
run_ci.bat
```

## Result

- Build: PASS
- Unit tests: PASS (`6/6`)
- Local CI script: PASS

## Notes

This report verifies implementation and local execution paths.
Hardware communication evidence for the new commands should be added with separate on-device runs.

