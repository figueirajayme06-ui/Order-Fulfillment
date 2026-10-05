# Local development runbook

## Supported workflow

Use the scripts in `build/dev` for normal local development. They start the dependencies in a known configuration and track the processes they create.

From the repository root:

```powershell
.\build\dev\check.ps1
.\build\dev\start.ps1
kjhkjnk
.\build\dev\stop.ps1
```

The starter selects Docker SQL when available, otherwise LocalDB; publishes the SQL project unless told not to; starts Azurite Blob and Table services; starts `OF.WebApp`; and starts the Vite frontend.

Useful options:

```powershell
# Use a known database mode.
.\build\dev\start.ps1 -DatabaseMode localdb
.\build\dev\start.ps1 -DatabaseMode docker

# Reuse an already published local schema.
.\build\dev\start.ps1 -SkipDbPublish

# Reuse installed frontend packages.
.\build\dev\start.ps1 -NoInstall
```

## Prerequisites

- .NET SDK 10 (the repository pins `10.0.100` in `global.json`).
- Node.js and npm.
- Visual Studio Build Tools or Visual Studio with the SQL Server Data Tools (SSDT) / Data storage and processing components, plus `sqllocaldb` and `sqlpackage`, for the database project. SSMS is optional and does not supply the SSDT build targets.
- Docker if choosing Docker database mode.
- `azurite-blob.cmd` and `azurite-table.cmd`; the start script launches both services.

Do not put connection strings, service-bus keys, or personal credentials in tracked settings files.

## Local endpoints and logs

| Service | Default endpoint | Notes |
| --- | --- | --- |
| Frontend | `http://localhost:5174` | Vite uses HTTPS only when local certificate files are present in `src/OF.Frontend/certs`. |
| Backend | `https://localhost:7200` and `http://localhost:5200` | Vite proxies `/api` to `https://localhost:7200`. |
| Azurite Blob | `http://127.0.0.1:10000` | Started by the dev script. |
| Azurite Table | `http://127.0.0.1:10002` | Started by the dev script. |

Logs, PIDs, and selected database mode are stored in `build/dev/.state`. Each launch writes timestamped stdout/stderr logs. The `backend.stdout.log`, `backend.stderr.log`, `frontend.stdout.log`, and `frontend.stderr.log` state files identify the latest logs. Use `stop.ps1` before starting services manually; it stops each tracked process tree so child processes do not retain log handles.

## Authentication and data configuration

### How authentication works

- In production, Azure App Service EasyAuth supplies identity headers.
- In development, `OF.WebApp` falls back to `LocalUserEmail` when those headers are absent.
- That email must identify a valid Order Fulfillment user in the selected data source. Otherwise `/api/auth/me` returns `401`, and the frontend displays **Failed to authenticate**.

For local-only development, set the email through user secrets rather than source-controlled settings:

```powershell
dotnet user-secrets set LocalUserEmail "your.name@aggreko.com" --project .\src\OF.WebApp\OF.WebApp.csproj
```

Use an account that is present in the selected development data. Do not add a real user email to a tracked configuration file.

### Launch profiles versus the dev script

`src/OF.WebApp/Properties/launchSettings.json` contains `OF.WebApp` and `OF.WebApp.Test` profiles for direct debugging. The supported dev script deliberately starts the backend with `--no-launch-profile` and injects its local SQL, storage, and runtime settings.

Do not mix a manually launched profile with the supported dev stack unless you deliberately need that data source and have confirmed the authentication configuration. Stop the existing process first to avoid two applications competing for port `7200`.

## Common issues

### Failed to authenticate

1. Confirm the backend is listening on `7200` and inspect `build/dev/.state/backend.err.log`.
2. Confirm `/api/auth/me` is reachable through the frontend and that no stale backend is running on the same port.
3. Confirm `LocalUserEmail` is set only for your local environment and maps to a valid user in the active database.
4. Restart the supported stack with `stop.ps1` then `start.ps1` after changing configuration.

### API or data is unavailable

1. Check the frontend proxy target (`https://localhost:7200`) and backend logs.
2. Confirm LocalDB/Docker SQL, the DACPAC publish, and Azurite ports are healthy.
3. Use `-SkipDbPublish` only when the local schema is already known to be current.

### Service Bus unavailable during activation

Local activation needs valid sender permissions. Use an environment variable or user-secret override; never commit it. If a stale user-secret namespace blocks the intended configuration, remove it with:

```powershell
dotnet user-secrets remove ServiceBusNamespace --project .\src\OF.WebApp\OF.WebApp.csproj
```

### Ports are already in use

Run:

```powershell
.\build\dev\stop.ps1
```

Then inspect the state logs before restarting. Do not delete or commit local Azurite/runtime files to solve a startup issue.

## Related documents

- [Frontend testing and verification](../frontend/TESTING.md)
- [Web API and persisted views contract](../api/WEB-API-CONTRACT.md)
- [Existing script reference](../../build/dev/README.md)

