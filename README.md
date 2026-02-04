# Build Reliability Governor

Prevents C++ build systems from freezing your machine or failing silently due to memory exhaustion.

## The Problem

Parallel C++ builds (`cmake --parallel`, `msbuild /m`, `ninja -j`) can easily exhaust system memory:

- Each `cl.exe` instance can use 1-4 GB RAM (templates, LTCG, heavy headers)
- Build systems launch N parallel jobs and hope for the best
- When RAM exhausts: system freeze, or `CL.exe exited with code 1` (no diagnostic)
- The killer metric is **Commit Charge**, not "free RAM"

## The Solution

A lightweight governor that sits between your build system and the compiler:

1. **Adaptive concurrency** based on commit charge, not job count
2. **Silent failure → actionable diagnosis**: "Memory pressure detected, recommend -j4"
3. **Auto-throttling**: builds slow down instead of crashing
4. **Fail-safe**: if governor is down, tools run ungoverned

## Quick Start

```powershell
# 1. Build
cd build-governor
dotnet build -c Release
dotnet publish src/Gov.Wrapper.CL -c Release -o bin/wrappers
dotnet publish src/Gov.Wrapper.Link -c Release -o bin/wrappers
dotnet publish src/Gov.Cli -c Release -o bin/cli

# 2. Start governor (in one terminal)
dotnet run --project src/Gov.Service -c Release

# 3. Run your build (in another terminal)
bin/cli/gov.exe run -- cmake --build . --parallel 16
```

## How It Works

```
                    ┌─────────────────┐
                    │  Gov.Service    │
                    │  (Token Pool)   │
                    │  - Monitor RAM  │
                    │  - Grant tokens │
                    │  - Classify OOM │
                    └────────┬────────┘
                             │ Named Pipe
         ┌───────────────────┼───────────────────┐
         │                   │                   │
    ┌────┴────┐        ┌────┴────┐        ┌────┴────┐
    │ cl.exe  │        │ cl.exe  │        │link.exe │
    │ wrapper │        │ wrapper │        │ wrapper │
    └────┬────┘        └────┬────┘        └────┬────┘
         │                   │                   │
    ┌────┴────┐        ┌────┴────┐        ┌────┴────┐
    │ real    │        │ real    │        │ real    │
    │ cl.exe  │        │ cl.exe  │        │ link.exe│
    └─────────┘        └─────────┘        └─────────┘
```

## Token Cost Model

| Action | Tokens | Notes |
|--------|--------|-------|
| Normal compile | 1 | Baseline |
| Heavy compile (Boost/gRPC) | 2-4 | Template-heavy |
| Compile with /GL | +3 | LTCG codegen |
| Link | 4 | Base link cost |
| Link with /LTCG | 8-12 | Full LTCG |

## Throttle Levels

| Commit Ratio | Level | Behavior |
|--------------|-------|----------|
| < 80% | Normal | Grant tokens immediately |
| 80-88% | Caution | Slower grants, delay 200ms |
| 88-92% | SoftStop | Significant delays, 500ms |
| > 92% | HardStop | Refuse new tokens |

## Failure Classification

When a build tool exits with an error, the governor classifies it:

- **LikelyOOM**: High commit ratio + process peaked high + no compiler diagnostics
- **LikelyPagingDeath**: Moderate pressure signals
- **NormalCompileError**: Compiler diagnostics present in stderr
- **Unknown**: Can't determine

On OOM, you see:
```
╔══════════════════════════════════════════════════════════════════╗
║  BUILD FAILED: Memory Pressure Detected                          ║
╠══════════════════════════════════════════════════════════════════╣
║  Exit code: 1                                                    ║
║  System commit: 94% (45.2 GB / 48.0 GB)                          ║
║  Process peak:  3.1 GB                                           ║
╠══════════════════════════════════════════════════════════════════╣
║  Recommendation: Reduce parallelism                              ║
║    CMAKE_BUILD_PARALLEL_LEVEL=4                                  ║
║    MSBuild: /m:4                                                 ║
║    Ninja: -j4                                                    ║
╚══════════════════════════════════════════════════════════════════╝
```

## Safety Features

- **Fail-safe**: If governor unavailable, wrappers run tools ungoverned
- **Lease TTL**: If wrapper crashes, tokens auto-reclaim after 30 min
- **No deadlock**: Timeouts on all pipe operations
- **Tool auto-detection**: Uses vswhere to find real cl.exe/link.exe

## CLI Commands

```powershell
# Run a governed build
gov run -- cmake --build . --parallel 16

# Check governor status
gov status

# Run without auto-starting governor
gov run --no-start -- ninja -j 8
```

## Environment Variables

| Variable | Description |
|----------|-------------|
| `GOV_REAL_CL` | Path to real cl.exe (auto-detected) |
| `GOV_REAL_LINK` | Path to real link.exe (auto-detected) |
| `GOV_ENABLED` | Set by `gov run` to indicate governed mode |

## Project Structure

```
build-governor/
├── src/
│   ├── Gov.Protocol/    # Shared DTOs
│   ├── Gov.Common/      # Windows metrics, classifier
│   ├── Gov.Service/     # Background governor
│   ├── Gov.Wrapper.CL/  # cl.exe shim
│   ├── Gov.Wrapper.Link/# link.exe shim
│   └── Gov.Cli/         # `gov` command
├── bin/
│   ├── wrappers/        # Published shims
│   └── cli/             # Published CLI
└── gov-env.cmd          # Manual PATH setup
```

## License

MIT
