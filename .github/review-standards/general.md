# General Coding Standards

These standards apply to every pull request regardless of which layer is changed.

## Naming Conventions

- Classes, interfaces, methods, and properties use **PascalCase**
- Local variables and parameters use **camelCase**
- Private fields use **_camelCase** (underscore prefix)
- Constants use **PascalCase** (not ALL_CAPS) and live in a static `Constants` class grouped by domain
- Test methods follow `[Method]_[Condition]_[Expected]` (e.g. `GetOrders_WithInvalidId_Returns400`)

## Security

- **Never log user-supplied input raw** — sanitize before passing to any `ILogger` method (strip newlines, truncate to a safe length)
- **Never log values that could contain tokens, credentials, or PII**
- **Validate external identifiers** before using them in queries or as route/query parameters
- Do not echo unvalidated input back to callers in error messages

## Error Handling

- This codebase uses **exception-based error handling** — do not introduce `Result<T>` or `Option<T>` patterns
- Let exceptions propagate to the global error handler unless a specific recovery action is possible at the call site
- Catch only what you can meaningfully handle; re-throw with `throw;` (not `throw ex;`) to preserve the stack trace
- Always log before re-throwing or swallowing: `_logger.LogError(ex, "...", args)`

## Logging

- Inject `ILogger<T>` via constructor — do not use a static logger or `LoggerFactory` directly
- Use structured logging with named parameters, not string interpolation: `_logger.LogInformation("Processing order {OrderId}", orderId)`
- Choose the correct level: `LogDebug` for diagnostics, `LogInformation` for normal flow milestones, `LogWarning` for recoverable issues, `LogError` for failures

## Dependency Injection

- Register services with the correct lifetime:
  - `AddScoped` — stateful per-request services (most business logic services)
  - `AddSingleton` — stateless utilities with no per-request state
  - `AddTransient` — lightweight message handlers
  - `AddHttpClient<TInterface, TImpl>` — services that own an `HttpClient`
- Do not resolve services from the container manually (`IServiceProvider.GetService`) except in factory delegates or middleware

## Configuration Access

- Configuration priority: **Azure Key Vault > environment variables > local.settings.json**
- Bind complex settings to a typed options class via `services.Configure<T>()` and inject `IOptions<T>` or `IOptionsSnapshot<T>` — do not scatter raw `IConfiguration` access throughout service methods
- Do not commit secrets, connection strings, or credentials — these must come from Key Vault or environment variables

## Pull Requests

- Each PR should be focused on a single concern; mixing unrelated changes is a red flag
- New public-facing behaviour must include corresponding tests
- Do not commit secrets, connection strings, or credentials — these must come from Key Vault or User Secrets
