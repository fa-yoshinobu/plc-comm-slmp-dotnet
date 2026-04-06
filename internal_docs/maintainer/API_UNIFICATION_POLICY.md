# API Unification Policy

This document defines the public API rules for the SLMP .NET library.
It is a design policy document. It does not claim that every rule is implemented yet.

## Purpose

- Keep the SLMP .NET API internally consistent between `SlmpClient` and `QueuedSlmpClient`.
- Keep operation names aligned with the SLMP Python library where the operation class is the same.
- Avoid hiding protocol-specific distinctions behind overly generic names.

## Public API Shape

The canonical client classes are:

- `SlmpClient` - full protocol client
- `QueuedSlmpClient` - serialized wrapper for concurrent callers

This library is protocol-oriented.
It must not replace clear SLMP operation names with a generic `Read()` or `Write()` facade.

Canonical direct device names:

- `ReadWordsRawAsync`
- `WriteWordsAsync`
- `ReadBitsAsync`
- `WriteBitsAsync`
- `ReadDWordsRawAsync`
- `WriteDWordsAsync`
- `ReadFloat32sAsync`
- `WriteFloat32sAsync`
- `ReadWordsExtendedAsync`
- `WriteWordsExtendedAsync`
- `ReadRandomAsync` <-> `read_random`
- `ReadRandomExtAsync`
- `WriteRandomWordsAsync`
- `WriteRandomWordsExtAsync`
- `WriteRandomBitsAsync`
- `WriteRandomBitsExtAsync`
- `ReadBlockAsync`
- `WriteBlockAsync`
- `ReadTypeNameAsync`

Canonical specialized names:

- `RegisterMonitorDevicesAsync`
- `RegisterMonitorDevicesExtAsync` <-> `register_monitor_devices_ext`
- `RunMonitorCycleAsync` <-> `run_monitor_cycle`
- `MemoryReadWordsAsync`
- `MemoryWriteWordsAsync`
- `ExtendUnitReadWordsAsync`
- `ExtendUnitWriteWordsAsync`
- `CpuBufferReadWordsAsync`
- `CpuBufferWriteWordsAsync`
- `ReadArrayLabelsAsync` <-> `read_array_labels`
- `WriteArrayLabelsAsync`
- `ReadRandomLabelsAsync`
- `WriteRandomLabelsAsync`
- `RemoteRunAsync`
- `RemoteStopAsync`
- `RemotePauseAsync`
- `RemoteLatchClearAsync`
- `RemoteResetAsync`
- `RemotePasswordUnlockAsync`
- `RemotePasswordLockAsync`

## Extension Methods

`SlmpClientExtensions` provides convenience wrappers:

- `ReadWordsAsync` - aligned multi-word read (no cross-DWord splits by default)
- `ReadDWordsAsync` - aligned DWord read
- `ReadTypedAsync` / `WriteTypedAsync` - dtype-based typed access (U/S/D/L/F)
- `WriteBitInWordAsync` - single-bit write within a word device
- `ReadNamedAsync` - address-string-based read
- `PollAsync` - repeated read with condition predicate
- `ParseAddress` - parse device address string to `SlmpDeviceAddress`

## Cross-Library Parity Rules

When an equivalent operation exists in the SLMP Python library, semantic names must stay aligned.

Examples:

- `ReadRandomAsync` <-> `read_random`
- `RegisterMonitorDevicesExtAsync` <-> `register_monitor_devices_ext`
- `RunMonitorCycleAsync` <-> `run_monitor_cycle`
- `ReadArrayLabelsAsync` <-> `read_array_labels`

## Async Rules

Async methods must use the same base name as the conceptual operation with the .NET `Async` suffix.

- Return the same logical result shape as the Python equivalent.
- Keep parameter order stable.
- Accept an optional `CancellationToken` on all async methods.

## 32-Bit Value Rules

- `DWord` means a raw 32-bit unsigned value stored across two PLC words (low-word-first).
- Signed 32-bit helpers, if added later, should be named `ReadInt32Async` / `WriteInt32Async`.
- Floating-point helpers must use `Float32` in the public name.

## Documentation Rules

README and samples must describe the canonical names from this document.
`QueuedSlmpClient` must expose the same surface as `SlmpClient` for all commonly used operations.













