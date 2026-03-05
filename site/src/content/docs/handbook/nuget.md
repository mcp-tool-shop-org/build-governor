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

Use this when you want to:
- Build a custom wrapper for a different build tool
- Communicate with the governor service programmatically
- Integrate governor awareness into your own build tooling

## Gov.Common

Windows memory metrics, OOM classification, and auto-start client. Depends on Windows APIs.

```xml
<PackageReference Include="Gov.Common" Version="1.*" />
```

Use this when you want to:
- Read system commit charge and memory pressure levels
- Classify build failures as OOM vs normal errors
- Auto-start the governor service from your own tools

## Security and data scope

Build Governor operates entirely locally on Windows:

| Aspect | Detail |
|--------|--------|
| Data accessed | System commit charge, per-process memory via Windows APIs, named pipe IPC |
| Data NOT accessed | No network requests, no telemetry, no credential storage |
| Permissions | Standard user for CLI/wrappers, Administrator for Service installation only |

See [SECURITY.md](https://github.com/mcp-tool-shop-org/build-governor/blob/main/SECURITY.md) for vulnerability reporting.
