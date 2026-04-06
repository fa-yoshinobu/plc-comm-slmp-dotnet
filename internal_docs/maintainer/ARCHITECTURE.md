# SLMP .NET Architecture

This document describes the internal design and structural components of the SLMP .NET library.

## Core Components

### 1. SlmpClient (Protocol Layer)
The primary class responsible for encoding and decoding SLMP frames (3E/4E). It handles the mapping between high-level read/write requests and raw byte arrays.

### 2. Transport Layer (Abstraction)
Communication is abstracted through a transport interface, allowing the same client logic to work over:
- **TCP**: Reliable, stream-oriented.
- **UDP**: Fast, packet-oriented.

### 3. QueuedSlmpClient (Concurrency Layer)
A wrapper that provides a thread-safe command queue. It ensures that only one request is active on the transport at any given time, preventing packet interleaving in multi-threaded applications.

## Request/Response Lifecycle

1.  **Request Construction**: `SlmpClient` builds the command header and payload.
2.  **Transmission**: The raw bytes are sent through the active `Transport`.
3.  **Reception**: The library waits for the response header, validates the length, and reads the full payload.
4.  **Parsing**: The response is validated (End Code check) and converted back into typed results (e.g., `ushort[]`).

## Error Handling
Errors are handled through a custom exception hierarchy, capturing both network-level failures and PLC-level error codes (End Codes).
