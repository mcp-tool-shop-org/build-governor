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
2. The wrapper checks if the governor service is running
3. If not, it auto-starts `Gov.Service.exe` in the background
4. The wrapper requests tokens from the governor
5. When granted, it runs the real `cl.exe`
6. After completion, it releases the tokens

## Token cost model

Different operations consume different numbers of tokens, reflecting their real-world memory profiles:

| Action | Tokens | Notes |
|--------|--------|-------|
| Normal compile | 1 | Baseline |
| Heavy compile (Boost/gRPC) | 2-4 | Template-heavy |
| Compile with /GL | +3 | LTCG codegen |
| Link | 4 | Base link cost |
| Link with /LTCG | 8-12 | Full LTCG |

## Architecture

The system consists of three components communicating over named pipes:

- **Gov.Service** — background token pool that monitors RAM and grants/refuses tokens
- **Gov.Wrapper.CL** — shim that replaces `cl.exe` in PATH, requests tokens before compiling
- **Gov.Wrapper.Link** — shim that replaces `link.exe` in PATH

## Auto-start behavior

The wrappers use a global mutex to ensure only one governor instance runs:

1. First wrapper acquires mutex, checks if governor is running
2. If not, starts `Gov.Service.exe --background`
3. Other wrappers wait on the mutex, then connect to the now-running governor
4. Background mode: governor shuts down after 30 minutes idle

## Safety features

- **Fail-safe**: if the governor is unavailable, wrappers run tools ungoverned
- **Lease TTL**: if a wrapper crashes, tokens auto-reclaim after 30 minutes
- **No deadlock**: timeouts on all pipe operations
- **Auto-detection**: uses `vswhere` to find real cl.exe/link.exe
