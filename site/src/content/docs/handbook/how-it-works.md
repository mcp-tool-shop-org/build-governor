---
title: How It Works
description: Architecture, token system, and auto-start behavior.
sidebar:
  order: 2
---

Build Governor sits between your build system and the compiler, throttling concurrency based on real-time memory pressure.

## Automatic protection flow

When you run a build:

1. `cmake` (or msbuild/ninja) spawns `cl.exe` — which is actually the Build Governor wrapper
2. The wrapper checks if the governor service is running via a named pipe connection (100 ms timeout)
3. If not, it auto-starts `Gov.Service.exe --background` using a global mutex to prevent races
4. The wrapper requests tokens from the governor (up to 60 s wait)
5. When granted, it runs the real `cl.exe` while monitoring per-process memory every 100 ms
6. After completion, it releases the tokens and receives a failure classification

## Token cost model

Different operations consume different numbers of tokens, reflecting their real-world memory profiles:

| Action | Tokens | Notes |
|--------|--------|-------|
| Normal compile | 1 | Baseline |
| Heavy compile (Boost/gRPC) | +2 | Template-heavy source path detected |
| Compile with /GL | +3 | Whole-program optimization |
| Compile with /Zi or /O2 | +1 | Debug info or aggressive optimization |
| Link | 4 | Base link cost |
| Link with /LTCG | +4 | Full link-time code generation |
| Link with /GL | +2 | Whole-program optimization at link |
| Link with PGO | +2 | Profile-guided optimization |

Compile tokens are clamped to 1-8. Link tokens are clamped to 2-12. Incremental linking reduces the cost by 2.

## Token budget

The governor calculates a dynamic token budget based on available commit headroom:

| Parameter | Default | Purpose |
|-----------|---------|---------|
| GbPerToken | 2.0 | GB of available commit per token |
| SafetyReserveGb | 8.0 | GB always kept free |
| MinTokens | 1 | Floor even under pressure |
| MaxTokens | 32 | Ceiling to prevent runaway |

The budget recalculates every 500 ms as part of the monitoring loop.

## Architecture

The system consists of six components:

- **Gov.Service** — background token pool that monitors RAM, grants/refuses tokens, and classifies failures. Communicates over a named pipe (`BuildGovernor`). Runs a 500 ms monitoring loop for memory sampling and lease expiry.
- **Gov.Wrapper.CL** — shim that replaces `cl.exe` in PATH, estimates token cost from compiler flags, requests tokens before compiling, and monitors process memory during execution.
- **Gov.Wrapper.Link** — shim that replaces `link.exe` in PATH, same pattern as the CL wrapper but with link-specific cost estimation.
- **Gov.Cli** — the `gov` command for governed builds (`gov run`) and status checks (`gov status`).
- **Gov.Protocol** — shared message DTOs (acquire, release, heartbeat, status) with no Windows dependency.
- **Gov.Common** — Windows memory metrics via `GlobalMemoryStatusEx`, OOM classification, GPU metrics via `nvidia-smi`, process-level memory monitoring via `psapi.dll`, and the auto-start client.

## Auto-start behavior

The wrappers use a global mutex (`Global\BuildGovernorMutex`) to ensure only one governor instance starts:

1. First wrapper acquires the mutex (5 s timeout)
2. Double-checks if governor is running (another process may have just started it)
3. If not, locates `Gov.Service.exe` by searching: `GOV_SERVICE_PATH` env var, sibling directories, Program Files, LocalAppData, and development project paths
4. Launches with `--background` flag
5. Waits up to 3 s for the service to become ready (polling every 200 ms)
6. Other wrappers block on the mutex, then connect to the now-running governor
7. Background mode: governor shuts down after 30 minutes of idle (no active leases)

The governor itself uses a separate instance mutex (`Global\BuildGovernorInstance`) to prevent duplicate services.

## GPU awareness

At startup (in non-quiet mode), the governor queries NVIDIA GPU status via `nvidia-smi` and displays VRAM usage, utilization, and temperature for each detected GPU. This is informational only — GPU metrics do not currently affect token budgeting.

## Safety features

- **Fail-safe**: if the governor is unavailable, wrappers run tools ungoverned with a yellow warning on stderr
- **Lease TTL**: if a wrapper crashes, tokens auto-reclaim after 30 minutes. A warning is logged at 10 minutes for long-running leases.
- **No deadlock**: timeouts on all pipe operations (2 s connect, 60 s acquire, 5 s release)
- **Auto-detection**: uses `vswhere` to find real cl.exe/link.exe, skipping the wrapper directory in PATH
- **Singleton**: global mutexes prevent both duplicate governor starts and duplicate governor instances
