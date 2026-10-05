# Local Dev Startup Scripts

Use these scripts to start the frontend + backend with a local test database and local storage emulator.

## Commands

From repository root:

```powershell
.\build\dev\check.ps1
.\build\dev\start.ps1
.\build\dev\stop.ps1
```

## Start Options

```powershell
# Auto select DB mode: Docker if available, otherwise LocalDB
.\build\dev\start.ps1

# Force LocalDB mode
.\build\dev\start.ps1 -DatabaseMode localdb

# Force Docker mode
.\build\dev\start.ps1 -DatabaseMode docker

# Skip SQL project publish if schema is already in place
.\build\dev\start.ps1 -SkipDbPublish

# Skip npm install
.\build\dev\start.ps1 -NoInstall

# Use a different Key Vault (default is kv-ofdev)
.\build\dev\start.ps1 -KeyVaultUri "https://kv-oftest.vault.azure.net/"

# Override Service Bus connection explicitly (otherwise sourced from Key Vault)
.\build\dev\start.ps1 -ServiceBusConnectionOverride "Endpoint=sb://<namespace>.servicebus.windows.net/;SharedAccessKeyName=<name>;SharedAccessKey=<key>"
```

Environment variable alternative:

```powershell
$env:NOF_SERVICEBUS_CONNECTION = "Endpoint=sb://<namespace>.servicebus.windows.net/;SharedAccessKeyName=<name>;SharedAccessKey=<key>"
.\build\dev\start.ps1
```

If activation returns "Service Bus unavailable", remove stale OF.WebApp user-secrets namespace overrides:

```powershell
dotnet user-secrets remove ServiceBusNamespace --project .\src\OF.WebApp\OF.WebApp.csproj
```

## URLs

- Frontend: http://localhost:5174
- Backend: https://localhost:7200

## Notes

- State and logs are written under `build/dev/.state`.
- SQL project build uses Visual Studio MSBuild because SSDT projects are not built by `dotnet build`.
- Local activation send requires Azure Service Bus Data Sender (or equivalent send permission) on the target namespace/queue.
