<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.md">English</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
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

**Protection automatique contre le manque de mémoire lors de la compilation C++. Aucune intervention manuelle requise.**

## Pourquoi

Les compilations C++ parallèles (`cmake --parallel`, `msbuild /m`, `ninja -j`) peuvent facilement épuiser la mémoire du système :

- Chaque instance de `cl.exe` peut utiliser de 1 à 4 Go de RAM (modèles, optimisation du code, nombreux fichiers d'en-tête)
- Les systèmes de compilation lancent N tâches parallèles et espèrent le meilleur.
- Lorsque la RAM est épuisée : le système se bloque, ou `CL.exe` se termine avec le code 1 (sans diagnostic).
- La métrique importante est la **mémoire allouée**, et non la "RAM libre".


Build Governor est un gestionnaire léger qui se place **automatiquement** entre votre système de compilation et le compilateur :

1. **Protection sans configuration** : Les wrappers démarrent automatiquement le gestionnaire lors de la première compilation.
2. **Concurrence adaptative** basée sur la mémoire allouée, et non sur le nombre de tâches.
3. **Échec silencieux → diagnostic précis** : "Pression de mémoire détectée, recommandation : -j4".
4. **Ralentissement automatique** : les compilations ralentissent au lieu de planter.
5. **Sécurité intégrée** : si le gestionnaire est indisponible, les outils fonctionnent sans contrôle.

## Démarrage rapide (Protection automatique)

```powershell
# One-time setup (no admin required)
cd build-governor
.\scripts\enable-autostart.ps1

# That's it! All builds are now protected
cmake --build . --parallel 16
msbuild /m:16
ninja -j 8
```

Les wrappers effectuent automatiquement les actions suivantes :
- Démarrent le gestionnaire s'il n'est pas en cours d'exécution.
- Surveillent la mémoire et limitent la vitesse si nécessaire.
- S'arrêtent après 30 minutes d'inactivité.

## Alternative : Service Windows (Entreprise)

Pour une protection permanente pour tous les utilisateurs :

```powershell
# Requires Administrator
.\scripts\install-service.ps1
```

## Mode manuel

Si vous préférez un contrôle explicite :

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

## Packages NuGet

| Package | Version | Description |
|---------|---------|-------------|
| [`Gov.Protocol`](https://www.nuget.org/packages/Gov.Protocol) | [![NuGet](https://img.shields.io/nuget/v/Gov.Protocol)](https://www.nuget.org/packages/Gov.Protocol) | DTOs de messages partagés pour la communication client-service via des pipes nommés. |
| [`Gov.Common`](https://www.nuget.org/packages/Gov.Common) | [![NuGet](https://img.shields.io/nuget/v/Gov.Common)](https://www.nuget.org/packages/Gov.Common) | Métriques de mémoire Windows, classification des erreurs de mémoire insuffisante, démarrage automatique du client. |

```xml
<!-- Gov.Protocol — message DTOs only (no Windows dependency) -->
<PackageReference Include="Gov.Protocol" Version="1.*" />

<!-- Gov.Common — memory metrics + OOM classifier (Windows) -->
<PackageReference Include="Gov.Common" Version="1.*" />
```

## Fonctionnement

### Flux de protection automatique

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

## Modèle de coût des jetons

| Action | Jetons | Notes |
|--------|--------|-------|
| Compilation normale | 1 | Valeur de référence |
| Compilation lourde (Boost/gRPC) | 2–4 | Utilisation intensive de modèles |
| Compilation avec /GL | +3 | Génération de code LTCG |
| Liaison | 4 | Coût de base de la liaison |
| Liaison avec /LTCG | 8–12 | LTCG complet |

## Niveaux de limitation

| Ratio de mémoire allouée | Niveau | Comportement |
|--------------|-------|----------|
| < 80% | Normal | Attribution immédiate des jetons |
| 80–88% | Attention | Attribution plus lente, délai de 200 ms |
| 88–92% | SoftStop | Délai important, 500 ms |
| > 92% | HardStop | Refus de nouveaux jetons |

## Classification des erreurs

Lorsqu'un outil de compilation se termine avec une erreur, le gestionnaire la classe :

- **LikelyOOM** : Ratio de mémoire allouée élevé + pic élevé de la consommation du processus + absence de diagnostics du compilateur.
- **LikelyPagingDeath** : Signaux de pression de mémoire modérés.
- **NormalCompileError** : Diagnostics du compilateur présents dans la sortie d'erreur standard.
- **Unknown** : Impossible de déterminer.

En cas d'erreur de mémoire insuffisante, vous verrez :
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

## Fonctionnalités de sécurité

- **Sécurité intégrée** : Si le gestionnaire est indisponible, les wrappers exécutent les outils sans contrôle.
- **TTL de la période de location** : Si le wrapper plante, les jetons sont automatiquement récupérés après 30 minutes.
- **Pas de blocage** : Délais de temporisation pour toutes les opérations de pipe.
- **Détection automatique des outils** : Utilise vswhere pour trouver les instances réelles de cl.exe/link.exe.

## Commandes CLI

```powershell
# Run a governed build
gov run -- cmake --build . --parallel 16

# Check governor status
gov status

# Run without auto-starting governor
gov run --no-start -- ninja -j 8
```

## Variables d'environnement

| Variable | Description |
|----------|-------------|
| `GOV_REAL_CL` | Chemin vers l'instance réelle de cl.exe (détecté automatiquement via vswhere). |
| `GOV_REAL_LINK` | Chemin vers l'instance réelle de link.exe (détecté automatiquement). |
| `GOV_ENABLED` | Définie par `gov run` pour indiquer le mode géré. |
| `GOV_SERVICE_PATH` | Chemin vers Gov.Service.exe pour le démarrage automatique. |
| `GOV_DEBUG` | Définie sur "1" pour activer la journalisation détaillée du démarrage automatique. |

## Structure du projet

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

## Comportement du démarrage automatique

Les programmes d'enveloppe utilisent un mutex global pour garantir qu'une seule instance du gestionnaire s'exécute.
Lorsque plusieurs compilateurs démarrent simultanément :

1. Le premier programme d'enveloppe acquiert le mutex, vérifie si le gestionnaire est en cours d'exécution.
2. Si ce n'est pas le cas, il démarre `Gov.Service.exe --background`.
3. Les autres programmes d'enveloppe attendent l'acquisition du mutex, puis se connectent au gestionnaire qui est maintenant en cours d'exécution.
4. En mode arrière-plan : le gestionnaire s'arrête après 30 minutes d'inactivité.

## Sécurité et portée des données

Build Governor fonctionne **entièrement localement** sur Windows : aucune requête réseau, aucune télémétrie.

- **Données accessibles :** Surveille la charge du système et la mémoire par processus via les API Windows. Communique avec les outils de construction via des canaux nommés (communication locale uniquement). Le service du gestionnaire s'arrête automatiquement après 30 minutes d'inactivité.
- **Données non accessibles :** Aucune requête réseau. Aucune télémétrie. Aucun stockage d'informations d'identification. Aucune inspection des artefacts de construction : le gestionnaire limite la concurrence des processus, mais ne lit pas le code source ni les binaires.
- **Autorisations requises :** Utilisateur standard pour l'interface de ligne de commande et les programmes d'enveloppe. Administrateur uniquement pour l'installation du service Windows.

Consultez [SECURITY.md](SECURITY.md) pour signaler les vulnérabilités.

---

## Tableau de bord

| Catégorie | Score |
|----------|-------|
| Sécurité | 10/10 |
| Gestion des erreurs | 10/10 |
| Documentation pour les utilisateurs | 10/10 |
| Qualité du code | 10/10 |
| Identité | 10/10 |
| **Overall** | **50/50** |

---

## Licence

[MIT](LICENSE)

---

Créé par <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
