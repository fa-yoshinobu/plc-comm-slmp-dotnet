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
- `RemoteRunAsync`, `RemoteStopAsync`, `RemotePauseAsync`, `RemoteLatchClearAsync`, `RemoteResetAsync`
- `ClearErrorAsync`
- low-level `RequestAsync`

## Next Steps

See `TODO.md` for remaining parity with Python implementation (labels, file operations, monitor registration, block/random/extended memory full parity, and richer CLI set).
