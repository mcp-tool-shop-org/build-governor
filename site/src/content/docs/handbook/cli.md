---
title: CLI Reference
description: Commands, environment variables, and setup scripts.
sidebar:
  order: 4
---

## CLI commands

The `gov` command provides three subcommands:

```powershell
# Run a governed build
gov run -- cmake --build . --parallel 16

# Run without auto-starting governor (fail if not running)
gov run --no-start -- ninja -j 8

# Check governor status (tokens, leases, commit charge)
gov status

# Show help
gov help
```

### `gov run`

Wraps a build command with governed `cl.exe` and `link.exe`:

1. Locates the published wrapper directory (searches multiple candidate paths)
2. Checks if the governor is running (connects via named pipe with 500 ms timeout)
3. If not running and `--no-start` is not set, starts the governor and waits up to 2 s
4. Prepends the wrapper directory to `PATH` and sets `GOV_ENABLED=1`
5. Auto-detects real `cl.exe` and `link.exe` via `vswhere` (or falls back to PATH search, skipping the wrapper directory)
6. Runs the build command via `cmd /c` so the modified PATH is respected

### `gov status`

Connects to the governor's named pipe and displays:

- Token pool: available / total
- Active lease count
- System commit charge (ratio and absolute values)
- Recommended parallelism level (`-j` value)

If the governor is not running, prints "Governor: not running" and exits with code 1.

## Setup scripts

| Script | Requires Admin | Purpose |
|--------|---------------|---------|
| `scripts\enable-autostart.ps1` | No | User-level setup — installs wrappers in PATH |
| `scripts\install-service.ps1` | Yes | Windows Service — always-on protection |
| `scripts\uninstall-service.ps1` | Yes | Remove the Windows Service |

## Environment variables

| Variable | Description |
|----------|-------------|
| `GOV_REAL_CL` | Path to real cl.exe (auto-detected via vswhere if not set) |
| `GOV_REAL_LINK` | Path to real link.exe (auto-detected if not set) |
| `GOV_ENABLED` | Set by `gov run` to indicate governed mode |
| `GOV_SERVICE_PATH` | Path to Gov.Service.exe for auto-start (checked first by the auto-start locator) |
| `GOV_DEBUG` | Set to "1" for verbose auto-start and idle-timeout logging |

## Gov.Service flags

The governor service accepts these command-line flags:

| Flag | Description |
|------|-------------|
| `--background` | Run in background mode with 30-minute idle auto-shutdown |
| `--service` | Run as Windows Service (quiet output) |

Without flags, the governor runs in foreground mode with full console output (memory status, GPU info, token pool details).

## Project structure

```
build-governor/
├── src/
│   ├── Gov.Protocol/       # Shared DTOs (NuGet: Gov.Protocol)
│   ├── Gov.Common/         # Windows metrics, classifier, GPU metrics,
│   │                       # process monitoring, auto-start (NuGet: Gov.Common)
│   ├── Gov.Service/        # Background governor + token pool
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
