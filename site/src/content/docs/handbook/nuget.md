---
title: NuGet Packages
description: Gov.Protocol and Gov.Common for custom tooling.
sidebar:
  order: 5
---

Build Governor publishes two NuGet packages for building custom tooling on top of its memory metrics and OOM classifier.

## Gov.Protocol

Shared message DTOs for client-service communication over named pipes. No Windows dependency — can be referenced from any .NET project.

```xml
<PackageReference Include="Gov.Protocol" Version="1.*" />
```

### Message types

| Type | Direction | Purpose |
|------|-----------|---------|
| `AcquireTokensRequest` | Client -> Service | Request tokens before a build tool runs |
| `AcquireTokensResponse` | Service -> Client | Grant or deny with lease ID and commit ratio |
| `ReleaseTokensRequest` | Client -> Service | Return tokens with process stats (peak memory, exit code, duration) |
| `ReleaseTokensResponse` | Service -> Client | Failure classification, retry recommendation |
| `HeartbeatRequest` | Client -> Service | Keep a lease alive during long operations |
| `HeartbeatResponse` | Service -> Client | Confirm lease is still valid |
| `StatusRequest` | Client -> Service | Query pool state |
| `StatusResponse` | Service -> Client | Tokens, leases, commit ratio, recommended parallelism |

### Failure classifications

The `FailureClassification` enum: `Success`, `NormalCompileError`, `LikelyOOM`, `LikelyPagingDeath`, `Unknown`.

Use this package when you want to:
- Build a custom wrapper for a different build tool
- Communicate with the governor service programmatically
- Integrate governor awareness into your own build tooling

## Gov.Common

Windows memory metrics, OOM classification, GPU metrics, process monitoring, and auto-start client. Depends on Windows APIs.

```xml
<PackageReference Include="Gov.Common" Version="1.*" />
```

### Key classes

| Class | Purpose |
|-------|---------|
| `WindowsMemoryMetrics` | Read system commit charge and calculate token budgets via `GlobalMemoryStatusEx` |
| `FailureClassifier` | Classify build failures using an evidence-weighted model |
| `GovernorClient` | Fail-safe named pipe client with auto-start support |
| `GovernorAutoStart` | Mutex-guarded governor startup (locates and launches `Gov.Service.exe`) |
| `ProcessMetrics` | Per-process memory monitoring via `psapi.dll` (100 ms sampling) |
| `GpuMetrics` | NVIDIA GPU status via `nvidia-smi` (VRAM, utilization, temperature) |

Use this package when you want to:
- Read system commit charge and memory pressure levels
- Classify build failures as OOM vs normal errors
- Auto-start the governor service from your own tools
- Monitor per-process memory consumption
- Query GPU memory status

## Security and data scope

Build Governor operates entirely locally on Windows:

| Aspect | Detail |
|--------|--------|
| Data accessed | System commit charge, per-process memory via Windows APIs, GPU metrics via nvidia-smi, named pipe IPC |
| Data NOT accessed | No network requests, no telemetry, no credential storage, no build artifact inspection |
| Permissions | Standard user for CLI/wrappers, Administrator for Service installation only |

See [SECURITY.md](https://github.com/mcp-tool-shop-org/build-governor/blob/main/SECURITY.md) for vulnerability reporting.
