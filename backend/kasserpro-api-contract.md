---
inclusion: always
---

# 📜 KasserPro — API Contract Document

> **النوع:** وثيقة تعاقد رسمية بين الباك-اند والفرونت-اند  
> **الحالة:** مبنية على الكود الفعلي (مش الـ docs القديمة)  
> **آخر تحديث:** 2 أبريل 2026  
> **القاعدة الذهبية:** الكود هو الحقيقة — لو في تعارض بين الوثيقة دي والكود، الكود يكسب

---

## 📋 فهرس المحتويات

1. [عقد الـ Response Shape](#1-عقد-الـ-response-shape)
2. [عقد الـ Authentication & Headers](#2-عقد-الـ-authentication--headers)
3. [عقد الـ Error Codes](#3-عقد-الـ-error-codes)
4. [عقد كل Module](#4-عقد-كل-module)
5. [عقد الـ Real-time (SignalR)](#5-عقد-الـ-real-time-signalr)
6. [قواعد الفرونت الإلزامية](#6-قواعد-الفرونت-الإلزامية)
7. [قواعد الباك-اند الإلزامية](#7-قواعد-الباك-اند-الإلزامية)
8. [Checklist قبل كل Feature جديد](#8-checklist-قبل-كل-feature-جديد)

---

## 1. عقد الـ Response Shape

### 🔒 القانون الأول — ماعدا استثناء واحد
**كل response من الـ API لازم يكون `ApiResponse<T>` — بدون استثناء**

```typescript
// ✅ الـ shape الوحيدة المقبولة — الفرونت يبني عليها كل حاجة
interface ApiResponse<T> {
  success: boolean;
  data?: T;           // موجود لو success = true
  message?: string;   // رسالة عربية للمستخدم
  errorCode?: string; // موجود لو success = false (مش message فقط!)
  errors?: string[];  // validation errors لو في أكتر من غلطة
}
```

### ✅ أمثلة صحيحة من الباك-اند

```csharp
// Success
return Ok(ApiResponse<ProductDto>.Ok(dto));
return Ok(ApiResponse<int>.Ok(id, "تم الإنشاء بنجاح"));

// Fail — دايمًا ErrorCode + Message
return BadRequest(ApiResponse<object>.Fail(ErrorCodes.PRODUCT_NOT_FOUND,
    ErrorMessages.Get(ErrorCodes.PRODUCT_NOT_FOUND)));
return NotFound(ApiResponse<object>.Fail(ErrorCodes.ORDER_NOT_FOUND,
    ErrorMessages.Get(ErrorCodes.ORDER_NOT_FOUND)));
```

### ❌ ممنوع تمامًا

```csharp
// ❌ الفرونت مش هيعرف يـ handle ده
return Ok(dto);
return BadRequest("الطلب غير موجود");
return BadRequest(new { success = false, message = "..." }); // ad-hoc objects
return StatusCode(500, "Error");
```

### كيف الفرونت يتعامل مع الـ Response

```typescript
// ✅ الطريقة الصحيحة الوحيدة — RTK Query
const result = await createOrder(orderData).unwrap();
// unwrap() بيـ throw لو success = false

// ✅ لما تحتاج تمسك الـ error
const [createOrder] = useCreateOrderMutation();
try {
  const data = await createOrder(orderData).unwrap();
  // data هنا هو الـ T من ApiResponse<T>
} catch (err) {
  const apiError = err as ApiResponse<null>;
  // apiError.errorCode — استخدمه للـ UI logic مش apiError.message
}

// ❌ ممنوع check على .success يدوي
if (response.data?.success === false) { ... } // مش بالطريقة دي
```

---

## 2. عقد الـ Authentication & Headers

### Headers الإلزامية في كل Request

| Header | نوعه | القيمة | ملاحظة |
|--------|------|--------|--------|
| `Authorization` | إلزامي | `Bearer {jwt_token}` | كل endpoint ما عدا `/api/auth/login` و `/api/health` |
| `X-Branch-Id` | إلزامي | `{branchId: int}` | كل endpoint ما عدا login وhealth وtenant-level endpoints |
| `X-Idempotency-Key` | إلزامي للـ writes | `{uuid}` | POST/PUT/DELETE على financial operations |
| `Content-Type` | للـ body | `application/json` | POST/PUT requests |

### إزاي الـ JWT بيشتغل

```
Login → JWT Token (claims: userId, tenantId, branchId, role, permissions, securityStamp)
         ↓
كل request → BranchAccessMiddleware يتحقق إن X-Branch-Id يخص الـ user
         ↓
JWT middleware يتحقق من SecurityStamp (cache 5 دقايق) — لو اتغير → 401
         ↓
HasPermission filter يتحقق من claim "permission" في الـ token
```

### إمتى يجي 401 vs 403

| الـ Status Code | المعنى | الـ Error Code |
|----------------|--------|---------------|
| `401 Unauthorized` | التوكن مش صالح / SecurityStamp اتغير / التوكن انتهى | `TOKEN_INVALID` / `TOKEN_EXPIRED` |
| `403 Forbidden` | التوكن صالح بس مفيش permission | `PERMISSION_DENIED` / `BRANCH_ACCESS_DENIED` |
| `503 Service Unavailable` | الـ system في maintenance mode | `MAINTENANCE_MODE` |

```typescript
// الفرونت — الـ base query handler
const baseQuery = fetchBaseQuery({
  baseUrl: '/api',
  prepareHeaders: (headers, { getState }) => {
    const token = (getState() as RootState).auth.token;
    if (token) headers.set('Authorization', `Bearer ${token}`);

    const branchId = (getState() as RootState).auth.currentBranchId;
    if (branchId) headers.set('X-Branch-Id', branchId);

    return headers;
  },
});

// Handle 401 → redirect to login
// Handle 503 → show maintenance screen
```

---

## 3. عقد الـ Error Codes

### القانون: الفرونت يتعامل مع `errorCode` مش `message`

```typescript
// ✅ صح — errorCode ثابت ومضمون
switch (error.errorCode) {
  case 'PRODUCT_NOT_FOUND': showToast('المنتج غير موجود'); break;
  case 'INSUFFICIENT_STOCK': showStockWarning(); break;
  case 'NO_OPEN_SHIFT': redirectToShiftPage(); break;
}

// ❌ غلط — message ممكن تتغير
if (error.message === 'المنتج غير موجود') { ... }
```

### جدول الـ Error Codes الرسمية

#### 🛒 Orders
| Error Code | الموقف | HTTP Status |
|-----------|--------|-------------|
| `ORDER_NOT_FOUND` | الطلب مش موجود | 404 |
| `ORDER_EMPTY` | الطلب بدون عناصر | 400 |
| `ORDER_INVALID_QUANTITY` | الكمية ≤ 0 | 400 |
| `ORDER_NOT_EDITABLE` | الطلب مش Draft | 400 |
| `ORDER_ALREADY_COMPLETED` | الطلب اتكمل قبل كده | 400 |
| `ORDER_ALREADY_CANCELLED` | الطلب اتلغى قبل كده | 400 |

#### 📦 Products & Inventory
| Error Code | الموقف | HTTP Status |
|-----------|--------|-------------|
| `PRODUCT_NOT_FOUND` | المنتج مش موجود | 404 |
| `PRODUCT_INVALID_PRICE` | السعر < 0 | 400 |
| `PRODUCT_NAME_REQUIRED` | الاسم فاضي | 400 |
| `PRODUCT_NAME_DUPLICATE` | الاسم متكرر | 400 |
| `PRODUCT_INACTIVE` | المنتج غير نشط | 400 |
| `INSUFFICIENT_STOCK` | المخزن مش كافي | 400 |
| `INVENTORY_NOT_FOUND` | مفيش سجل مخزون للفرع ده | 404 |

#### 💰 Payments & Shifts
| Error Code | الموقف | HTTP Status |
|-----------|--------|-------------|
| `PAYMENT_INVALID_AMOUNT` | المبلغ <= 0 | 400 |
| `PAYMENT_OVERPAYMENT` | الدفع أكتر من 2x الإجمالي | 400 |
| `NO_OPEN_SHIFT` | مفيش شيفت مفتوح | 400 |
| `SHIFT_ALREADY_OPEN` | في شيفت مفتوح بالفعل | 400 |
| `SHIFT_NOT_FOUND` | الشيفت مش موجود | 404 |

#### 🔐 Auth & Access
| Error Code | الموقف | HTTP Status |
|-----------|--------|-------------|
| `TOKEN_INVALID` | التوكن غلط | 401 |
| `TOKEN_EXPIRED` | التوكن انتهى | 401 |
| `PERMISSION_DENIED` | مفيش permission | 403 |
| `BRANCH_ACCESS_DENIED` | مش بتاع الفرع ده | 403 |
| `USER_NOT_FOUND` | المستخدم مش موجود | 404 |
| `USER_INACTIVE` | الحساب موقوف | 403 |
| `TENANT_INACTIVE` | الشركة موقوفة | 403 |

#### ⚙️ System
| Error Code | الموقف | HTTP Status |
|-----------|--------|-------------|
| `MAINTENANCE_MODE` | السيستم في صيانة | 503 |
| `VALIDATION_ERROR` | Validation فشل | 400 |
| `CONCURRENCY_CONFLICT` | تعارض في التعديل المتزامن | 409 |
| `BACKUP_NOT_FOUND` | ملف النسخة الاحتياطية غير موجود | 404 |
| `BACKUP_FAILED` | فشل إنشاء النسخة الاحتياطية | 500 |
| `RESTORE_FAILED` | فشلت عملية الاستعادة | 500 |
| `DATABASE_ERROR` | خطأ في قاعدة البيانات | 500 |

---

## 4. عقد كل Module

### 🔑 Auth Module

| Endpoint | Method | Auth | Headers | Request Body | Response |
|----------|--------|------|---------|--------------|----------|
| `/api/auth/login` | POST | ❌ | - | `LoginRequest` | `ApiResponse<LoginResponse>` |
| `/api/auth/register` | POST | ✅ | Bearer | `RegisterRequest` | `ApiResponse<bool>` |
| `/api/auth/me` | GET | ✅ | Bearer | - | `ApiResponse<UserInfo>` |

```typescript
interface LoginRequest {
  email: string;
  password: string;
}

interface LoginResponse {
  accessToken: string;
  expiresAt: string; // ISO 8601 UTC
  user: {
    id: number;
    name: string;
    email: string;
    role: 'Admin' | 'Cashier' | 'SystemOwner';
    permissions: string[]; // Permission enum values
  };
}
```

### New Permission Values

| Permission | Value | Usage |
|-----------|-------|-------|
| `OrdersCreate` | `202` | Create/update/complete/cancel orders and open/close shifts |
| `ExpensesManage` | `702` | Update and delete expenses |
| `CashRegisterManage` | `1001` | Deposit and withdraw cash register transactions |

---

### 📦 Products Module

| Endpoint | Method | Permission | Request | Response |
|----------|--------|------------|---------|----------|
| `/api/products` | GET | `ProductsView` | Query params | `ApiResponse<PagedResult<ProductDto>>` |
| `/api/products/{id}` | GET | `ProductsView` | - | `ApiResponse<ProductDto>` |
| `/api/products` | POST | `ProductsManage` | `CreateProductDto` | `ApiResponse<int>` |
| `/api/products/{id}` | PUT | `ProductsManage` | `UpdateProductDto` | `ApiResponse<bool>` |
| `/api/products/{id}` | DELETE | `ProductsManage` | - | `ApiResponse<bool>` |

```typescript
interface ProductDto {
  id: number;
  name: string;
  barcode?: string;
  price: number;          // NET price (بدون ضريبة)
  categoryId: number;
  categoryName: string;
  isActive: boolean;
  // currentBranchStock is transitional - will move to BranchInventory endpoint
  currentBranchStock?: number; // @deprecated
  // ❌ مفيش stockQuantity هنا — المخزن في BranchInventory
}

interface CreateProductDto {
  name: string;
  barcode?: string;
  price: number;          // لازم >= 0
  categoryId: number;
  isActive?: boolean;     // default: true
}

// ⚠️ تنبيه للفرونت: المخزن بيجي من endpoint منفصل
interface BranchInventoryDto {
  productId: number;
  productName: string;
  quantity: number;       // ← هنا المخزن الحقيقي (per branch)
  branchName: string;
}
```

---

### 🛒 Orders Module

| Endpoint | Method | Permission | Request | Response |
|----------|--------|------------|---------|----------|
| `/api/orders` | GET | `OrdersView` | Filter params | `ApiResponse<PagedResult<OrderDto>>` |
| `/api/orders/{id}` | GET | `OrdersView` | - | `ApiResponse<OrderDto>` |
| `/api/orders` | POST | `OrdersCreate` | `CreateOrderDto` | `ApiResponse<OrderDto>` |
| `/api/orders/{id}/items` | POST | `OrdersCreate` | `AddOrderItemDto` | `ApiResponse<OrderDto>` |
| `/api/orders/{id}/items/{itemId}` | DELETE | `OrdersCreate` | - | `ApiResponse<OrderDto>` |
| `/api/orders/{id}/complete` | POST | `OrdersCreate` | `CompleteOrderDto` | `ApiResponse<OrderDto>` |
| `/api/orders/{id}/cancel` | POST | `OrdersCreate` | - | `ApiResponse<OrderDto>` |
| `/api/orders/{id}/refund` | POST | `OrdersRefund` | `RefundDto` | `ApiResponse<OrderDto>` |

```typescript
// Code-first contract: keep the extended backend enum values below.
// ⚠️ مهم: OrderType values بالظبط زي الباك-اند
type OrderType = 'DineIn' | 'Takeaway' | 'Delivery' | 'Return';
type OrderStatus = 'Draft' | 'Pending' | 'Completed' | 'Cancelled' | 'Refunded' | 'PartiallyRefunded';
type PaymentMethod = 'Cash' | 'Card' | 'Fawry' | 'BankTransfer';

interface CreateOrderDto {
  orderType: OrderType;
  tableNumber?: string;   // للـ DineIn فقط
  customerId?: string;
  notes?: string;
}

interface AddOrderItemDto {
  productId: number;
  quantity: number;       // لازم > 0
  notes?: string;
}

interface CompleteOrderDto {
  paymentMethod: PaymentMethod;
  paidAmount: number;     // لازم >= totalAmount
  // الـ change بيتحسب في الباك: change = paidAmount - totalAmount
}

interface OrderDto {
  id: number;
  orderNumber: string;
  orderType: OrderType;
  status: OrderStatus;
  items: OrderItemDto[];
  subtotal: number;       // قبل الضريبة
  taxAmount: number;
  totalAmount: number;    // subtotal + taxAmount
  paidAmount?: number;
  change?: number;
  paymentMethod?: PaymentMethod;
  createdAt: string;      // ISO 8601 UTC
  completedAt?: string;
  customerId?: string;
  customerName?: string;
}

// 💡 Tax Calculation — الفرونت لازم يحسب بنفس الطريقة دي
// NET prices in DB, tax added at display/total time
// taxAmount = subtotal * (tenant.taxRate / 100)
// totalAmount = subtotal + taxAmount
```

---

### 💰 Payments & Shifts Module

| Endpoint | Method | Permission | Request | Response |
|----------|--------|------------|---------|----------|
| `/api/payments/order/{orderId}` | GET | `OrdersView` | - | `ApiResponse<PaymentDto[]>` |
| `/api/shifts/current` | GET | `OrdersView` | - | `ApiResponse<ShiftDto?>` |
| `/api/shifts/open` | POST | `OrdersCreate` | `OpenShiftDto` | `ApiResponse<ShiftDto>` |
| `/api/shifts/close` | POST | `OrdersCreate` | `CloseShiftDto` | `ApiResponse<ShiftDto>` |
| `/api/shifts/history` | GET | `OrdersView` | - | `ApiResponse<ShiftDto[]>` |
| `/api/shifts/{id}/handover` | POST | `ShiftsManage` | `HandoverShiftRequest` | `ApiResponse<ShiftDto>` |
| `/api/shifts/{id}/update-activity` | POST | `OrdersView` | - | `ApiResponse<ShiftDto>` |
| `/api/shifts/warnings` | GET | `OrdersView` | - | `ApiResponse<ShiftWarningDto>` |

```typescript
interface ShiftDto {
  id: number;
  openingBalance: number;
  closingBalance?: number;
  totalSales?: number;
  totalCash?: number;
}
```


### 📊 Reports Module

| Endpoint | Method | Permission | Notes |
|----------|--------|------------|-------|
| `/api/reports/daily` | GET | `ReportsView` | Daily summary report |
| `/api/reports/sales` | GET | `ReportsView` | Date range required |
| `/api/reports/daily/print` | POST | `ReportsView` | Sends print command |
| `/api/inventory-reports/branch/{branchId}` | GET | `ReportsView` | Branch inventory report |
| `/api/inventory-reports/unified` | GET | `ReportsView` + `Admin/SystemOwner` | Unified inventory across branches |
| `/api/inventory-reports/transfer-history` | GET | `ReportsView` | Transfer history report |
| `/api/inventory-reports/low-stock-summary` | GET | `ReportsView` | Low stock summary |
| `/api/inventory-reports/branch/{branchId}/export` | GET | `ReportsView` | CSV export for branch inventory |
| `/api/inventory-reports/unified/export` | GET | `ReportsView` + `Admin/SystemOwner` | CSV export for unified inventory |
| `/api/financial-reports/profit-loss` | GET | `ReportsView` | Profit & loss report |
| `/api/financial-reports/expenses` | GET | `ReportsView` | Expenses report |
| `/api/customer-reports/top-customers` | GET | `ReportsView` | Top customers report |
| `/api/customer-reports/debts` | GET | `ReportsView` | Customer debts report |
| `/api/customer-reports/activity` | GET | `ReportsView` | Customer activity report |
| `/api/employee-reports/cashier-performance` | GET | `ReportsView` | Cashier performance report |
| `/api/employee-reports/shifts` | GET | `ReportsView` | Detailed shifts report |
| `/api/employee-reports/sales` | GET | `ReportsView` | Sales by employee report |
| `/api/product-reports/movement` | GET | `ReportsView` | Product movement report |
| `/api/product-reports/profitability` | GET | `ReportsView` | Most profitable products |
| `/api/product-reports/slow` | GET | `ReportsView` | Slow-moving products |
| `/api/product-reports/cogs` | GET | `ReportsView` | Cost of goods sold report |
| `/api/supplier-reports/purchases` | GET | `ReportsView` | Supplier purchases report |
| `/api/supplier-reports/debts` | GET | `ReportsView` | Supplier debts report |
| `/api/supplier-reports/performance` | GET | `ReportsView` | Supplier performance report |

```typescript
interface ReportFilter {
  startDate: string;
  endDate: string;
  branchId?: number;
}
```


### 🏢 Tenants & Branches Module

| Endpoint | Method | Auth | Notes |
|----------|--------|------|-------|
| `/api/tenants` | GET | `Admin` | المدير بس |
| `/api/tenants/{id}` | GET | `Admin` | - |
| `/api/branches` | GET | `Authorize` | كل المستخدمين |
| `/api/branches/{id}` | GET | `Authorize` | - |
| `/api/branches` | POST | `Admin` | - |
| `/api/branches/{id}` | PUT | `Admin` | - |

```typescript
interface BranchDto {
  id: number;
  name: string;
  address?: string;
  phone?: string;
  isActive: boolean;
}

interface TenantDto {
  id: number;
  name: string;
  taxRate: number;        // مثلاً 14 (مش 0.14)
  isTaxEnabled: boolean;
  timezone: string;       // "Africa/Cairo"
  isActive: boolean;
}
```

---

### ⚙️ System Module

| Endpoint | Method | Auth | Notes |
|----------|--------|------|-------|
| `/api/admin/backup` | POST | `Admin/SystemOwner` | `ApiResponse<BackupResult>` - Creates manual backup |
| `/api/admin/restore` | POST | `Admin/SystemOwner` | `ApiResponse<RestoreResult>` - Restore from backup filename |
| `/api/admin/backups` | GET | `Admin/SystemOwner` | `ApiResponse<BackupInfo[]>` - List available backups |
| `/api/admin/restore/upload` | POST | `Admin/SystemOwner` | `ApiResponse<RestoreResult>` - Restore from uploaded `.db` backup |
| `/api/system/info` | GET | `Admin/SystemOwner` | LAN and host information |
| `/api/system/health` | GET | `AllowAnonymous` | Lightweight system health |
| `/api/health` | GET | `AllowAnonymous` | Primary API health check |
| `/api/health/deep` | GET | `Admin/SystemOwner` | Detailed health |


### 📡 Health Check Response

```typescript
interface BasicHealthResponse {
  status: 'healthy' | 'degraded' | 'unhealthy';
  timestamp: string;
  version: string;
  database: { status: string };
  uptime: string;
}

interface DeepHealthResponse {
  status: 'healthy' | 'degraded' | 'unhealthy';
  database: { status: string; responseTime: number; };
  disk: { freeGB: number; totalGB: number; };
  memory: { usedMB: number; totalMB: number; };
  uptime: number;
}
```

---

## 5. عقد الـ Real-time (SignalR)

### DeviceHub — `/hubs/devices`

```typescript
const connection = new signalR.HubConnectionBuilder()
  .withUrl('/hubs/devices', {
    accessTokenFactory: () => getToken(),
  })
  .withAutomaticReconnect()
  .build();
```

### Events من الـ Server → Client

| Event | البيانات | الموقف |
|-------|---------|--------|
| `PrintReceipt` | `PrintCommandDto` | إرسال أمر طباعة للإيصال أو التقرير |
| `PrintDebtPaymentReceipt` | `PrintCommandDto` | إرسال أمر طباعة لإيصال سداد مديونية عميل |

### Events من Client → Server

| Event | البيانات | الموقف |
|-------|---------|--------|
| `PrintCompleted` | `PrintCompletedEventDto` | Bridge App يبلغ الـ API بنتيجة الطباعة |

```typescript
connection.invoke('PrintCompleted', {
  commandId: string,
  success: boolean,
  errorMessage?: string,
  completedAt: string
});
```

---
## 6. قواعد الفرونت الإلزامية

### ❌ ممنوع تمامًا

```typescript
// 1. ❌ لا any type
const data: any = response;

// 2. ❌ لا magic strings للـ enums
order.status = "completed"; // غلط
order.status = 'Completed'; // ✅ صح

// 3. ❌ لا مقارنة على message نص
if (error.message === 'المنتج غير موجود') // غلط

// 4. ❌ لا hardcoded tax rate
const tax = total * 0.14; // غلط — جيبه من tenant.taxRate

// 5. ❌ لا استدعاء endpoints محذوفة
api.getCredentials(); // ❌ محذوف
api.migrateInventory(); // ❌ محذوف (dead code في systemApi.ts)

// 6. ❌ لا مقارنة على response.data?.success
if (response.data?.success === false) // استخدم unwrap() بدل كده
```

### ✅ إلزامي دايمًا

```typescript
// 1. ✅ Types تطابق DTOs الباك-اند بالظبط
// لو الباك عمل تغيير — الفرونت لازم يتحدث برضو

// 2. ✅ errorCode للـ UI logic مش message
const handleError = (err: ApiResponse<null>) => {
  if (err.errorCode === 'NO_OPEN_SHIFT') router.push('/shifts');
  if (err.errorCode === 'INSUFFICIENT_STOCK') showStockAlert();
};

// 3. ✅ التحقق من الشيفت قبل كل order
const { data: currentShift } = useGetCurrentShiftQuery();
if (!currentShift?.data?.id) return <NoShiftScreen />;

// 4. ✅ Tax calculation بنفس طريقة الباك-اند (Tax Exclusive)
const taxAmount = subtotal * (tenant.taxRate / 100);
const total = subtotal + taxAmount;

// 5. ✅ Idempotency Key للـ financial writes
headers['X-Idempotency-Key'] = crypto.randomUUID();

// 6. ✅ مخزن المنتجات يجي من BranchInventory مش Product
// ProductDto مفيهاش stockQuantity — استخدم inventory endpoint
```

### Pagination Pattern

```typescript
// كل list endpoint بيدعم pagination
interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// Query params
interface PaginationParams {
  page?: number;      // default: 1
  pageSize?: number;  // default: 20, max: 100
  search?: string;
  sortBy?: string;
  sortDesc?: boolean;
}
```

---

## 7. قواعد الباك-اند الإلزامية

### ❌ ممنوع تمامًا في الكود

```csharp
// 1. ❌ لا AutoMapper
_mapper.Map<ProductDto>(product);

// 2. ❌ لا FluentValidation
// كل validation يدوي في الـ Service مع ErrorCodes

// 3. ❌ لا ApiResponse.Fail بـ message فقط
ApiResponse<T>.Fail("الطلب غير موجود"); // frontend مش هيعرف يـ handle

// 4. ❌ لا hardcoded TenantId/BranchId
new Order { TenantId = 1, BranchId = 1 }; // danger!

// 5. ❌ لا query بدون TenantId filter
_context.Products.FirstOrDefaultAsync(p => p.Id == id);

// 6. ❌ لا transaction بدون await using var
await _unitOfWork.BeginTransactionAsync(); // connection leak!

// 7. ❌ لا Product.StockQuantity (اتشال في migration 20260329232433)
product.StockQuantity -= quantity;

// 8. ❌ لا silent catch
catch { } // بيخفي bugs — على الأقل log!
```

### ✅ إلزامي دايمًا

```csharp
// 1. ✅ كل endpoint = [HasPermission]
[HasPermission(Permission.ProductsManage)]
public async Task<IActionResult> Create(...)

// 2. ✅ كل query = TenantId + BranchId
.Where(p => p.TenantId == _currentUser.TenantId
         && p.BranchId == _currentUser.BranchId)

// 3. ✅ Transaction صحيحة
await using var transaction = await _unitOfWork.BeginTransactionAsync();
try { ... await transaction.CommitAsync(); }
catch { await transaction.RollbackAsync(); throw; }

// 4. ✅ Error format صح
ApiResponse<T>.Fail(ErrorCodes.ORDER_NOT_FOUND,
    ErrorMessages.Get(ErrorCodes.ORDER_NOT_FOUND))

// 5. ✅ Stock من BranchInventory
var inventory = await _context.BranchInventory
    .FirstOrDefaultAsync(i => i.ProductId == productId
                            && i.BranchId == _currentUser.BranchId
                            && i.TenantId == _currentUser.TenantId, ct);

// 6. ✅ DTO mapping
private static ProductDto MapToDto(Product p) => new() { ... };
// أو .Select(p => new ProductDto { ... })

// 7. ✅ UpdateSecurityStamp بعد password change
user.UpdateSecurityStamp();
await _unitOfWork.SaveChangesAsync(ct);
```

---

## 8. Checklist قبل كل Feature جديد

### الباك-اند

```
قبل الكتابة:
- [ ] حددت الـ TenantId + BranchId scope للـ entity؟
- [ ] الـ operation مالية؟ → Transaction إلزامية
- [ ] عرّفت الـ Permission الجديدة لو محتاج؟

أثناء الكتابة:
- [ ] كل query فيها TenantId filter؟
- [ ] كل entity creation بتستخدم ICurrentUserService؟
- [ ] كل endpoint فيه [HasPermission]؟
- [ ] الـ error responses كلها بـ ErrorCodes + ErrorMessages.Get()?
- [ ] مفيش AutoMapper أو FluentValidation؟
- [ ] مفيش Product.StockQuantity؟

قبل الـ Commit:
- [ ] اتعملت integration test للـ security (cross-tenant)?
- [ ] الـ DTO types متزامنة مع الفرونت؟
- [ ] الـ API doc اتحدث في docs/001_API_DOCUMENTATION.md؟
- [ ] أضفت الـ ErrorCodes الجديدة للجدول في الوثيقة دي؟
```

### الفرونت-اند

```
قبل الكتابة:
- [ ] اتأكدت إن الـ endpoint موجود في الباك-اند فعلاً؟
- [ ] اتأكدت إن الـ types بتطابق الـ DTOs؟
- [ ] المستخدم عنده الـ permission المطلوبة؟

أثناء الكتابة:
- [ ] بتستخدم errorCode مش message للـ UI logic?
- [ ] بتحسب الضريبة بنفس طريقة الباك-اند؟
- [ ] مفيش any types?
- [ ] المخزن جاي من BranchInventory مش Product?
- [ ] Financial writes فيها X-Idempotency-Key?

قبل الـ Commit:
- [ ] اختبرت لو المستخدم مش عنده permission؟
- [ ] اختبرت لو مفيش شيفت مفتوح؟
- [ ] اختبرت الـ error states كلها؟
```

### لو في Endpoint جديد أو اتغير

```
📢 لازم يحصل الاتنين مع بعض:
1. الباك-اند يضيف الـ endpoint + يحدث الوثيقة دي
2. الفرونت يضيف الـ RTK Query + يحدث الـ Types

⚠️ مينفعش حد يغير الباك-اند من غير ما الفرونت يعرف والعكس.
```

---

## 📌 Quick Reference Card (للـ AI Agents)

```
الأسئلة المهمة قبل أي كود:

1. مالية؟          → await using var transaction = ...
2. بتقرأ Data؟     → TenantId + BranchId filter + AsNoTracking
3. بتكتب Data؟     → TenantId + BranchId من ICurrentUserService
4. Error؟          → ApiResponse.Fail(ErrorCodes.X, ErrorMessages.Get(X))
5. Validation؟     → يدوي في الـ Service (مش FluentValidation)
6. Mapping؟        → MapToDto() أو .Select() (مش AutoMapper)
7. Stock؟          → BranchInventory.Quantity (مش Product.StockQuantity)
8. Frontend path?  → frontend/ (مش client/)
9. Permission؟     → [HasPermission(Permission.X)] (مش [Authorize] بس)
10. Password reset? → UpdateSecurityStamp() بعده
```

---

> **Document Owner:** Principal Software Architect  
> **Last Updated:** April 2, 2026  
> **Review Trigger:** عند أي تغيير في DTOs أو Endpoints أو Error Codes  
> **الهدف:** Zero surprises بين الباك-اند والفرونت





