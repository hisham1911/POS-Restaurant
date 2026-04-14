# KasserPro Workspace Instructions

These instructions are mandatory for all code generation, refactoring, reviews, and tests in this repository.

## Canonical Sources

1. `.kiro/steering/architecture.md` is the architecture source of truth.
2. `.kiro/skills/kasserpro-bestpractices/SKILL.md` is the implementation behavior guide.
3. If docs conflict with code, treat code as truth and report the conflict.

## Non-Negotiable Rules

1. Do not use AutoMapper. Use explicit `.Select(...)` projections or `MapToDto()` methods.
2. Do not use FluentValidation. Do manual validation in services using `ErrorCodes` and `ErrorMessages.Get(...)`.
3. Frontend path is `frontend/` (not `client/`).
4. Stock is tracked in `BranchInventory.Quantity`, not `Product.StockQuantity`.
5. Financial operations must use transactions with `await using var transaction = await _unitOfWork.BeginTransactionAsync();`.
6. API errors should use `ApiResponse<T>.Fail(ErrorCodes.X, ErrorMessages.Get(ErrorCodes.X))`.
7. For SQLite schema changes, never use risky direct alter patterns. Use add/migrate/drop style migration flow.
8. Preserve tenant and branch isolation in all queries and writes.

## Working Behavior

1. Before editing, identify impacted layers (API/Application/Domain/Infrastructure/Frontend).
2. Keep DTO contracts aligned between backend and frontend.
3. Prefer minimal, safe changes with tests for business-critical logic.
4. For security or financial code, prioritize correctness over speed.
