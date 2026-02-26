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

**Proteção automática contra o esgotamento de memória durante a compilação de C++. Não são necessários passos manuais.**

## Por que?

Compilações paralelas de C++ (`cmake --parallel`, `msbuild /m`, `ninja -j`) podem facilmente esgotar a memória do sistema:

- Cada instância de `cl.exe` pode usar de 1 a 4 GB de RAM (templates, LTCG, muitos arquivos de cabeçalho)
- Os sistemas de compilação iniciam N tarefas paralelas e esperam o melhor
- Quando a RAM se esgota: o sistema trava ou `CL.exe` termina com o código 1 (sem diagnóstico)
- A métrica crucial é o **uso de memória alocada**, não a "RAM livre"


O Build Governor é um controlador leve que **automaticamente** se posiciona entre seu sistema de compilação e o compilador:

1. **Proteção sem configuração** — Os wrappers iniciam automaticamente o controlador na primeira compilação
2. **Concorrência adaptativa** baseada no uso de memória alocada, e não no número de tarefas
3. **Falha silenciosa → diagnóstico acionável** — "Pressão de memória detectada, recomenda-se usar -j4"
4. **Redução automática de velocidade** — as compilações diminuem a velocidade em vez de falhar
5. **Segurança** — se o controlador estiver inativo, as ferramentas são executadas sem controle

## Início Rápido (Proteção Automática)

```powershell
# One-time setup (no admin required)
cd build-governor
.\scripts\enable-autostart.ps1

# That's it! All builds are now protected
cmake --build . --parallel 16
msbuild /m:16
ninja -j 8
```

Os wrappers fazem automaticamente:
- Iniciam o controlador se ele não estiver em execução
- Monitoram a memória e reduzem a velocidade quando necessário
- Desligam após 30 minutos de inatividade

## Alternativa: Serviço do Windows (para empresas)

Para proteção contínua em todos os usuários:

```powershell
# Requires Administrator
.\scripts\install-service.ps1
```

## Modo Manual

Se você preferir controle explícito:

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

## Pacotes NuGet

| Pacote | Versão | Descrição |
| --------- | --------- | ------------- |
| [`Gov.Protocol`](https://www.nuget.org/packages/Gov.Protocol) | [![NuGet](https://img.shields.io/nuget/v/Gov.Protocol)](https://www.nuget.org/packages/Gov.Protocol) | DTOs de mensagens compartilhadas para comunicação cliente-servidor via pipes nomeados. |
| [`Gov.Common`](https://www.nuget.org/packages/Gov.Common) | [![NuGet](https://img.shields.io/nuget/v/Gov.Common)](https://www.nuget.org/packages/Gov.Common) | Métricas de memória do Windows, classificação de OOM, inicialização automática do cliente. |

```xml
<!-- Gov.Protocol — message DTOs only (no Windows dependency) -->
<PackageReference Include="Gov.Protocol" Version="1.*" />

<!-- Gov.Common — memory metrics + OOM classifier (Windows) -->
<PackageReference Include="Gov.Common" Version="1.*" />
```

## Como Funciona

### Fluxo de Proteção Automática

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

### Arquitetura

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

## Modelo de Custo de Tokens

| Ação | Tokens | Notes |
| -------- | -------- | ------- |
| Compilação normal | 1 | Baseline (Referência) |
| Compilação pesada (Boost/gRPC) | 2–4 | Intensiva em templates |
| Compilação com /GL | +3 | Geração de código LTCG |
| Link | 4 | Custo básico de link |
| Link com /LTCG | 8–12 | LTCG completo |

## Níveis de Redução de Velocidade

| Taxa de Uso de Memória | Level | Comportamento |
| -------------- | ------- | ---------- |
| < 80% | Normal | Concede tokens imediatamente |
| 80–88% | Cuidado | Concessão mais lenta, atraso de 200 ms |
| 88–92% | SoftStop | Atrasos significativos, 500 ms |
| > 92% | HardStop | Recusa de novos tokens |

## Classificação de Falha

Quando uma ferramenta de compilação termina com um erro, o controlador a classifica:

- **LikelyOOM**: Alta taxa de uso de memória + o processo atingiu um pico alto + sem diagnósticos do compilador
- **LikelyPagingDeath**: Sinais de pressão moderada
- **NormalCompileError**: Diagnósticos do compilador presentes no stderr
- **Unknown**: Não é possível determinar

Em caso de OOM, você verá:
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

## Recursos de Segurança

- **Segurança**: Se o controlador estiver indisponível, os wrappers executam as ferramentas sem controle
- **TTL do Lease**: Se o wrapper falhar, os tokens são automaticamente recuperados após 30 minutos
- **Sem deadlock**: Timeouts em todas as operações de pipe
- **Detecção automática de ferramentas**: Usa o vswhere para encontrar o cl.exe/link.exe real

## Comandos da Linha de Comando

```powershell
# Run a governed build
gov run -- cmake --build . --parallel 16

# Check governor status
gov status

# Run without auto-starting governor
gov run --no-start -- ninja -j 8
```

## Variáveis de Ambiente

| Variável | Descrição |
| ---------- | ------------- |
| `GOV_REAL_CL` | Caminho para o cl.exe real (detectado automaticamente via vswhere) |
| `GOV_REAL_LINK` | Caminho para o arquivo link.exe (detectado automaticamente). |
| `GOV_ENABLED` | Definido por `gov run` para indicar o modo gerenciado. |
| `GOV_SERVICE_PATH` | Caminho para o arquivo Gov.Service.exe para inicialização automática. |
| `GOV_DEBUG` | Defina como "1" para registro detalhado da inicialização automática. |

## Estrutura do Projeto

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

## Comportamento da Inicialização Automática

Os wrappers utilizam um mutex global para garantir que apenas uma instância do gerenciador esteja em execução.
Quando vários compiladores são iniciados simultaneamente:

1. O primeiro wrapper adquire o mutex, verifica se o gerenciador está em execução.
2. Se não estiver, inicia o `Gov.Service.exe --background`.
3. Outros wrappers aguardam o mutex e, em seguida, conectam-se ao gerenciador que está em execução.
4. Modo de fundo: o gerenciador é desligado após 30 minutos de inatividade.

## Licença

[MIT](LICENSE)
