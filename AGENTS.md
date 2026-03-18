# Agent Guide: SLMP .NET

This repository is part of the PLC Communication Workspace and follows the global standards defined in `D:\PLC_COMM_PROJ\AGENTS.md`.

## 1. Project-Specific Context
- **Protocol**: SLMP (Seamless Message Protocol)
- **Authoritative Source**: Mitsubishi Electric specifications.
- **Language**: .NET 9.0 (C#)
- **Role**: Core Communication Library (Binary 3E/4E frames).

## 2. Mandatory Rules (Global Standards)
- **Language**: All code, comments, and documentation MUST be in **English**.
- **Encoding**: Use **UTF-8 (without BOM)** for all files to prevent Mojibake.
- **Mandatory Static Analysis**:
  - All changes must pass `dotnet format` and Roslyn analyzers (StyleCop/SonarAnalyzer).
  - Use `dotnet build` to verify compliance.
- **Documentation Structure**: Follow the Modern Documentation Policy:
  - `docs/user/`: User manuals and API guides. [DIST]
  - `docs/maintainer/`: Protocol specs and internal logic. [REPO]
  - `docs/validation/`: Hardware QA reports and bug analysis. [REPO]
- **Distribution Control**: Ensure `.csproj` excludes `docs/maintainer/`, `docs/validation/`, `tests/`, and `TODO.md` from NuGet packages (`.nupkg`).

## 3. Reference Materials
- **Official Specs**: Refer to `local_folder/CLPA_DOWNLOAD_.../` for authoritative English manuals (Local only).
- **Evidence**: Check `docs/validation/reports/` for verified communication results with Mitsubishi iQ-R, iQ-F, and Q series.

## 4. Development Workflow
- **Issue Tracking**: Log remaining tasks in `TODO.md`.
- **Change Tracking**: Update `CHANGELOG.md` for every fix or feature.
- **QA Requirement**: Every hardware-related fix must include an evidence report in `docs/validation/reports/`.
