# .NET 10 Runtime Upgrade Plan

## Overview

All projects in the Order Fulfillment solution previously targeted `net7.0`, which reached end-of-life in May 2024. The solution has been upgraded directly to .NET 10 LTS (supported until November 2028), skipping .NET 8, as .NET 10 was already released and provides longer support.

## Status: ✅ Complete

All changes have been implemented and merged into the `Net-8-Upgrade` branch (PR #513).

> **Note**: This plan was originally scoped for .NET 8 (EOL November 2026). During implementation it was revised to target **.NET 10 LTS** (EOL November 2028) instead, as .NET 10 was already released and provides an additional 2 years of support at no extra migration cost.

## Acceptance Criteria

- [x] All .csproj files updated to `<TargetFramework>net10.0</TargetFramework>`
- [x] NuGet packages updated to .NET 10-compatible versions (EF Core 10, Azure Functions Worker, etc.)
- [x] Dockerfiles updated to use `mcr.microsoft.com/dotnet/runtime:10.0` and `sdk:10.0`
- [x] Terraform `application_stack` updated: `dotnet_version = "v10.0"` for both Web App and Function App
- [x] Azure Functions runtime remains `~4` (compatible with .NET 10 isolated)
- [x] All unit tests pass against the upgraded runtime (341 passed, 7 skipped, 0 failed)
- [ ] Smoke test in Dev environment confirms no regressions
- [x] SQL Server Docker container used for test execution works with updated EF Core
- [x] CI/CD pipeline updated to install .NET 10 SDK

---

## Phase 1: Project Files & NuGet Packages

### Step 1.1: Update TargetFramework in all .csproj files

**Files modified** (12 projects):

| Project | Path | Type |
|---------|------|------|
| OF.Api | `src/OF.Api/OF.Api.csproj` | Azure Functions (Isolated Worker) |
| OF.Common | `src/OF.Common/OF.Common.csproj` | Class Library |
| OF.Data | `src/OF.Data/OF.Data.csproj` | Class Library (EF Core) |
| OF.Data.Assets | `src/OF.Data.Assets/OF.Data.Assets.csproj` | Console App (Dockerized) |
| OF.Data.CPQ | `src/OF.Data.CPQ/OF.Data.CPQ.csproj` | Console App (Dockerized) |
| OF.Data.ProductItems | `src/OF.Data.ProductItems/OF.Data.ProductItems.csproj` | Console App (Dockerized) |
| OF.Data.Quotes | `src/OF.Data.Quotes/OF.Data.Quotes.csproj` | Console App (Dockerized) |
| OF.Data.Warehouses | `src/OF.Data.Warehouses/OF.Data.Warehouses.csproj` | Console App (Dockerized) |
| OF.PricingUI | `src/OF.PricingUI/OF.PricingUI.csproj` | ASP.NET Core Web |
| OF.UI | `src/OF.UI/OF.UI.csproj` | ASP.NET Core Web |
| OF.UI.Shared | `src/OF.UI.Shared/OF.UI.Shared.csproj` | Razor SDK Library |
| OF.Tests | `src/OF.Tests/OF.Tests.csproj` | xUnit Test Project |

**Change**: `<TargetFramework>net7.0</TargetFramework>` → `<TargetFramework>net10.0</TargetFramework>`

### Step 1.2: Update Entity Framework Core packages

**Files**: `OF.Data.csproj`, `OF.Tests.csproj`

| Package | From | To |
|---------|------|-----|
| Microsoft.EntityFrameworkCore | 7.0.11 | 10.0.8 |
| Microsoft.EntityFrameworkCore.Design | 7.0.11 | 10.0.8 |
| Microsoft.EntityFrameworkCore.SqlServer | 7.0.11 | 10.0.8 |
| Microsoft.EntityFrameworkCore.Tools | 7.0.11 | 10.0.8 |
| Microsoft.EntityFrameworkCore.InMemory | 7.0.14 | 10.0.8 |

### Step 1.3: Update EF Core third-party extensions

| Package | Projects | From | To |
|---------|----------|------|-----|
| EFCore.BulkExtensions | OF.Common, OF.Data.Assets, OF.Data.CPQ, OF.Data.ProductItems, OF.Data.Warehouses | 7.1.6 | 10.0.1 |
| FlexLabs.EntityFrameworkCore.Upsert | OF.Api | 7.0.0 | 10.0.0 |
| EfCore.SchemaCompare | OF.Data | 7.0.0 | 10.0.0 |
| Npgsql.EntityFrameworkCore.PostgreSQL | OF.Common | 7.0.18 | 10.0.1 |

**Note**: `EFCore.BulkExtensions.SqlBulkCopyOptions` conflicts with `Microsoft.Data.SqlClient.SqlBulkCopyOptions`. Resolved by adding a using alias in `MoveDataForCPQTablesHandler.cs`:
```csharp
using SqlBulkCopyOptions = EFCore.BulkExtensions.SqlBulkCopyOptions;
```

### Step 1.4: Update Azure Functions Worker packages

**File**: `src/OF.Api/OF.Api.csproj`

| Package | From | To |
|---------|------|-----|
| Microsoft.Azure.Functions.Worker | 1.19.0 | 2.52.0 |
| Microsoft.Azure.Functions.Worker.Sdk | 1.14.0 | 2.0.7 |

**Note**: `AzureFunctionsVersion` remains `v4` (compatible with .NET 10 isolated)

### Step 1.5: Update Microsoft.Extensions.* packages

**Files**: Multiple projects

All `Microsoft.Extensions.*` packages at version 7.0.0 → 10.0.8:

- Microsoft.Extensions.Configuration
- Microsoft.Extensions.Configuration.EnvironmentVariables
- Microsoft.Extensions.Caching.Abstractions
- Microsoft.Extensions.Caching.Memory
- Microsoft.Extensions.Http
- Microsoft.Extensions.Http.Polly
- Microsoft.Extensions.Logging
- Microsoft.Extensions.Logging.Console

### Step 1.6: Update security and identity packages

| Package | From | To |
|---------|------|-----|
| Azure.Identity | 1.11.4 | 1.17.1 |
| Microsoft.IdentityModel.JsonWebTokens | 7.1.2 | 7.7.1 |
| Microsoft.IdentityModel.Tokens | 7.1.2 | 7.7.1 |

**Note**: `Azure.Identity` and `Microsoft.IdentityModel.*` version bumps were required to resolve NU1605 package downgrade errors caused by transitive dependencies from `EFCore.BulkExtensions` → `Microsoft.Data.SqlClient`.

### Step 1.7: Update other packages

| Package | From | To |
|---------|------|-----|
| System.Text.Json (OF.UI, OF.UI.Shared, OF.PricingUI) | 7.0.4 | 10.0.0 |
| Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation | 7.0.20 | 10.0.0 |
| Refit / Refit.HttpClientFactory / Refit.Newtonsoft.Json | 7.0.0 | 8.0.0 |

### Step 1.8: Replace custom TimeProvider with System.TimeProvider

`OF.Common.TimeProvider` was a pre-.NET 8 workaround for the absence of `System.TimeProvider`. Now that we're on .NET 10, it has been removed.

**Changes**:
- Deleted `src/OF.Common/TimeProvider.cs`
- `SystemTimeProvider` now extends `System.TimeProvider` directly
- `FakeTimeProvider` (tests) now extends `System.TimeProvider` directly
- Removed `using TimeProvider = OF.Common.TimeProvider;` alias from 6 files:
  - `OF.Api/AgreementMessageFunctions.cs`
  - `OF.Api/ChangeFunctions.cs`
  - `OF.Api/UseCases/Agreements/ActivateHeaderHandler.cs`
  - `OF.Tests/ExtensionsX.cs`
  - `OF.Tests/FakeTimeProvider.cs`
  - `OF.UI.Shared/Database/DataRepository.cs`

---

## Phase 2: Docker Images

### Step 2.1: Update all Dockerfiles

**Files** (5 Dockerfiles):

- `src/OF.Data.Assets/Dockerfile`
- `src/OF.Data.CPQ/Dockerfile`
- `src/OF.Data.Quotes/Dockerfile`
- `src/OF.Data.Warehouses/Dockerfile`
- `src/OF.Data.ProductItems/Dockerfile`

**Changes** (in each file):

```dockerfile
# Before
FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build

# After
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
```

---

## Phase 3: Terraform Infrastructure

### Step 3.1: Update App Service .NET version

**File**: `build/infra/main.tf` (line ~485)

```hcl
# Before
application_stack {
  current_stack  = "dotnet"
  dotnet_version = "v7.0"
}

# After
application_stack {
  current_stack  = "dotnet"
  dotnet_version = "v10.0"
}
```

### Step 3.2: Update Function App .NET version

**File**: `build/infra/main.tf` (line ~591)

```hcl
# Before
application_stack {
  dotnet_version              = "v7.0"
  use_dotnet_isolated_runtime = true
}

# After
application_stack {
  dotnet_version              = "v10.0"
  use_dotnet_isolated_runtime = true
}
```

**Note**: `functions_extension_version = "~4"` remains unchanged (compatible with .NET 10)

---

## Phase 4: Build & Test Verification

### Step 4.1: Restore and build solution

```bash
dotnet restore ./src/OF.sln
dotnet build ./src/OF.sln --configuration Release
```

**Result**: Build succeeded. 144 warnings, 1 expected error (OF.Data.Design.sqlproj requires Visual Studio SSDT and cannot be built with the dotnet CLI).

### Step 4.2: Run unit tests with Docker SQL Server

```bash
# Start SQL Server container
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=yourStrong!Password" \
  -p 1433:1433 --name sqlserver -d mcr.microsoft.com/mssql/server:2019-latest

# Set connection string
export TEST_CONNECTION_STRING='Server=localhost,1433;Database=master;User ID=sa;Password=yourStrong!Password;Encrypt=false;TrustServerCertificate=true;'

# Run tests
dotnet test ./src/OF.Tests --configuration Release
```

**Result**: ✅ 341 passed, 7 skipped (pre-existing), 0 failed.

### Step 4.3: Build Docker images

```bash
docker build -t of-data-assets:10.0 -f src/OF.Data.Assets/Dockerfile src/
docker build -t of-data-cpq:10.0 -f src/OF.Data.CPQ/Dockerfile src/
docker build -t of-data-quotes:10.0 -f src/OF.Data.Quotes/Dockerfile src/
docker build -t of-data-warehouses:10.0 -f src/OF.Data.Warehouses/Dockerfile src/
docker build -t of-data-productitems:10.0 -f src/OF.Data.ProductItems/Dockerfile src/
```

### Step 4.4: Update CI/CD pipeline

**File**: `.azd/actions/build.yml`

```yaml
# Before
- task: UseDotNet@2
  inputs:
    version: '7.x'

# After
- task: UseDotNet@2
  inputs:
    version: '10.x'
```

The hosted agent only had .NET SDK 8.0.421 installed, causing `NETSDK1045` errors when targeting `net10.0`. The `UseDotNet@2` task installs the required SDK at pipeline runtime.

---

## Phase 5: Deployment & Smoke Test

### Step 5.1: Deploy to Dev environment

- Apply Terraform changes to update Azure infrastructure
- Deploy application via existing CI/CD pipeline

### Step 5.2: Smoke test checklist

- [ ] Web App (OF.UI, OF.PricingUI) loads successfully
- [ ] Function App responds to HTTP triggers
- [ ] Database connections work (EF Core migrations if any)
- [ ] Service Bus message processing works
- [ ] Durable Functions execute correctly
- [ ] Docker container services run without errors

---

## Scope

### Included

- All 12 .csproj files updated to net10.0
- All NuGet packages updated to .NET 10-compatible versions
- All 5 Dockerfiles updated to .NET 10 images
- Terraform infrastructure updated for both Web App and Function App
- Azure Functions runtime stays at v4 (compatible)
- CI/CD pipeline updated to install .NET 10 SDK
- Removed custom `OF.Common.TimeProvider` in favour of built-in `System.TimeProvider`
- Migrated `System.Data.SqlClient` usage to `Microsoft.Data.SqlClient`

### Excluded

- SQL Database project (`OF.Data.Design.sqlproj`) — no .NET runtime, requires Visual Studio SSDT
- CI/CD pipeline modifications beyond SDK version (existing pipeline works as-is)
- Application logic changes

### Assumptions

- No EF Core breaking changes requiring code modifications (confirmed)
- Third-party packages (BulkExtensions, Upsert) have .NET 10 releases (confirmed)
- Azure SDK packages are .NET 10 compatible (confirmed)

---

## Risk Mitigation

1. **Breaking Changes**: Reviewed [Microsoft's .NET 10 breaking changes](https://learn.microsoft.com/en-us/dotnet/core/compatibility/10.0). No breaking changes impacted this codebase.

2. **Third-Party Packages**: All third-party packages confirmed to have .NET 10 compatible releases prior to implementation.

3. **Rollback Plan**: The `Net-8-Upgrade` branch preserves the full change history. The previous `net7.0` code remains accessible via git history.

---

## Further Considerations

1. **Central Package Management**: Consider adding `Directory.Build.props` to centralize package versions across all projects. This would prevent version drift detected (e.g., `Azure.Messaging.ServiceBus` at both 7.16.1 and 7.17.4 in different projects).

2. **Regression Tests**: Include the regression project (`OF.Tests.Regression`) in the verification pipeline if not already included.

---

## References

- [.NET 10 Announcement](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/)
- [.NET 10 Breaking Changes](https://learn.microsoft.com/en-us/dotnet/core/compatibility/10.0)
- [EF Core 10 What's New](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew)
- [Azure Functions .NET Isolated Worker](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide)
- [System.TimeProvider (.NET 8+)](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider)
