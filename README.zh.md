<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/build-governor/main/assets/logo-build-governor.png" alt="Build Governor" width="400">
</p>

<p align="center">
  <a href="https://github.com/mcp-tool-shop-org/build-governor/actions/workflows/publish.yml"><img src="https://github.com/mcp-tool-shop-org/build-governor/actions/workflows/publish.yml/badge.svg" alt="CI"></a>
  <a href="https://www.nuget.org/packages/Gov.Protocol"><img src="https://img.shields.io/nuget/v/Gov.Protocol" alt="NuGet Gov.Protocol"></a>
  <a href="https://www.nuget.org/packages/Gov.Common"><img src="https://img.shields.io/nuget/v/Gov.Common" alt="NuGet Gov.Common"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow" alt="MIT License"></a>
  <a href="https://mcp-tool-shop-org.github.io/build-governor/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page"></a>
</p>

**自动保护 C++ 构建过程，防止因内存耗尽而崩溃。无需手动操作。**

## 原因

并行 C++ 构建（`cmake --parallel`、`msbuild /m`、`ninja -j`）很容易耗尽系统内存：

- 每个 `cl.exe` 实例可能使用 1–4 GB 的 RAM（模板、LTCG、大量头文件）
- 构建系统启动 N 个并行任务，并希望一切顺利
- 当 RAM 耗尽时：系统冻结，或者出现 `CL.exe exited with code 1` 错误（没有诊断信息）
- 关键指标是 **已分配内存**，而不是“可用 RAM”


构建管理器是一个轻量级的管理器，它**自动**位于您的构建系统和编译器之间：

1. **零配置保护**：包装程序会在首次构建时自动启动管理器
2. **自适应并发**：基于已分配内存，而不是任务数量
3. **将错误转换为可操作的诊断信息**：“检测到内存压力，建议使用 -j4”
4. **自动降速**：构建速度变慢，而不是崩溃
5. **安全机制**：如果管理器不可用，则工具以未受限的方式运行

## 快速入门（自动保护）

```powershell
# One-time setup (no admin required)
cd build-governor
.\scripts\enable-autostart.ps1

# That's it! All builds are now protected
cmake --build . --parallel 16
msbuild /m:16
ninja -j 8
```

包装程序会自动：
- 如果管理器未运行，则启动管理器
- 监控内存，并在需要时进行降速
- 30 分钟无操作后自动停止

## 替代方案：Windows 服务（企业版）

为了在所有用户中提供始终在线的保护：

```powershell
# Requires Administrator
.\scripts\install-service.ps1
```

## 手动模式

如果您喜欢明确的控制：

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

## NuGet 包

| 包 | 版本 | 描述 |
| --------- | --------- | ------------- |
| [`Gov.Protocol`](https://www.nuget.org/packages/Gov.Protocol) | [![NuGet](https://img.shields.io/nuget/v/Gov.Protocol)](https://www.nuget.org/packages/Gov.Protocol) | 用于客户端和服务之间通过命名管道进行通信的共享消息 DTO。 |
| [`Gov.Common`](https://www.nuget.org/packages/Gov.Common) | [![NuGet](https://img.shields.io/nuget/v/Gov.Common)](https://www.nuget.org/packages/Gov.Common) | Windows 内存指标、OOM 分类、自动启动客户端。 |

```xml
<!-- Gov.Protocol — message DTOs only (no Windows dependency) -->
<PackageReference Include="Gov.Protocol" Version="1.*" />

<!-- Gov.Common — memory metrics + OOM classifier (Windows) -->
<PackageReference Include="Gov.Common" Version="1.*" />
```

## 工作原理

### 自动保护流程

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

### 架构

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

## 令牌成本模型

| 操作 | 令牌 | Notes |
| -------- | -------- | ------- |
| 正常编译 | 1 | 基线 |
| 高强度编译（Boost/gRPC） | 2–4 | 模板密集型 |
| 使用 /GL 编译 | +3 | LTCG 代码生成 |
| Link | 4 | 基本链接成本 |
| 使用 /LTCG 链接 | 8–12 | 完整 LTCG |

## 降速级别

| 已分配内存比率 | Level | 行为 |
| -------------- | ------- | ---------- |
| < 80% | 正常 | 立即授予令牌 |
| 80–88% | 警告 | 授予速度较慢，延迟 200 毫秒 |
| 88–92% | 软降速 | 明显延迟，500 毫秒 |
| > 92% | 硬降速 | 拒绝新的令牌 |

## 错误分类

当构建工具出现错误时退出，管理器会对其进行分类：

- **LikelyOOM**: 已分配内存比率高 + 进程峰值很高 + 编译器没有诊断信息
- **LikelyPagingDeath**: 出现中等程度的压力信号
- **NormalCompileError**: 编译器诊断信息出现在标准错误输出中
- **Unknown**: 无法确定

当发生 OOM 时，您会看到：
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

## 安全特性

- **安全机制**：如果管理器不可用，包装程序以未受限的方式运行工具
- **租约 TTL**：如果包装程序崩溃，令牌将在 30 分钟后自动回收
- **无死锁**：所有管道操作都有超时机制
- **工具自动检测**：使用 vswhere 查找真实的 cl.exe/link.exe

## 命令行

```powershell
# Run a governed build
gov run -- cmake --build . --parallel 16

# Check governor status
gov status

# Run without auto-starting governor
gov run --no-start -- ninja -j 8
```

## 环境变量

| 变量 | 描述 |
| ---------- | ------------- |
| `GOV_REAL_CL` | 真实 cl.exe 的路径（通过 vswhere 自动检测） |
| `GOV_REAL_LINK` | `link.exe` 文件的实际路径（自动检测）。 |
| `GOV_ENABLED` | 由 `gov run` 命令设置，用于指示受控模式。 |
| `GOV_SERVICE_PATH` | `Gov.Service.exe` 文件的路径，用于自动启动。 |
| `GOV_DEBUG` | 设置为 "1" 以启用详细的自动启动日志记录。 |

## 项目结构

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

## 自动启动行为

这些包装程序使用一个全局互斥锁，以确保只有一个治理程序实例运行。
当多个编译器同时启动时：

1. 第一个包装程序获取互斥锁，检查治理程序是否正在运行。
2. 如果没有运行，则启动 `Gov.Service.exe --background`。
3. 其他包装程序等待互斥锁，然后连接到现在正在运行的治理程序。
4. 后台模式：治理程序在 30 分钟无操作后自动关闭。

## 许可证

[MIT](LICENSE)
