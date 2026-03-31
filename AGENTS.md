# KasserPro Agent Rules

This file defines mandatory behavior for coding agents working in this repository.

## Source Of Truth

1. `.kiro/steering/architecture.md` is the architecture authority.
2. `.kiro/skills/kasserpro-bestpractices/SKILL.md` is the implementation behavior guide.
3. If code and documentation conflict, follow code and report the mismatch.

## Always Enforce

1. No AutoMapper. Use explicit `.Select(...)` projections or local `MapToDto()` methods.
2. No FluentValidation. Perform manual service-layer validation with `ErrorCodes` and `ErrorMessages.Get(...)`.
3. Frontend is in `frontend/`, not `client/`.
4. Stock is per branch in `BranchInventory.Quantity`, not `Product.StockQuantity`.
5. Use transactions for financial operations with `await using var transaction = await _unitOfWork.BeginTransactionAsync();`.
6. Use `ApiResponse<T>.Fail(ErrorCodes.X, ErrorMessages.Get(ErrorCodes.X))` for error responses.
7. For SQLite schema evolution, avoid direct alter patterns. Use add/migrate/drop flow.
8. Preserve tenant and branch isolation in every read/write path.

## Working Style

1. Keep changes minimal and safe.
2. Keep backend DTOs and frontend types aligned.
3. Add or update tests for business-critical behavior.
4. Prioritize correctness for security and financial code.
