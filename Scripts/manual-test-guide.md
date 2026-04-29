# Manual Verification Guide: Expenses & Cash Register

## Prerequisites
- Backend running on `http://localhost:5243`
- Frontend running on `http://localhost:3000`
- Database accessible (SQLite or SQL Server)

---

## Scenario 1: Cash Payment → Creates Cash Register Record

### Steps
1. **Find a Purchase Invoice**
   ```
   GET http://localhost:5243/api/purchase-invoices?page=1&pageSize=5&status=Pending,PartiallyPaid
   Authorization: Bearer {admin_token}
   ```
   Note the `id` of any unpaid invoice.

2. **Record Baseline**
   Run this SQL before the payment:
   ```sql
   SELECT COUNT(*) as baseline
   FROM CashRegisterTransactions
   WHERE Type = 6;  -- SupplierPayment
   ```
   Save the `baseline` number.

3. **Pay with Cash**
   ```
   POST http://localhost:5243/api/purchase-invoices/{invoiceId}/payments
   Authorization: Bearer {admin_token}
   Content-Type: application/json

   {
     "amount": 50.00,
     "paymentDate": "2026-04-29T12:00:00Z",
     "method": "Cash",
     "referenceNumber": "TEST-CASH-001",
     "notes": "Manual test - should create cash register record"
   }
   ```
   **Expected Response:** `isSuccess: true`

4. **Verify Database**
   ```sql
   SELECT
     t.Id,
     t.Type,
     t.Amount,
     t.Description,
     t.CreatedAt,
     t.BranchId,
     t.ShiftId
   FROM CashRegisterTransactions t
   WHERE t.Type = 6
   ORDER BY t.CreatedAt DESC
   LIMIT 3;
   ```
   **Expected:**
   - A new row appears with `Type = 6` (SupplierPayment)
   - `Amount` should match the payment amount (50.00)
   - `Description` should reference the supplier payment
   - Total count = baseline + 1

---

## Scenario 2: Bank Transfer Payment → NO Cash Register Record

### Steps
1. **Record Baseline**
   ```sql
   SELECT COUNT(*) as baseline
   FROM CashRegisterTransactions
   WHERE Type = 6;
   ```

2. **Pay with BankTransfer**
   ```
   POST http://localhost:5243/api/purchase-invoices/{invoiceId}/payments
   Authorization: Bearer {admin_token}
   Content-Type: application/json

   {
     "amount": 75.00,
     "paymentDate": "2026-04-29T12:00:00Z",
     "method": "BankTransfer",
     "referenceNumber": "TEST-BANK-001",
     "notes": "Manual test - should NOT create cash register record"
   }
   ```
   **Expected Response:** `isSuccess: true`

3. **Verify Database**
   ```sql
   SELECT COUNT(*) as after_count
   FROM CashRegisterTransactions
   WHERE Type = 6;
   ```
   **Expected:** `after_count` should equal `baseline` (no change)

   Verify the most recent SupplierPayment is still the cash one:
   ```sql
   SELECT Amount, Description, CreatedAt
   FROM CashRegisterTransactions
   WHERE Type = 6
   ORDER BY CreatedAt DESC
   LIMIT 1;
   ```
   **Expected:** Most recent should be 50.00 (the cash payment), not 75.00

---

## Scenario 3: Cashier Permissions → Transfer Button Hidden

### Steps
1. **Login as Cashier** via frontend
   - Email: `ahmed@kasserpro.com`
   - Password: `123456`

2. **Navigate to** `http://localhost:3000/cash-register`

3. **Verify UI**
   - **Deposit button** (`إيداع`) → **VISIBLE**
   - **Withdraw button** (`سحب`) → **VISIBLE**
   - **Transfer button** (`تحويل نقدي`) → **HIDDEN** (not in DOM)
   - **Reconcile button** (`مطابقة وإغلاق الشيفت`) → **HIDDEN** (not in DOM)
   - **Refresh button** (`تحديث`) → **VISIBLE**

4. **Login as Admin**
   - Email: `admin@kasserpro.com`
   - Password: `Admin@123`

5. **Navigate to** `http://localhost:3000/cash-register`

6. **Verify Admin sees all buttons**
   - Transfer button → **VISIBLE**
   - Reconcile button → **VISIBLE**

---

## Quick SQL Verification (All Scenarios)

```sql
-- Count all transaction types for verification
SELECT
  Type,
  COUNT(*) as Count,
  SUM(Amount) as TotalAmount
FROM CashRegisterTransactions
GROUP BY Type;

-- Expected Type 6 (SupplierPayment) count increases only after Cash payments
-- Expected Type 1-5,7-9 should be unaffected by purchase invoice payments
```

---

## Troubleshooting

### If Admin login fails
- Check that the backend `KasserPro.API` is running on port 5243
- Verify the database has the seeded admin user

### If Cashier still sees Transfer button
- Check cashier permissions in `UserPermissions` table:
  ```sql
  SELECT p.Key, p.Group
  FROM UserPermissions up
  JOIN Permissions p ON up.PermissionId = p.Id
  JOIN Users u ON up.UserId = u.Id
  WHERE u.Email = 'ahmed@kasserpro.com';
  ```
- `CashRegisterTransfer` (1002) and `CashRegisterReconcile` (1003) should NOT be in the list for a standard cashier

### If cash payment doesn't create cash register record
- Verify `PurchaseInvoiceService.AddPaymentAsync` has the cash register integration code
- Verify `ICashRegisterService.RecordTransactionAsync` is being called
- Check application logs for errors during payment
