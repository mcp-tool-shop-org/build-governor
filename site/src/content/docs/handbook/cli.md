---
title: CLI Reference
description: Commands, environment variables, and setup scripts.
sidebar:
  order: 4
---

## CLI commands

```powershell
# Run a governed build
gov run -- cmake --build . --parallel 16

# Check governor status
gov status

# Run without auto-starting governor
gov run --no-start -- ninja -j 8
```

## Setup scripts

| Script | Requires Admin | Purpose |
|--------|---------------|---------|
| `scripts\enable-autostart.ps1` | No | User-level setup — installs wrappers in PATH |
| `scripts\install-service.ps1` | Yes | Windows Service — always-on protection |
| `scripts\uninstall-service.ps1` | Yes | Remove the Windows Service |

## Environment variables

| Variable | Description |
|----------|-------------|
| `GOV_REAL_CL` | Path to real cl.exe (auto-detected via vswhere) |
| `GOV_REAL_LINK` | Path to real link.exe (auto-detected) |
| `GOV_ENABLED` | Set by `gov run` to indicate governed mode |
| `GOV_SERVICE_PATH` | Path to Gov.Service.exe for auto-start |
| `GOV_DEBUG` | Set to "1" for verbose auto-start logging |

## Project structure

```
build-governor/
├── src/
│   ├── Gov.Protocol/       # Shared DTOs (NuGet)
│   ├── Gov.Common/         # Windows metrics, classifier (NuGet)
│   ├── Gov.Service/        # Background governor
│   ├── Gov.Wrapper.CL/     # cl.exe shim
│   ├── Gov.Wrapper.Link/   # link.exe shim
│   └── Gov.Cli/            # gov command
├── scripts/
│   ├── enable-autostart.ps1
│   ├── install-service.ps1
│   └── uninstall-service.ps1
└── bin/
    ├── wrappers/           # Published shims
    ├── service/            # Published service
    └── cli/                # Published CLI
```
