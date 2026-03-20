# CHANGELOG

## 2026-03-19

- bootstrap `plc-comm-slmp-dotnet` solution with library, CLI sample, and tests
- implement core `SlmpClient` transport and binary 3E/4E request handling
- add core read/write, type-name, remote control, and clear-error APIs
- add `connection-check` and `other-station-check` CLI commands
- fix iQ-R random bit write payload encoding for `1402` write-check path
- add mixed block write retry handling for `0xC056`/`0xC05B`/`0xC061`/`0x414A`
- update `block-check --write-check` to enable mixed-block retry fallback
- expand `other-station-check` target parsing to support `SELF-CPU1..4` and repeatable `--target`
- add `compatibility-probe` CLI command with markdown/json latest report output
- add `g-hg-appendix1-coverage` CLI command with optional write-check flow
- add `compatibility-matrix-render` command for dotnet probe outputs
- add `appendix1-device-recheck`, `read-soak`, `mixed-read-load`, and `tcp-concurrency` CLI probes
- add `QueuedSlmpClient` and `single-connection-load` for single-TCP-connection serialized execution
- add Extended Specification extended device read/write support in `.NET` client
- add GitHub Actions CI workflow (`.github/workflows/ci.yml`)
- add initial user/maintainer/validation documents
