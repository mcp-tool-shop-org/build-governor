# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 1.0.x   | Yes       |
| < 1.0   | No        |

## Reporting a Vulnerability

**Email:** 64996768+mcp-tool-shop@users.noreply.github.com

Please include:
- Description of the vulnerability
- Steps to reproduce
- Potential impact

**Response timeline:**
- Acknowledgment: within 48 hours
- Assessment: within 7 days
- Fix (if confirmed): within 30 days

## Scope

Build Governor is a **Windows build reliability tool** that prevents memory exhaustion during compilation.
- **Data accessed:** Monitors system commit charge and per-process memory via Windows APIs. Communicates with build tools (cl.exe, link.exe) via named pipes (local IPC only). Reads/writes token budgets and throttle state in-process. Governor service auto-shuts down after 30 minutes idle.
- **Data NOT accessed:** No network requests. No telemetry. No cloud services. No credential storage. No build artifact inspection — governor throttles process concurrency, it doesn't read source code or binaries.
- **Permissions required:** Standard user for CLI and wrappers. Administrator for Windows Service installation only. Named pipe creation on localhost.
