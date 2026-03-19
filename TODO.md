# TODO

## Python Parity Roadmap

- [ ] Add missing high-level APIs to match `plc-comm-slmp-python`:
  - [ ] random read/write full set
  - [ ] block read/write full set (including mixed split/retry policy parity)
  - [ ] monitor registration / monitor cycle
  - [ ] memory read/write (`0613` / `1613`)
  - [ ] extension unit read/write (`0601` / `1601`)
  - [ ] label operations
  - [ ] file operations
  - [ ] on-demand receive/request
- [ ] Add async + sync API parity wrappers for all supported operations.
- [ ] Add CLI parity scripts equivalent to Python probes:
  - [x] compatibility probe
  - [x] g/hg appendix1 coverage
  - [x] matrix renderer
  - [x] appendix recheck tools
  - [x] soak / mixed-read-load / tcp-concurrency probes
- [ ] Add hardware evidence reports under `docs/validation/reports/`.
- [x] Add CI workflow (`.github/workflows/ci.yml`) for build + test + format + docs link check.
- [ ] Tighten package distribution rules to exclude repository-only docs and tests from NuGet outputs.

## Known Limitations

- Linked direct devices (`Jn\Xn` style) are not supported yet.
