---
title: Throttle Levels
description: Adaptive memory-based throttling and failure classification.
sidebar:
  order: 3
---

Build Governor monitors system commit charge in real time and adjusts its behavior across four throttle levels.

## Commit charge levels

| Commit Ratio | Level | Behavior |
|--------------|-------|----------|
| < 80% | Normal | Grant tokens immediately |
| 80-88% | Caution | Slower grants, 200 ms delay between retries |
| 88-92% | SoftStop | Significant delays, 500 ms between retries |
| > 92% | HardStop | Refuse new tokens entirely |

The key metric is **commit charge** (total committed virtual memory), not "free RAM." Commit charge is a better predictor of OOM because it accounts for all memory promises the OS has made, not just what's currently resident.

These thresholds are configurable via `TokenBudgetConfig` (defaults: `CautionRatio = 0.80`, `SoftStopRatio = 0.88`, `HardStopRatio = 0.92`).

## Monitoring loop

The governor samples memory every 500 ms. Each tick:

1. Reads system commit charge via `GlobalMemoryStatusEx`
2. Recalculates the token budget (total tokens, throttle level, recommended parallelism)
3. Checks for expired leases and reclaims their tokens

## Failure classification

When a build tool exits, the governor classifies the result using an evidence-weighted model. Multiple signals contribute to an OOM evidence score:

| Signal | Weight | Threshold |
|--------|--------|-----------|
| Commit ratio at exit >= 92% | 0.40 | HardStop zone |
| Commit ratio at exit >= 88% | 0.25 | SoftStop zone |
| Peak commit ratio during execution >= 95% | 0.30 | Near-limit |
| Process peak memory >= 2.5 GB | 0.20 | Heavy consumer |
| No compiler diagnostics in stderr | 0.20 | Silent death |
| Short duration (< 5 s) + high memory (>= 1.5 GB) | 0.15 | Fast crash |

The total evidence score determines the classification:

| Classification | Evidence | Meaning |
|---------------|----------|---------|
| **Success** | Exit code 0 | No analysis needed |
| **LikelyOOM** | >= 0.6 | High confidence memory exhaustion. Triggers retry with halved tokens. |
| **LikelyPagingDeath** | >= 0.4 | Moderate pressure. Triggers retry with halved tokens. |
| **NormalCompileError** | < 0.4, stderr has diagnostics | Standard compiler/linker error |
| **Unknown** | < 0.4, no diagnostics | Unable to determine cause |

## OOM diagnostics

On a likely OOM failure, you see an actionable diagnostic instead of a cryptic exit code:

```
BUILD FAILED: Memory Pressure Detected

Exit code: 1
System commit: 94% (45.2 GB / 48.0 GB)
Process peak:  3.1 GB
Evidence: commit ratio 94% at exit, no compiler diagnostics in stderr

Recommendation: Reduce parallelism
  CMAKE_BUILD_PARALLEL_LEVEL=4
  MSBuild: /m:4
  Ninja: -j4
```

For `LikelyPagingDeath`, the diagnostic says "Possible Paging Pressure" and notes that the build will retry with reduced parallelism.

This replaces the silent `CL.exe exited with code 1` that typically gives no indication of what went wrong.
