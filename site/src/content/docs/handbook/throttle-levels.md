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
| 80-88% | Caution | Slower grants, 200 ms delay |
| 88-92% | SoftStop | Significant delays, 500 ms |
| > 92% | HardStop | Refuse new tokens entirely |

The key metric is **commit charge** (total committed virtual memory), not "free RAM." Commit charge is a better predictor of OOM because it accounts for all memory promises the OS has made, not just what's currently resident.

## Failure classification

When a build tool exits with an error, the governor classifies the failure:

| Classification | Meaning |
|---------------|---------|
| **LikelyOOM** | High commit ratio + process peaked high + no compiler diagnostics |
| **LikelyPagingDeath** | Moderate pressure signals |
| **NormalCompileError** | Compiler diagnostics present in stderr |
| **Unknown** | Can't determine the cause |

## OOM diagnostics

On a likely OOM failure, you see an actionable diagnostic instead of a cryptic exit code:

```
BUILD FAILED: Memory Pressure Detected

Exit code: 1
System commit: 94% (45.2 GB / 48.0 GB)
Process peak:  3.1 GB

Recommendation: Reduce parallelism
  CMAKE_BUILD_PARALLEL_LEVEL=4
  MSBuild: /m:4
  Ninja: -j4
```

This replaces the silent `CL.exe exited with code 1` that typically gives no indication of what went wrong.
