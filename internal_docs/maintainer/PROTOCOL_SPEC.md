# Protocol Notes

## Frame Support

- Binary 3E request/response
- Binary 4E request/response

## Transport Support

- TCP (`TcpClient`)
- UDP (`UdpClient`)

## Compatibility Modes

- `legacy`: subcommands `0x0000/0x0001`
- `iqr`: subcommands `0x0002/0x0003`

## Current API Scope

Implemented in `SlmpClient`:

- `ReadTypeNameAsync`
- `ReadWordsAsync` / `WriteWordsAsync`
- `ReadBitsAsync` / `WriteBitsAsync`
- random read/write
- block read/write
- monitor registration / monitor cycle
- label array / label random read/write
- memory read/write
- extension-unit read/write
- Extended Specification read/write helpers
- `RemoteRunAsync`, `RemoteStopAsync`, `RemotePauseAsync`, `RemoteLatchClearAsync`, `RemoteResetAsync`
- `ClearErrorAsync`
- low-level `RequestAsync`

## Follow-Up

Semantic parity with `plc-comm-slmp-python` is still the target, but the
remaining items are now mostly environment-dependent follow-up work rather than
missing core APIs. See `TODO.md` for the active list.
