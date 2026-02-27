<p align="center">
  <a href="README.md">English</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
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

**C++ ビルド時のメモリ不足による問題を自動的に保護します。手動での操作は不要です。**

## なぜ

並列 C++ ビルド (`cmake --parallel`, `msbuild /m`, `ninja -j`) は、システムメモリを容易に使い果たしてしまう可能性があります。

- `cl.exe` の各インスタンスは、1～4 GB の RAM を使用する可能性があります (テンプレート、LTCG、大量のヘッダーファイル)。
- ビルドシステムは、N 個の並列ジョブを開始し、最善を祈ります。
- RAM が不足すると、システムがフリーズするか、`CL.exe` がコード 1 で終了します (診断情報は表示されません)。
- 重要な指標は、**コミットメモリの使用量**であり、"空き RAM" ではありません。


Build Governor は、軽量な制御システムで、**自動的に** ビルドシステムとコンパイラの間に入り込みます。

1. **設定不要の保護機能**：ラッパーが、最初のビルド時に自動的に制御システムを起動します。
2. **適応的な並列処理**：ジョブ数ではなく、コミットメモリの使用量に基づいて調整します。
3. **エラーの診断**：「メモリ不足が検出されました。-j4 を推奨します」というメッセージを表示します。
4. **自動スロットリング**：ビルドがクラッシュする代わりに、速度が低下します。
5. **安全機能**：制御システムが停止した場合でも、ツールは制御なしで実行されます。

## クイックスタート (自動保護)

```powershell
# One-time setup (no admin required)
cd build-governor
.\scripts\enable-autostart.ps1

# That's it! All builds are now protected
cmake --build . --parallel 16
msbuild /m:16
ninja -j 8
```

ラッパーは、自動的に以下の操作を行います。
- 制御システムが実行されていない場合は、起動します。
- メモリを監視し、必要に応じてスロットリングを行います。
- 30 分間の非アクティブ状態の後、停止します。

## 代替手段：Windows サービス (エンタープライズ版)

すべてのユーザーに対して常に保護を提供する場合：

```powershell
# Requires Administrator
.\scripts\install-service.ps1
```

## 手動モード

明示的な制御を好む場合：

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

## NuGet パッケージ

| パッケージ | バージョン | 説明 |
|---------|---------|-------------|
| [`Gov.Protocol`](https://www.nuget.org/packages/Gov.Protocol) | [![NuGet](https://img.shields.io/nuget/v/Gov.Protocol)](https://www.nuget.org/packages/Gov.Protocol) | クライアントとサービス間の通信に使用する共有メッセージ DTO。 |
| [`Gov.Common`](https://www.nuget.org/packages/Gov.Common) | [![NuGet](https://img.shields.io/nuget/v/Gov.Common)](https://www.nuget.org/packages/Gov.Common) | Windows のメモリメトリクス、OOM (Out Of Memory) の分類、クライアントの自動起動機能。 |

```xml
<!-- Gov.Protocol — message DTOs only (no Windows dependency) -->
<PackageReference Include="Gov.Protocol" Version="1.*" />

<!-- Gov.Common — memory metrics + OOM classifier (Windows) -->
<PackageReference Include="Gov.Common" Version="1.*" />
```

## 仕組み

### 自動保護のフロー

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

### アーキテクチャ

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

## トークンコストモデル

| アクション | トークン | 備考 |
|--------|--------|-------|
| 通常のコンパイル | 1 | ベースライン |
| 負荷の高いコンパイル (Boost/gRPC) | 2–4 | テンプレートを多く使用 |
| `/GL` オプション付きのコンパイル | +3 | LTCG コード生成 |
| リンク | 4 | 基本リンクコスト |
| `/LTCG` オプション付きのリンク | 8–12 | フル LTCG |

## スロットリングレベル

| コミット比率 | レベル | 動作 |
|--------------|-------|----------|
| < 80% | 通常 | すぐにトークンを付与 |
| 80–88% | 注意 | 遅い付与、200 ms の遅延 |
| 88–92% | ソフトストップ | 大きな遅延、500 ms |
| > 92% | ハードストップ | 新しいトークンの付与を拒否 |

## エラー分類

ビルドツールがエラーで終了した場合、制御システムはそれを分類します。

- **LikelyOOM**: コミット比率が高い + プロセスがピーク時に高い値を示す + コンパイラからの診断情報がない
- **LikelyPagingDeath**: 中程度のメモリ圧迫の兆候
- **NormalCompileError**: コンパイラからの診断情報が stderr に表示される
- **Unknown**: 判別できない

OOM の場合、以下が表示されます。
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

## 安全機能

- **安全機能**: 制御システムが利用できない場合、ラッパーはツールを制御なしで実行します。
- **リース TTL**: ラッパーがクラッシュした場合、30 分後にトークンが自動的に回収されます。
- **デッドロックなし**: すべてのパイプ操作にタイムアウトを設定しています。
- **ツールの自動検出**: `vswhere` を使用して、実際の `cl.exe` / `link.exe` を検出します。

## CLI コマンド

```powershell
# Run a governed build
gov run -- cmake --build . --parallel 16

# Check governor status
gov status

# Run without auto-starting governor
gov run --no-start -- ninja -j 8
```

## 環境変数

| 変数 | 説明 |
|----------|-------------|
| `GOV_REAL_CL` | 実際の `cl.exe` へのパス (vswhere を使用して自動検出) |
| `GOV_REAL_LINK` | 実際の `link.exe` へのパス (自動検出) |
| `GOV_ENABLED` | `gov run` によって設定され、制御されたモードであることを示します。 |
| `GOV_SERVICE_PATH` | 自動起動用の `Gov.Service.exe` へのパス |
| `GOV_DEBUG` | 詳細な自動起動ログを有効にする場合は "1" に設定します。 |

## プロジェクト構造

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

## 自動起動の動作

ラッパーは、グローバルミューテックスを使用して、ガバナーのインスタンスが一つだけ実行されるようにしています。
複数のコンパイラが同時に起動した場合：

1. 最初のラッパーがミューテックスを取得し、ガバナーが実行中かどうかを確認します。
2. 実行中でない場合、`Gov.Service.exe --background` を起動します。
3. 他のラッパーはミューテックスを待機し、その後、起動したガバナーに接続します。
4. バックグラウンドモード：ガバナーは、30分間アイドル状態になるとシャットダウンします。

## セキュリティとデータ範囲

Build Governorは、Windows上で**完全にローカル**に動作します。ネットワークリクエストやテレメトリは一切ありません。

- **アクセスするデータ:** Windows APIを通じて、システム全体のコミット容量とプロセスごとのメモリを監視します。名前付きパイプ（ローカルIPCのみ）を介してビルドツールと通信します。ガバナーサービスは、30分間アイドル状態になると自動的にシャットダウンします。
- **アクセスしないデータ:** ネットワークリクエストは行いません。テレメトリも行いません。認証情報の保存も行いません。ビルド成果物の検査も行いません。ガバナーはプロセスの並列処理を制御しますが、ソースコードやバイナリを読み取ることはありません。
- **必要な権限:** CLIとラッパーを使用する場合は、通常ユーザー権限が必要です。Windowsサービスをインストールする場合は、管理者権限が必要です。

脆弱性に関する報告は、[SECURITY.md](SECURITY.md) を参照してください。

---

## 評価項目

| カテゴリ | 評価 |
|----------|-------|
| セキュリティ | 10/10 |
| エラー処理 | 10/10 |
| オペレーター向けドキュメント | 10/10 |
| 品質 | 10/10 |
| 信頼性 | 10/10 |
| **Overall** | **50/50** |

---

## ライセンス

[MIT](LICENSE)

---

作成者：<a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
