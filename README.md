# (Mitsubishi MELSEC) SLMP .NET

[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Static Analysis: dotnet format](https://img.shields.io/badge/Lint-dotnet%20format-blue.svg)](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format)

A modern, strictly typed .NET client library for Mitsubishi SLMP (Seamless Message Protocol). Supporting Binary 3E and 4E frames for iQ-R, iQ-F, and Q series PLCs.

## 噫 Key Features

- **High Performance**: Optimized for .NET 9.0 with asynchronous I/O.
- **Binary Support**: Full support for Binary 3E and 4E SLMP frames.
- **Strict Protocol Compliance**: Based on official Mitsubishi Electric English specifications.
- **Single-File Distribution**: Supports self-contained publishing.
- **CI-Ready**: Built-in quality checks via `run_ci.bat`.

## 逃 Quick Start

### Basic Usage
```csharp
using Slmp;

// Connect to a MELSEC iQ-R PLC
using var client = new SlmpClient("192.168.1.10", 1025);

// Read D100 (Word)
int val = await client.ReadWordAsync("D100");
Console.WriteLine($"Value: {val}");
```

## 当 Documentation

Follows the workspace-wide hierarchical documentation policy:

- [**User Guide**](docs/user/USER_GUIDE.md): Installation and API reference.
- [**QA Reports**](docs/validation/reports/): Formal evidence of communication with Mitsubishi hardware.
- [**Protocol Spec**](docs/maintainer/PROTOCOL_SPEC.md): Technical details of the SLMP implementation.

## 屏 Development & CI

Quality is managed via `run_ci.bat`.

### Local CI & Publish
```bash
run_ci.bat
```
Validates the code and publishes a self-contained Single-File EXE to the `publish/` directory.

## 塘 License

Distributed under the [MIT License](LICENSE).

