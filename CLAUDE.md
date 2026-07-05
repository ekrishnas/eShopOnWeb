# CLAUDE.md — AI Agent Context for eShopOnWeb

This file gives Claude Code (and any AI agent) the context needed to work effectively in this codebase without re-deriving it from scratch.

## What This Application Does

eShopOnWeb is a reference ASP.NET Core e-commerce application demonstrating Clean Architecture. It sells catalog items (products with brands and types), manages user baskets, and processes orders. It is deliberately kept simple — its purpose is to show patterns, not to be a production system.

## Architecture

**Clean Architecture** with strict dependency rules: inner layers never reference outer ones.

```
ApplicationCore   ← domain entities, interfaces, services, specifications (no infra deps)
Infrastructure    ← EF Core, Identity, repository implementations (depends on ApplicationCore)
Web               ← ASP.NET Core MVC + Razor Pages (depends on ApplicationCore + Infrastructure)
PublicApi         ← FastEndpoints REST API (depends on ApplicationCore + Infrastructure)
BlazorAdmin       ← WebAssembly admin panel, calls PublicApi endpoints
BlazorShared      ← DTOs shared between Web and BlazorAdmin
eShopWeb.AppHost  ← .NET Aspire orchestration for local dev
```

Key patterns in use:
- **Repository + Specification** (Ardalis): queries live in `ApplicationCore/Specifications/`
- **MediatR**: domain events published after order creation
- **Guard clauses** (Ardalis): input validation at service entry points
- **Result pattern** (Ardalis): some services return `Result<T>` instead of throwing

## Key Files to Know

| Path | What it is |
|---|---|
| `src/ApplicationCore/Services/` | Core business logic — BasketService, OrderService, UriComposer |
| `src/ApplicationCore/Entities/` | Domain aggregates: Basket, Order, CatalogItem, etc. |
| `src/ApplicationCore/Specifications/` | Ardalis specs for all queries |
| `src/ApplicationCore/Exceptions/` | Domain-specific exceptions |
| `src/Infrastructure/Data/` | EF Core contexts (CatalogContext, AppIdentityDbContext) |
| `src/Infrastructure/Data/SeedData.cs` | Catalog seeding on startup |
| `src/Web/Pages/` | Razor Pages (Basket, Checkout, Catalog) |
| `src/PublicApi/CatalogItemEndpoints/` | FastEndpoints for catalog CRUD |
| `tests/UnitTests/` | xUnit unit tests (NSubstitute mocks) |
| `tests/IntegrationTests/` | Repository integration tests (EF InMemory) |
| `tests/PublicApiIntegrationTests/` | FastEndpoints integration tests |
| `tests/FunctionalTests/` | Full web stack functional tests |

## Build & Run Commands

```bash
# Build entire solution
dotnet build ./eShopOnWeb.sln --configuration Release

# Run all tests with coverage
dotnet test ./eShopOnWeb.sln --collect:"XPlat Code Coverage" --logger trx

# Run only unit tests (fast)
dotnet test tests/UnitTests/UnitTests.csproj

# Run the web app (requires PublicApi running separately for BlazorAdmin)
cd src/Web && dotnet run --launch-profile https
cd src/PublicApi && dotnet run

# Aspire (orchestrates everything)
cd src/eShopWeb.AppHost && dotnet run

# EF migrations
cd src/Web
dotnet ef database update -c CatalogContext -p ../Infrastructure/Infrastructure.csproj -s Web.csproj
dotnet ef database update -c AppIdentityDbContext -p ../Infrastructure/Infrastructure.csproj -s Web.csproj
```

## Test Patterns

Unit tests follow the pattern: **one class per scenario, not per method**.

```csharp
// tests/UnitTests/ApplicationCore/Services/BasketServiceTests/AddItemToBasket.cs
public class AddItemToBasket
{
    private readonly IRepository<Basket> _mockBasketRepo = Substitute.For<IRepository<Basket>>();
    private readonly IAppLogger<BasketService> _mockLogger = Substitute.For<IAppLogger<BasketService>>();

    [Fact]
    public async Task InvokesBasketRepositoryGetBySpecAsyncOnce() { ... }
}
```

When adding unit tests for a service:
1. Create a folder `tests/UnitTests/ApplicationCore/Services/<ServiceName>Tests/`
2. One file per scenario (verb phrase: `CreateOrderWithMissingCatalogItem.cs`)
3. Use `NSubstitute` for mocks, never Moq (commented-out Moq references exist — ignore them)
4. Use `xUnit` (`[Fact]` and `[Theory]`)

## Known Coverage Gaps (as of July 2025)

- `OrderService` has **zero unit tests** — highest priority gap
- `UriComposer` has no unit tests
- PublicApi: `CatalogBrandListEndpoint`, `CatalogTypeListEndpoint`, `UpdateCatalogItemEndpoint`, `UpdateRoleEndpoint` lack integration tests
- `BlazorAdmin` has no test project

## Guard Clause Conventions

This codebase uses `Ardalis.GuardClauses`. Custom guards live in `src/ApplicationCore/Extensions/GuardExtensions.cs`.

```csharp
Guard.Against.Null(basket, nameof(basket));
Guard.Against.EmptyBasketOnCheckout(basket.Items);  // custom
Guard.Against.NullOrEmpty(name, nameof(name));
```

When adding new domain validation, prefer a custom guard extension over inline `if` + `throw`.

## Exception Conventions

Domain exceptions live in `src/ApplicationCore/Exceptions/`. They extend `Exception` and provide a clear message. The Web layer catches them in page handlers and redirects gracefully (see `Checkout.cshtml.cs`).

## What AI Should NOT Change Without Discussion

- EF Core migrations (always a human-reviewed step)
- `SeedData.cs` (affects all developer environments)
- Authentication/Identity configuration in `Infrastructure`
- The Clean Architecture dependency direction (ApplicationCore must never reference Infrastructure or Web)

## AIDLC Workflow Notes

When using Claude Code on this repo:
1. Read this file first (you're doing it now)
2. Use `docs/prompts/` for task-specific prompts
3. After any change: run `dotnet test tests/UnitTests/` at minimum
4. Before committing: check `docs/prompts/pre-commit-review.md`
5. PR description must fill in `.github/PULL_REQUEST_TEMPLATE.md`
