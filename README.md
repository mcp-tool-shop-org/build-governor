<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/build-governor/readme.png" alt="Build Governor" width="400">
</p>

<p align="center">
  <a href="https://github.com/mcp-tool-shop-org/build-governor/actions/workflows/publish.yml"><img src="https://github.com/mcp-tool-shop-org/build-governor/actions/workflows/publish.yml/badge.svg" alt="CI"></a>
  <a href="https://www.nuget.org/packages/Gov.Protocol"><img src="https://img.shields.io/nuget/v/Gov.Protocol" alt="NuGet Gov.Protocol"></a>
  <a href="https://www.nuget.org/packages/Gov.Common"><img src="https://img.shields.io/nuget/v/Gov.Common" alt="NuGet Gov.Common"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow" alt="MIT License"></a>
  <a href="https://mcp-tool-shop-org.github.io/build-governor/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page"></a>
</p>

**Automatic protection against C++ build memory exhaustion. No manual steps required.**

## Why

Parallel C++ builds (`cmake --parallel`, `msbuild /m`, `ninja -j`) can easily exhaust system memory:

- Each `cl.exe` instance can use 1–4 GB RAM (templates, LTCG, heavy headers)
- Build systems launch N parallel jobs and hope for the best
- When RAM exhausts: system freeze, or `CL.exe exited with code 1` (no diagnostic)
- The killer metric is **Commit Charge**, not "free RAM"


Build Governor is a lightweight governor that **automatically** sits between your build system and the compiler:

1. **Zero-config protection** — Wrappers auto-start governor on first build
2. **Adaptive concurrency** based on commit charge, not job count
3. **Silent failure → actionable diagnosis** — "Memory pressure detected, recommend -j4"
4. **Auto-throttling** — builds slow down instead of crashing
5. **Fail-safe** — if governor is down, tools run ungoverned

## Quick Start (Automatic Protection)

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
- Shut down after 30 min of inactivity

## Alternative: Windows Service (Enterprise)

For always-on protection across all users:

```powershell
# Requires Administrator
.\scripts\install-service.ps1
```

## Manual Mode

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

## NuGet Packages

| Package | Version | Description |
|---------|---------|-------------|
| [`Gov.Protocol`](https://www.nuget.org/packages/Gov.Protocol) | [![NuGet](https://img.shields.io/nuget/v/Gov.Protocol)](https://www.nuget.org/packages/Gov.Protocol) | Shared message DTOs for client–service communication over named pipes. |
| [`Gov.Common`](https://www.nuget.org/packages/Gov.Common) | [![NuGet](https://img.shields.io/nuget/v/Gov.Common)](https://www.nuget.org/packages/Gov.Common) | Windows memory metrics, OOM classification, auto-start client. |

```xml
<!-- Gov.Protocol — message DTOs only (no Windows dependency) -->
<PackageReference Include="Gov.Protocol" Version="1.*" />

<!-- Gov.Common — memory metrics + OOM classifier (Windows) -->
<PackageReference Include="Gov.Common" Version="1.*" />
```

## How It Works

### Automatic Protection Flow

```
  cmake --build .
        │
        ▼
    ┌───────────┐
    │  cl.exe   │ ← Actually the wrapper (in PATH)
    │  wrapper  │
    └─────┬─────┘
          │
          ▼
  ┌───────────────────┐
  │ Governor running? │
  └─────────┬─────────┘
       No   │   Yes
            │
     ┌──────┴──────┐
     ▼             ▼
  Auto-start    Connect
  Governor      directly
     │             │
     └──────┬──────┘
            ▼
    Request tokens
            │
            ▼
    Run real cl.exe
            │
            ▼
    Release tokens
```

### Architecture

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
| Heavy compile (Boost/gRPC) | 2–4 | Template-heavy |
| Compile with /GL | +3 | LTCG codegen |
| Link | 4 | Base link cost |
| Link with /LTCG | 8–12 | Full LTCG |

## Throttle Levels

| Commit Ratio | Level | Behavior |
|--------------|-------|----------|
| < 80% | Normal | Grant tokens immediately |
| 80–88% | Caution | Slower grants, delay 200 ms |
| 88–92% | SoftStop | Significant delays, 500 ms |
| > 92% | HardStop | Refuse new tokens |

## Failure Classification

When a build tool exits, the governor classifies the result using an evidence-weighted model (commit ratio, process peak memory, stderr diagnostics, execution duration):

- **Success**: Exit code 0 — no further analysis
- **LikelyOOM**: High commit ratio + process peaked high + no compiler diagnostics (evidence >= 0.6)
- **LikelyPagingDeath**: Moderate pressure signals (evidence >= 0.4)
- **NormalCompileError**: Compiler diagnostics present in stderr
- **Unknown**: Can't determine the cause

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
- **Lease TTL**: If wrapper crashes, tokens auto-reclaim after 30 minutes (warning logged at 10 minutes)
- **No deadlock**: Timeouts on all pipe operations (2 s connect, 60 s acquire, 5 s release)
- **Tool auto-detection**: Uses vswhere to find real cl.exe/link.exe
- **Singleton**: Global mutex prevents multiple governor instances
- **GPU awareness**: Displays NVIDIA GPU memory/utilization at startup via nvidia-smi

## CLI Commands

```powershell
# Run a governed build
gov run -- cmake --build . --parallel 16

# Check governor status
gov status

# Run without auto-starting governor
gov run --no-start -- ninja -j 8
```

## Token Budget Defaults

| Parameter | Default | Description |
|-----------|---------|-------------|
| GbPerToken | 2.0 | GB of available commit per token |
| SafetyReserveGb | 8.0 | GB always kept free |
| MinTokens | 1 | Floor even under pressure |
| MaxTokens | 32 | Ceiling to prevent runaway |

## Environment Variables

| Variable | Description |
|----------|-------------|
| `GOV_REAL_CL` | Path to real cl.exe (auto-detected via vswhere) |
| `GOV_REAL_LINK` | Path to real link.exe (auto-detected) |
| `GOV_ENABLED` | Set by `gov run` to indicate governed mode |
| `GOV_SERVICE_PATH` | Path to Gov.Service.exe for auto-start |
| `GOV_DEBUG` | Set to "1" for verbose auto-start and idle-timeout logging |

## Project Structure

```
build-governor/
├── src/
│   ├── Gov.Protocol/    # Shared DTOs
│   ├── Gov.Common/      # Windows metrics, classifier, auto-start
│   ├── Gov.Service/     # Background governor (supports --background)
│   ├── Gov.Wrapper.CL/  # cl.exe shim (auto-starts governor)
│   ├── Gov.Wrapper.Link/# link.exe shim
│   └── Gov.Cli/         # `gov` command
├── scripts/
│   ├── enable-autostart.ps1  # User setup (no admin)
│   ├── install-service.ps1   # Windows Service (admin)
│   └── uninstall-service.ps1 # Remove service
├── bin/
│   ├── wrappers/        # Published shims
│   ├── service/         # Published service
│   └── cli/             # Published CLI
└── gov-env.cmd          # Manual PATH setup
```

## Auto-Start Behavior

The wrappers use a global mutex to ensure only one governor instance runs.
When multiple compilers start simultaneously:

1. First wrapper acquires mutex, checks if governor running
2. If not, starts `Gov.Service.exe --background`
3. Other wrappers wait on mutex, then connect to now-running governor
4. Background mode: governor shuts down after 30 min idle

## Security & Data Scope

Build Governor operates **entirely locally** on Windows — no network requests, no telemetry.

- **Data accessed:** Monitors system commit charge and per-process memory via Windows APIs. Communicates with build tools via named pipes (local IPC only). Governor service auto-shuts down after 30 minutes idle.
- **Data NOT accessed:** No network requests. No telemetry. No credential storage. No build artifact inspection — governor throttles process concurrency, it doesn't read source code or binaries.
- **Permissions required:** Standard user for CLI and wrappers. Administrator for Windows Service installation only.

See [SECURITY.md](SECURITY.md) for vulnerability reporting.

---

## Scorecard

| Category | Score |
|----------|-------|
| Security | 10/10 |
| Error Handling | 10/10 |
| Operator Docs | 10/10 |
| Shipping Hygiene | 10/10 |
| Identity | 10/10 |
| **Overall** | **50/50** |

---

## License

[MIT](LICENSE)

---

Built by <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
