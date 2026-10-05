# Backend Coding Standards

Applies to changes under `src/`.

## Service Design

- Every service must have a **public interface** — no concrete service should be consumed directly by callers
- Services receive all dependencies via **constructor injection** — no service locator, no `new` for dependencies
- Services must not hold per-request mutable state as fields; use local variables or `IHttpContextAccessor` for request-scoped data

## Azure Functions (OF.Api)

- All HTTP-triggered functions must validate the incoming request body before processing
- Use the **mediator pattern** (`IMediator` / `ISender`) to dispatch use cases — functions should not contain business logic
- Accept `CancellationToken` in function signatures and forward it to downstream async calls
- Return appropriate HTTP status codes: `200` for success, `400` for validation failure, `404` for not found, `500` for unexpected errors
- Do not swallow exceptions silently in function handlers — log and re-throw or return a structured error response

## EF Core & Data Access

- Use `IDbContextFactory<ApplicationDbContext>` for creating short-lived `DbContext` instances — do not inject `DbContext` directly into long-lived services
- Prefer `AsNoTracking()` for read-only queries to improve performance
- Do not load entire collections into memory for filtering — apply `Where` clauses before materialising with `ToListAsync()`
- Database schema changes must be scripted in `OF.Data.Design` (SSDT project) — do not use EF Core migrations

## Error Responses

- Return `400 BadRequest` for invalid input or validation failures
- Return `404 NotFound` when a requested resource does not exist
- Return `500 InternalServerError` for unexpected failures — include a meaningful message but do not leak stack traces
- Do not expose internal exception messages in HTTP responses for production paths

## HTTP Clients

- Register HTTP clients via `AddHttpClient<TInterface, TImpl>()` — do not `new HttpClient()` in service constructors
- Set explicit timeouts when registering clients for slow upstream systems
- Use custom `DelegatingHandler` subclasses for cross-cutting concerns like auth token injection

## Async / Await

- All I/O-bound methods must be `async Task<T>` — do not use `.Result` or `.Wait()` on tasks
- Accept `CancellationToken` in public service methods that make outbound HTTP calls; forward it to all async operations
- Do not use `async void` except for event handlers

## Nullable Reference Types

- Nullable reference types are enabled — do not suppress warnings with `!` unless you have verified the value cannot be null at that point

## Testing (xUnit + Moq + FluentAssertions)

- Test class naming: `[ClassName]Tests`
- Test method naming: `[Method]_[Condition]_[Expected]`
- Use `FluentAssertions` for assertions — do not mix with raw xUnit `Assert.*`
- Use `Microsoft.EntityFrameworkCore.InMemory` for unit tests that need a `DbContext` without a real database
- Use `DatabaseFixture` for integration tests that require a real SQL Server — these will run against a containerised SQL Server in CI via `TEST_CONNECTION_STRING`
- Do not hit real external endpoints or real Azure services in unit tests — mock all outbound dependencies
