---
title: Beginners
description: New to Build Governor? Start here for a plain-language walkthrough.
sidebar:
  order: 99
---

## What does this tool do?

Build Governor prevents your computer from freezing or crashing during large C++ builds. When you compile a big C++ project, your build system (CMake, MSBuild, Ninja) launches many compiler processes in parallel. Each one can consume 1-4 GB of RAM. If too many run at once, Windows runs out of memory and either freezes the system or silently kills the compiler with an unhelpful "exited with code 1" error.

Build Governor acts as a traffic controller. It sits between your build system and the compiler, granting permission to run based on how much memory is actually available. When memory gets tight, it slows down new compilations instead of letting the system crash. If a build does fail due to memory, it tells you exactly what happened and recommends a safer parallelism level.

## Who is it for?

Build Governor is for anyone who compiles C++ on Windows and has experienced:

- System freezes during large builds
- Random `CL.exe exited with code 1` errors with no explanation
- Having to manually tune `-j` flags through trial and error
- Builds that work on a beefy workstation but fail on a laptop
- Teams where different machines have different amounts of RAM and need different parallelism settings

It is especially useful for projects that use template-heavy libraries (Boost, gRPC, Abseil), link-time code generation (LTCG), or whole-program optimization (/GL), all of which dramatically increase per-process memory consumption.

## When should I reach for it?

Reach for Build Governor when:

- **Your builds crash intermittently** — If builds sometimes fail with exit code 1 and no compiler error message, memory exhaustion is the likely cause.
- **You run parallel builds on constrained hardware** — Laptops, CI runners, or shared build machines with limited RAM.
- **You are tired of tuning `-j` flags** — Build Governor automatically calculates the right parallelism level based on actual available memory, so you can safely use `-j 16` and let it throttle as needed.
- **You use LTCG or heavy template libraries** — These dramatically increase memory use per compilation unit. Build Governor assigns higher token costs to these operations.

You do not need Build Governor if your builds always run with `-j 1` or if you have far more RAM than your build could ever use.

## Key concepts

**Commit Charge** — The total amount of virtual memory the OS has promised to all processes. This is the metric Build Governor watches, not "free RAM." When commit charge approaches the commit limit (physical RAM + page file), the OS must either page aggressively (causing freezes) or refuse allocations (causing crashes).

**Tokens** — Build Governor uses a token pool to control concurrency. Each compiler or linker invocation must acquire tokens before running. The number of tokens required depends on the operation (a normal compile costs 1 token; a full LTCG link costs up to 12). The total token budget is calculated from available commit headroom: roughly 1 token per 2 GB of free commit space, minus an 8 GB safety reserve.

**Throttle Levels** — Based on the current commit ratio:
- **Normal** (< 80%): tokens granted immediately
- **Caution** (80-88%): 200 ms delay between grant attempts
- **SoftStop** (88-92%): 500 ms delay
- **HardStop** (> 92%): no new tokens granted

**Wrappers** — Build Governor works by placing shim executables named `cl.exe` and `link.exe` earlier in your PATH than the real compiler. When your build system calls `cl.exe`, it actually runs the wrapper, which requests tokens, runs the real compiler, and reports results back.

**Leases** — When a wrapper acquires tokens, it gets a lease with a 30-minute timeout. If the wrapper process crashes, the lease expires and the tokens are automatically reclaimed.

**Failure Classification** — After each tool exits, the governor analyzes commit ratio, process peak memory, stderr output, and execution duration to classify the failure as OOM, paging death, normal compile error, or unknown. OOM and paging classifications trigger actionable diagnostics with recommended parallelism.

## First steps

### 1. Prerequisites

- Windows (Build Governor uses Windows-specific memory APIs)
- .NET 9.0 SDK (for building from source)
- Visual Studio Build Tools with the C++ workload installed

### 2. Clone and build

```powershell
git clone https://github.com/mcp-tool-shop-org/build-governor.git
cd build-governor
dotnet build -c Release
```

### 3. Set up automatic protection

```powershell
.\scripts\enable-autostart.ps1
```

This publishes the wrappers and configures your PATH so that all builds are automatically governed. No admin privileges required.

### 4. Build your project as usual

```powershell
cmake --build . --parallel 16
```

The wrappers handle everything: starting the governor, requesting tokens, monitoring memory, and shutting down after 30 minutes of inactivity.

### 5. Check status

```powershell
gov status
```

This shows the current token pool, active leases, system commit charge, and recommended parallelism.

## Common mistakes

**Forgetting the double dash in `gov run`** — The `--` separator tells `gov` where your command starts. Without it, `gov` cannot tell which arguments are yours and which are its own.
```powershell
# Wrong:
gov run cmake --build . --parallel 16

# Correct:
gov run -- cmake --build . --parallel 16
```

**Setting parallelism too low "just in case"** — With Build Governor, you can safely set high parallelism (`-j 16` or higher). The governor dynamically throttles based on actual memory pressure. Setting `-j 2` defeats the purpose because you lose the speed benefit on machines that have plenty of RAM.

**Running multiple governor instances** — The governor uses a global mutex to prevent this, but if you manually start `Gov.Service.exe` in multiple terminals, the second instance exits silently (exit code 0). Use `gov status` to verify the governor is running rather than starting it manually.

**Ignoring the recommended parallelism** — When a build fails with a "Memory Pressure Detected" diagnostic, the recommended `-j` value is calculated from your actual commit headroom. Use it as a starting point if you want to bypass the governor for a quick retry.

**Not building the wrappers** — If you see "Wrapper directory not found," you need to publish the wrapper projects first:
```powershell
dotnet publish src/Gov.Wrapper.CL -c Release -o bin/wrappers
dotnet publish src/Gov.Wrapper.Link -c Release -o bin/wrappers
```

## FAQ

**Does Build Governor slow down my builds?**
Only when memory is actually tight. Under normal conditions (commit charge < 80%), tokens are granted instantly with no measurable overhead. The named pipe communication adds microseconds per compiler invocation. When throttling kicks in, builds slow down by the delay amount (200-500 ms per new compilation), which is far better than a system freeze or a wasted build.

**Does it work with non-MSVC compilers?**
Currently, Build Governor provides wrappers for `cl.exe` (MSVC compiler) and `link.exe` (MSVC linker) only. If you use GCC or Clang on Windows, you could build a custom wrapper using the `Gov.Protocol` and `Gov.Common` NuGet packages. The token pool and memory monitoring are compiler-agnostic.

**What happens if the governor crashes?**
The wrappers are fail-safe: if they cannot connect to the governor, they run the real compiler ungoverned and print a yellow warning to stderr. Your build continues without protection rather than failing.

**Does it need admin privileges?**
No, for normal use. The CLI, wrappers, and auto-start all work as a standard user. Admin is only required for installing the Windows Service (which provides always-on protection across all user sessions).

**Does it send any data over the network?**
No. Build Governor is entirely local. It uses Windows APIs for memory metrics, `nvidia-smi` for optional GPU info, and named pipes for local IPC. There are no network requests, no telemetry, and no cloud dependencies.

**Can I use it on CI/CD runners?**
Yes. The auto-start mechanism is particularly useful on CI runners where you want protection without service installation. Just run `enable-autostart.ps1` as part of your agent setup. The governor starts with the first build and shuts down after 30 minutes of idle.

**What .NET version do I need?**
.NET 9.0. The solution targets `net9.0` (for Gov.Protocol) and `net9.0-windows` (for Windows-specific components).
