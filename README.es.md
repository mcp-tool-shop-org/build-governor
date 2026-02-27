<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.md">English</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
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

**Protección automática contra el agotamiento de memoria durante la compilación de C++. No se requieren pasos manuales.**

## ¿Por qué?

Las compilaciones paralelas de C++ (`cmake --parallel`, `msbuild /m`, `ninja -j`) pueden fácilmente agotar la memoria del sistema:

- Cada instancia de `cl.exe` puede usar de 1 a 4 GB de RAM (plantillas, LTCG, muchos encabezados)
- Los sistemas de compilación inician N trabajos paralelos y esperan lo mejor.
- Cuando se agota la RAM: el sistema se congela, o `CL.exe` finaliza con el código 1 (sin diagnóstico).
- La métrica clave es el **uso de memoria reservada**, no la "RAM libre".


Build Governor es un controlador ligero que **automáticamente** se sitúa entre su sistema de compilación y el compilador:

1. **Protección sin configuración** — Los wrappers inician automáticamente el controlador en la primera compilación.
2. **Concurrencia adaptativa** basada en el uso de memoria reservada, no en el número de trabajos.
3. **Fallo silencioso → diagnóstico útil** — "Se detectó presión de memoria, se recomienda -j4".
4. **Limitación automática** — las compilaciones se ralentizan en lugar de fallar.
5. **Mecanismo de seguridad** — si el controlador no está disponible, las herramientas se ejecutan sin control.

## Inicio rápido (Protección automática)

```powershell
# One-time setup (no admin required)
cd build-governor
.\scripts\enable-autostart.ps1

# That's it! All builds are now protected
cmake --build . --parallel 16
msbuild /m:16
ninja -j 8
```

Los wrappers hacen automáticamente lo siguiente:
- Inician el controlador si no se está ejecutando.
- Supervisan la memoria y limitan la velocidad cuando es necesario.
- Se detienen después de 30 minutos de inactividad.

## Alternativa: Servicio de Windows (para empresas)

Para una protección continua para todos los usuarios:

```powershell
# Requires Administrator
.\scripts\install-service.ps1
```

## Modo manual

Si prefiere un control explícito:

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

## Paquetes de NuGet

| Paquete | Versión | Descripción |
|---------|---------|-------------|
| [`Gov.Protocol`](https://www.nuget.org/packages/Gov.Protocol) | [![NuGet](https://img.shields.io/nuget/v/Gov.Protocol)](https://www.nuget.org/packages/Gov.Protocol) | DTOs de mensajes compartidos para la comunicación cliente-servicio a través de tuberías con nombre. |
| [`Gov.Common`](https://www.nuget.org/packages/Gov.Common) | [![NuGet](https://img.shields.io/nuget/v/Gov.Common)](https://www.nuget.org/packages/Gov.Common) | Métricas de memoria de Windows, clasificación de errores de falta de memoria (OOM), inicio automático del cliente. |

```xml
<!-- Gov.Protocol — message DTOs only (no Windows dependency) -->
<PackageReference Include="Gov.Protocol" Version="1.*" />

<!-- Gov.Common — memory metrics + OOM classifier (Windows) -->
<PackageReference Include="Gov.Common" Version="1.*" />
```

## Cómo funciona

### Flujo de protección automática

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

### Arquitectura

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

## Modelo de costo de tokens

| Acción | Tokens | Notas |
|--------|--------|-------|
| Compilación normal | 1 | Línea de base |
| Compilación intensiva (Boost/gRPC) | 2–4 | Intensiva en plantillas |
| Compilación con /GL | +3 | Generación de código LTCG |
| Enlace | 4 | Costo base de enlace |
| Enlace con /LTCG | 8–12 | LTCG completo |

## Niveles de limitación

| Relación de uso de memoria | Nivel | Comportamiento |
|--------------|-------|----------|
| < 80% | Normal | Otorga tokens inmediatamente |
| 80–88% | Precaución | Otorga más lentamente, con un retraso de 200 ms |
| 88–92% | SoftStop | Retrasos significativos, 500 ms |
| > 92% | HardStop | Niega nuevos tokens |

## Clasificación de fallos

Cuando una herramienta de compilación finaliza con un error, el controlador lo clasifica:

- **LikelyOOM**: Alta relación de uso de memoria + el proceso alcanzó un pico alto + no hay diagnósticos del compilador.
- **LikelyPagingDeath**: Señales de presión moderada.
- **NormalCompileError**: Diagnósticos del compilador presentes en stderr.
- **Unknown**: No se puede determinar.

En caso de falta de memoria (OOM), verá:
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

## Características de seguridad

- **Mecanismo de seguridad**: Si el controlador no está disponible, los wrappers ejecutan las herramientas sin control.
- **TTL de concesión**: Si el wrapper falla, los tokens se recuperan automáticamente después de 30 minutos.
- **Sin interbloqueo**: Tiempos de espera en todas las operaciones de tubería.
- **Detección automática de herramientas**: Utiliza vswhere para encontrar cl.exe/link.exe reales.

## Comandos de la línea de comandos

```powershell
# Run a governed build
gov run -- cmake --build . --parallel 16

# Check governor status
gov status

# Run without auto-starting governor
gov run --no-start -- ninja -j 8
```

## Variables de entorno

| Variable | Descripción |
|----------|-------------|
| `GOV_REAL_CL` | Ruta al cl.exe real (detectada automáticamente mediante vswhere) |
| `GOV_REAL_LINK` | Ruta al link.exe real (detectada automáticamente) |
| `GOV_ENABLED` | Establecida por `gov run` para indicar el modo controlado. |
| `GOV_SERVICE_PATH` | Ruta a Gov.Service.exe para el inicio automático. |
| `GOV_DEBUG` | Establecer en "1" para un registro detallado del inicio automático. |

## Estructura del proyecto

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

## Comportamiento de inicio automático

Los componentes utilizan un mutex global para asegurar que solo una instancia del "gobernador" se ejecute.
Cuando múltiples compiladores se inician simultáneamente:

1. El primer componente adquiere el mutex, verifica si el "gobernador" está en ejecución.
2. Si no, inicia `Gov.Service.exe --background`.
3. Los demás componentes esperan el mutex y luego se conectan al "gobernador" que ahora está en ejecución.
4. En modo de segundo plano: el "gobernador" se cierra automáticamente después de 30 minutos de inactividad.

## Seguridad y Alcance de Datos

Build Governor opera **completamente de forma local** en Windows: no realiza solicitudes de red, ni recopila datos de telemetría.

- **Datos accedidos:** Supervisa la carga del sistema y la memoria por proceso a través de las API de Windows. Se comunica con las herramientas de compilación a través de tuberías con nombre (solo comunicación entre procesos local). El servicio del "gobernador" se cierra automáticamente después de 30 minutos de inactividad.
- **Datos NO accedidos:** No realiza solicitudes de red. No recopila datos de telemetría. No almacena credenciales. No inspecciona archivos de compilación: el "gobernador" limita la concurrencia de procesos, pero no lee el código fuente ni los archivos binarios.
- **Permisos requeridos:** Usuario estándar para la línea de comandos y los componentes. Administrador solo para la instalación del servicio de Windows.

Consulte [SECURITY.md](SECURITY.md) para informar sobre vulnerabilidades.

---

## Cuadro de Evaluación

| Categoría | Puntuación |
|----------|-------|
| Seguridad | 10/10 |
| Manejo de Errores | 10/10 |
| Documentación para Operadores | 10/10 |
| Higiene en la Implementación | 10/10 |
| Identidad | 10/10 |
| **Overall** | **50/50** |

---

## Licencia

[MIT](LICENSE)

---

Creado por <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
