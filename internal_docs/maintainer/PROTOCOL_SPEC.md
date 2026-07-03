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

## Current Status

No active protocol TODO is tracked here. Use the repository-root `TODO.md` for
any future active item.
