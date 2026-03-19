# CHANGELOG

## 2026-03-19

- bootstrap `plc-comm-slmp-dotnet` solution with library, CLI sample, and tests
- implement core `SlmpClient` transport and binary 3E/4E request handling
- add core read/write, type-name, remote control, and clear-error APIs
- add `connection-check` and `other-station-check` CLI commands
- fix iQ-R random bit write payload encoding for `1402` write-check path
- add mixed block write retry handling for `0xC056`/`0xC05B`/`0xC061`/`0x414A`
- update `block-check --write-check` to enable mixed-block retry fallback
- add initial user/maintainer/validation documents
