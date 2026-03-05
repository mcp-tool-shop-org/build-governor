---
title: Getting Started
description: Install Build Governor and protect your C++ builds.
sidebar:
  order: 1
---

Build Governor automatically protects against C++ build memory exhaustion on Windows. No manual configuration required.

## The problem

Parallel C++ builds can easily exhaust system memory:

- Each `cl.exe` instance can use 1-4 GB RAM (templates, LTCG, heavy headers)
- Build systems launch N parallel jobs and hope for the best
- When RAM exhausts: system freeze, or `CL.exe exited with code 1` with no diagnostic
- The killer metric is **Commit Charge**, not "free RAM"

## Quick start (automatic)

```powershell
# One-time setup (no admin required)
cd build-governor
.\scripts\enable-autostart.ps1

# That's it! All builds are now protected
cmake --build . --parallel 16
msbuild /m:16
ninja -j 8
```

The wrappers automatically:
- Start the governor if it's not running
- Monitor memory and throttle when needed
- Shut down after 30 minutes of inactivity

## Windows Service (enterprise)

For always-on protection across all users:

```powershell
# Requires Administrator
.\scripts\install-service.ps1
```

## Manual mode

If you prefer explicit control:

```powershell
# 1. Build
dotnet build -c Release
dotnet publish src/Gov.Wrapper.CL -c Release -o bin/wrappers
dotnet publish src/Gov.Wrapper.Link -c Release -o bin/wrappers
dotnet publish src/Gov.Cli -c Release -o bin/cli

# 2. Start governor (in one terminal)
dotnet run --project src/Gov.Service -c Release

# 3. Run your build (in another terminal)
bin/cli/gov.exe run -- cmake --build . --parallel 16
```
