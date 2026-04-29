# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

KasserPro is a multi-tenant POS (Point of Sale) system with branch management, inventory tracking, financial reporting, and Arabic RTL support. It consists of a .NET 8 backend API and a React 18 TypeScript frontend.

## Build & Run Commands

### Backend (.NET 8, SQLite)

```bash
cd backend/KasserPro.API
dotnet restore          # restore packages
dotnet build            # build
dotnet run              # starts on http://localhost:5243
dotnet watch run        # dev with hot reload
dotnet test             # run xUnit tests (from backend/ dir)
```

### Frontend (React 18, Vite, npm)

```bash
cd frontend
npm install
npm run dev             # starts on http://localhost:3000, proxies /api to :5243
npm run build           # tsc -b && vite build
npm run type-check      # TypeScript check only
npm run lint            # ESLint (zero warnings)
npm run test:e2e        # Playwright (requires both backend and frontend running)
npm run test:e2e:headed # Playwright with browser UI
```

### Database Migrations

```bash
cd backend/KasserPro.API
dotnet ef migrations add MigrationName -p ../KasserPro.Infrastructure -s .
dotnet ef database update -p ../KasserPro.Infrastructure -s .
```

## Architecture

**Clean Architecture** with four .NET projects and a React SPA:

```
backend/
  KasserPro.API            -> Controllers, Middleware, SignalR Hub, Program.cs entry point
  KasserPro.Application    -> Services, DTOs, Validation, ErrorCodes
  KasserPro.Domain         -> Entities, Enums (no external dependencies)
  KasserPro.Infrastructure -> EF Core DbContext, Repositories, UnitOfWork, Migrations
  KasserPro.Tests          -> xUnit + FluentAssertions
frontend/src/
  api/         -> RTK Query endpoint definitions (baseApi.ts is the root)
  components/  -> React components
  hooks/       -> Custom hooks
  pages/       -> Page-level components
  store/       -> Redux Toolkit store, slices (auth, cart, ui, branch), middleware
  types/       -> TypeScript interfaces
  utils/       -> Utilities and constants
```

**Dependency flow:** API -> Application -> Domain <- Infrastructure

### Key Patterns

- **Multi-tenant isolation**: Every query filters by TenantId/BranchId. BranchAccessMiddleware enforces this at the API level.
- **Soft delete**: Entities use `IsDeleted` flag with EF Core global query filters.
- **Repository + Unit of Work**: All data access goes through `IUnitOfWork` which exposes repositories.
- **Financial transactions**: Must use `await using var transaction = await _unitOfWork.BeginTransactionAsync();`
- **API responses**: Use `ApiResponse<T>.Fail(ErrorCodes.X, ErrorMessages.Get(ErrorCodes.X))` for errors.
- **Stock tracking**: Tracked in `BranchInventory.Quantity`, NOT `Product.StockQuantity`.
- **Auth**: JWT Bearer tokens with SecurityStamp validation (invalidates tokens on password change).

### Middleware Stack (request order)

MaintenanceModeMiddleware -> CorrelationIdMiddleware -> ExceptionMiddleware -> IdempotencyMiddleware -> JWT Auth -> BranchAccessMiddleware -> Authorization

### Frontend State

- **Redux Toolkit** with RTK Query for data fetching.
- Auth state (token, user) persisted via redux-persist. Branch state is NOT persisted (fresh selection on each login).
- Path alias: `@/*` maps to `./src/*`.
- Styling: TailwindCSS with Cairo font for Arabic RTL support.
- Forms: react-hook-form + zod validation.

## Non-Negotiable Rules

These come from `.github/copilot-instructions.md` and `.kiro/steering/`:

1. **No AutoMapper** -- use explicit `.Select(...)` projections or `MapToDto()` methods.
2. **No FluentValidation** -- manual validation in services using `ErrorCodes` and `ErrorMessages.Get(...)`.
3. **SQLite migrations: never use `AlterColumn`** -- use the Add + Migrate + Drop pattern (add new column, copy data with SQL, drop old column). See `.kiro/steering/database-migrations-guide.md`.
4. **Preserve tenant and branch isolation** in all queries and writes.
5. **Financial/security code: correctness over speed.** Always use transactions for financial operations.
6. **Keep DTO contracts aligned** between backend and frontend when changing either side.

## Canonical Documentation

- `.kiro/steering/architecture.md` -- architecture source of truth
- `.kiro/steering/database-migrations-guide.md` -- SQLite migration patterns
- `.kiro/steering/kasserpro-api-contract.md` -- API specifications
- `.kiro/steering/kasserpro-developer-guide.md` -- development workflows
- `.github/copilot-instructions.md` -- mandatory workspace rules

If docs conflict with code, treat code as truth and report the conflict.
