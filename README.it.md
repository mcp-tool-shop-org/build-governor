<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.md">English</a> | <a href="README.pt-BR.md">Português (BR)</a>
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

**Protezione automatica contro l'esaurimento della memoria durante la compilazione C++. Non sono necessari passaggi manuali.**

## Perché

Le compilazioni C++ parallele (`cmake --parallel`, `msbuild /m`, `ninja -j`) possono facilmente esaurire la memoria del sistema:

- Ogni istanza di `cl.exe` può utilizzare da 1 a 4 GB di RAM (modelli, LTCG, header pesanti)
- I sistemi di build avviano N processi paralleli e sperano nel meglio
- Quando la RAM si esaurisce: il sistema si blocca o `CL.exe` termina con codice 1 (nessuna informazione diagnostica)
- La metrica cruciale è il **Commit Charge**, non la "RAM libera"


Build Governor è un gestore leggero che si posiziona **automaticamente** tra il tuo sistema di build e il compilatore:

1. **Protezione senza configurazione** — I wrapper avviano automaticamente il gestore alla prima compilazione
2. **Concorrenza adattiva** basata sul commit charge, non sul numero di processi
3. **Errore silenzioso → diagnosi utile** — "Pressione di memoria rilevata, si consiglia -j4"
4. **Limitazione automatica** — le compilazioni rallentano invece di interrompersi
5. **Sicurezza** — se il gestore non è attivo, gli strumenti vengono eseguiti senza controllo

## Guida rapida (protezione automatica)

```powershell
# One-time setup (no admin required)
cd build-governor
.\scripts\enable-autostart.ps1

# That's it! All builds are now protected
cmake --build . --parallel 16
msbuild /m:16
ninja -j 8
```

I wrapper eseguono automaticamente le seguenti azioni:
- Avviano il gestore se non è in esecuzione
- Monitorano la memoria e limitano l'attività quando necessario
- Si arrestano dopo 30 minuti di inattività

## Alternativa: Servizio Windows (per aziende)

Per una protezione continua per tutti gli utenti:

```powershell
# Requires Administrator
.\scripts\install-service.ps1
```

## Modalità manuale

Se preferisci un controllo esplicito:

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

## Pacchetti NuGet

| Pacchetto | Versione | Descrizione |
|---------|---------|-------------|
| [`Gov.Protocol`](https://www.nuget.org/packages/Gov.Protocol) | [![NuGet](https://img.shields.io/nuget/v/Gov.Protocol)](https://www.nuget.org/packages/Gov.Protocol) | DTO di messaggi condivisi per la comunicazione client-servizio tramite pipe denominate. |
| [`Gov.Common`](https://www.nuget.org/packages/Gov.Common) | [![NuGet](https://img.shields.io/nuget/v/Gov.Common)](https://www.nuget.org/packages/Gov.Common) | Metriche di memoria di Windows, classificazione OOM, avvio automatico del client. |

```xml
<!-- Gov.Protocol — message DTOs only (no Windows dependency) -->
<PackageReference Include="Gov.Protocol" Version="1.*" />

<!-- Gov.Common — memory metrics + OOM classifier (Windows) -->
<PackageReference Include="Gov.Common" Version="1.*" />
```

## Come funziona

### Flusso di protezione automatica

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

### Architettura

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

## Modello di costo dei token

| Azione | Token | Note |
|--------|--------|-------|
| Compilazione normale | 1 | Valore di base |
| Compilazione pesante (Boost/gRPC) | 2–4 | Ricca di modelli |
| Compilazione con /GL | +3 | Generazione LTCG |
| Link | 4 | Costo di base del link |
| Link con /LTCG | 8–12 | LTCG completo |

## Livelli di limitazione

| Rapporto di commit | Livello | Comportamento |
|--------------|-------|----------|
| < 80% | Normale | Concede token immediatamente |
| 80–88% | Attenzione | Concessioni più lente, ritardo di 200 ms |
| 88–92% | SoftStop | Ritardi significativi, 500 ms |
| > 92% | HardStop | Rifiuta nuovi token |

## Classificazione degli errori

Quando uno strumento di build termina con un errore, il gestore lo classifica:

- **LikelyOOM**: Elevato rapporto di commit + processo con picco elevato + nessuna informazione diagnostica del compilatore
- **LikelyPagingDeath**: Segnali di pressione moderata
- **NormalCompileError**: Informazioni diagnostiche del compilatore presenti nell'output di errore standard
- **Unknown**: Impossibile determinare

In caso di OOM, si visualizza:
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

## Funzionalità di sicurezza

- **Sicurezza**: Se il gestore non è disponibile, i wrapper eseguono gli strumenti senza controllo
- **TTL della licenza**: Se il wrapper si arresta in modo anomalo, i token vengono automaticamente recuperati dopo 30 minuti
- **Nessun deadlock**: Timeout su tutte le operazioni delle pipe
- **Rilevamento automatico degli strumenti**: Utilizza vswhere per trovare le istanze reali di cl.exe/link.exe

## Comandi CLI

```powershell
# Run a governed build
gov run -- cmake --build . --parallel 16

# Check governor status
gov status

# Run without auto-starting governor
gov run --no-start -- ninja -j 8
```

## Variabili d'ambiente

| Variabile | Descrizione |
|----------|-------------|
| `GOV_REAL_CL` | Percorso dell'istanza reale di cl.exe (rilevato automaticamente tramite vswhere) |
| `GOV_REAL_LINK` | Percorso dell'istanza reale di link.exe (rilevato automaticamente) |
| `GOV_ENABLED` | Impostata da `gov run` per indicare la modalità gestita |
| `GOV_SERVICE_PATH` | Percorso di Gov.Service.exe per l'avvio automatico |
| `GOV_DEBUG` | Impostata su "1" per la registrazione dettagliata dell'avvio automatico |

## Struttura del progetto

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

## Comportamento di avvio automatico

I wrapper utilizzano un mutex globale per garantire che venga eseguita solo un'istanza del componente di controllo.
Quando più compilatori vengono avviati contemporaneamente:

1. Il primo wrapper acquisisce il mutex, verifica se il componente di controllo è in esecuzione.
2. Se non lo è, avvia `Gov.Service.exe --background`.
3. Gli altri wrapper attendono il mutex, quindi si connettono al componente di controllo ora in esecuzione.
4. In modalità background: il componente di controllo si arresta dopo 30 minuti di inattività.

## Sicurezza e ambito dei dati

Build Governor opera **esclusivamente localmente** su Windows: non effettua richieste di rete, non raccoglie dati di telemetria.

- **Dati accessibili:** Monitora l'utilizzo della memoria di sistema e la memoria per processo tramite le API di Windows. Comunica con gli strumenti di build tramite pipe con nome (solo IPC locale). Il servizio del componente di controllo si arresta automaticamente dopo 30 minuti di inattività.
- **Dati NON accessibili:** Nessuna richiesta di rete. Nessuna telemetria. Nessun salvataggio di credenziali. Nessuna ispezione degli artefatti di build: il componente di controllo limita la concorrenza dei processi, ma non legge il codice sorgente o i file binari.
- **Autorizzazioni richieste:** Utente standard per la riga di comando e i wrapper. Amministratore solo per l'installazione del servizio Windows.

Consultare il file [SECURITY.md](SECURITY.md) per la segnalazione di vulnerabilità.

---

## Valutazione

| Categoria | Punteggio |
|----------|-------|
| Sicurezza | 10/10 |
| Gestione degli errori | 10/10 |
| Documentazione per gli operatori | 10/10 |
| Qualità del codice | 10/10 |
| Identità | 10/10 |
| **Overall** | **50/50** |

---

## Licenza

[MIT](LICENSE)

---

Creato da <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
