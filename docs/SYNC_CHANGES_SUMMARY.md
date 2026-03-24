# Frontend ↔ Backend Sync — Breaking Changes Summary

**Date:** Post Zero-Defect Hardening  
**Context:** The backend services were hardened to fix 31 bugs from the Adversarial Stress Test. This document summarizes all sync changes made to API Controllers, DTOs, Frontend Types, and UI components.

---

## 1. Backend DTO Changes

### ShiftDto (`backend/KasserPro.Application/DTOs/Shifts/ShiftDto.cs`)

- **ADDED** `TotalFawry` (decimal) — Fawry payment total for the shift
- **ADDED** `TotalBankTransfer` (decimal) — Bank transfer payment total
- `TotalCard` remains but now represents **all non-cash payments** (Card + Fawry + BankTransfer)

### ShiftSummaryDto (`backend/KasserPro.Application/DTOs/Reports/ReportDto.cs`)

- **ADDED** `TotalFawry` (decimal) — Fawry breakdown for shift in daily reports

---

## 2. Backend Service Changes

### ShiftService (`backend/KasserPro.Application/Services/Implementations/ShiftService.cs`)

- `CalculateShiftFinancials` now returns a **5-tuple**: `(TotalOrders, TotalCash, TotalCard, TotalFawry, TotalBankTransfer)`
- `MapToDto` now populates `TotalFawry` and `TotalBankTransfer` (always computed from orders, not stored on entity)
- All call sites (CloseAsync, ForceCloseAsync) updated to destructure the new tuple

### ReportService (`backend/KasserPro.Application/Services/Implementations/ReportService.cs`)

- Shift summaries in daily reports now compute `TotalFawry` from order payments
- Uses same status filter (Completed + PartiallyRefunded + Refunded) for consistency

---

## 3. Frontend Type Changes

### order.types.ts

- **`PaymentMethod`**: Added `"BankTransfer"` → `"Cash" | "Card" | "Fawry" | "BankTransfer"`
- **`OrderItem`**: Added `refundedQuantity: number` — tracks how many units already refunded
- **`Order`**: Added `originalOrderId?: number` — links return orders to original

### shift.types.ts

- **`Shift`**: Added `totalFawry: number` and `totalBankTransfer: number`

### report.types.ts

- **`ShiftSummary`**: Added `totalFawry: number`

---

## 4. Frontend Constants Changes

### constants.ts

- **`PAYMENT_METHODS`**: Added `BankTransfer: { label: "تحويل بنكي", icon: "🏦" }`
- Changed Fawry icon from `💳` to `📱` for differentiation
- **`ERROR_MESSAGES`**: Expanded from 5 entries to 30+ covering all critical backend error codes (orders, shifts, payments, products, customers, cash register)

### errorHandler.ts

- **`ERROR_CODES`**: Expanded from 13 to 48 error codes matching backend `ErrorCodes.cs`
- **`ERROR_MESSAGES`**: Full Arabic translations for all 48 error codes

---

## 5. Frontend Component Fixes

### RefundModal.tsx — **CRITICAL FIX**

- **BUG FIX**: `maxQuantity` now uses `item.quantity - (item.refundedQuantity || 0)` instead of `item.quantity`
  - This prevents re-refunding already-refunded items on PartiallyRefunded orders
- Items with 0 remaining refundable quantity are filtered out
- **Added**: 500-character limit on custom refund reason (matches backend validation)
- **Added**: Character counter shown when approaching limit (>450 chars)
- **Added**: Structured error handling — backend error codes mapped to Arabic messages via `ERROR_MESSAGES`

### OrderDetailsModal.tsx

- Shows `(مسترجع: X)` indicator on items with `refundedQuantity > 0`

### ShiftPage.tsx

- Label changed from "المبيعات بالبطاقة" → "مبيعات إلكترونية" (Electronic Sales)
- When Fawry or BankTransfer amounts exist, shows granular breakdown:
  - بطاقة (pure card)
  - فوري
  - تحويل بنكي

### DailyReportPage.tsx

- Shift detail label changed from "بطاقة" → "إلكتروني"
- Added Fawry line in shift breakdown when `totalFawry > 0`
- Thermal print template updated to show Fawry breakdown

### cartSlice.ts

- **Added**: Explicit percentage discount validation — `Math.min(value, 100)` clamp
- **Added**: `Math.max(0, value)` guard for negative discount values

---

## 6. API Controllers — No Changes Needed

After thorough audit, **no controller changes were required** because:

- Controllers delegate to hardened services (don't call internal methods like `DeductRefundStatsAsync` directly)
- `OrdersController.RefundAsync` already passes correct parameters
- `ShiftsController` passes through to service layer which handles unified financials
- `ReportsController` calls hardened `GetDailyReportAsync` and `GetSalesReportAsync`

---

## 7. Build Verification

| Layer                                | Status                        |
| ------------------------------------ | ----------------------------- |
| Backend (`dotnet build`)             | ✅ 0 warnings, 0 errors       |
| Frontend TypeScript (`tsc --noEmit`) | ✅ 0 errors                   |
| Frontend Production (`vite build`)   | ✅ 1783 modules, built in 12s |
