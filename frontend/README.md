# 🏪 KasserPro Frontend — Complete Developer Guide

> **Production-Grade POS System Frontend**  
> React 18 + TypeScript + Redux Toolkit + RTK Query + TailwindCSS

This is NOT a simple CRUD app. This is a complex, multi-tenant, real-time Point of Sale system with inventory management, financial reporting, and role-based access control.

---

## 📚 Table of Contents

1. [Frontend Overview](#-frontend-overview)
2. [Architecture Deep Dive](#-architecture-deep-dive)
3. [Folder Structure Explained](#-folder-structure-explained)
4. [State Management Strategy](#-state-management-strategy)
5. [API Integration](#-api-integration)
6. [Data Flow](#-data-flow)
7. [Authentication & Authorization](#-authentication--authorization)
8. [Forms & Validation](#-forms--validation)
9. [Error Handling](#-error-handling)
10. [UI & Styling System](#-ui--styling-system)
11. [How to Add New Features](#-how-to-add-new-features)
12. [Performance Considerations](#-performance-considerations)
13. [Testing](#-testing)
14. [Common Pitfalls & Rules](#-common-pitfalls--rules)
15. [AI Agent Instructions](#-ai-agent-instructions)

---

## 🎯 Frontend Overview

### What is KasserPro?

KasserPro is a **multi-tenant, multi-branch Point of Sale (POS) system** designed for retail businesses. The frontend is the primary interface for:

- **Cashiers**: Process orders, manage shifts, handle payments
- **Admins**: Manage products, inventory, users, view reports
- **System Owners**: Manage multiple tenants (companies)

### Core User Flows

```
┌─────────────────────────────────────────────────────────────┐
│                    CASHIER DAILY FLOW                       │
├─────────────────────────────────────────────────────────────┤
│ 1. Login → Select Branch                                    │
│ 2. Open Shift (with opening balance)                        │
│ 3. Create Orders:                                           │
│    - Scan/search products                                   │
│    - Add to cart                                            │
│    - Apply discounts                                        │
│    - Process payment (Cash/Card/Fawry)                      │
│    - Print receipt                                          │
│ 4. Handle Returns/Refunds                                   │
│ 5. Close Shift (with closing balance)                       │
│ 6. Logout                                                   │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                     ADMIN DAILY FLOW                        │
├─────────────────────────────────────────────────────────────┤
│ 1. Login → Select Branch                                    │
│ 2. Manage Products & Inventory                              │
│ 3. View Reports (Sales, Inventory, Financial)               │
│ 4. Manage Users & Permissions                               │
│ 5. Handle Purchase Invoices                                 │
│ 6. Manage Expenses                                          │
│ 7. Backup/Restore Database                                  │
└─────────────────────────────────────────────────────────────┘
```

### Key Features

- **Real-time POS**: Fast order creation with barcode scanning
- **Multi-tenant**: Each company (tenant) has isolated data
- **Multi-branch**: Each tenant can have multiple branches
- **Inventory Tracking**: Per-branch stock management
- **Shift Management**: Opening/closing balances with cash reconciliation
- **Financial Reports**: Sales, profit/loss, expenses
- **Role-based Access**: Admin, Cashier, SystemOwner roles with granular permissions
- **Offline-first**: Works with intermittent connectivity (retry logic)
- **Arabic RTL**: Full Arabic language support with RTL layout

---

## 🏗️ Architecture Deep Dive

### Component Architecture

We follow a **feature-based component architecture** with clear separation of concerns:

```
┌─────────────────────────────────────────────────────────────┐
│                    COMPONENT HIERARCHY                      │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Pages (Route Components)                                   │
│    ↓                                                        │
│  Feature Components (Smart Components)                      │
│    ↓                                                        │
│  UI Components (Presentational Components)                  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

#### 1. Pages (Route Components)

- **Location**: `src/pages/`
- **Purpose**: Top-level route components
- **Responsibilities**:
  - Handle routing
  - Fetch data using RTK Query hooks
  - Pass data to feature components
  - Handle page-level state
- **Example**: `POSPage.tsx`, `ProductsPage.tsx`

```typescript
// Example: pages/products/ProductsPage.tsx
const ProductsPage = () => {
  const { data, isLoading } = useGetProductsQuery();
  const [createProduct] = useCreateProductMutation();

  return (
    <div>
      <ProductList products={data?.data} />
      <ProductForm onSubmit={createProduct} />
    </div>
  );
};
```

#### 2. Feature Components (Smart Components)

- **Location**: `src/components/{feature}/`
- **Purpose**: Feature-specific business logic
- **Responsibilities**:
  - Connect to Redux store
  - Handle feature-specific state
  - Orchestrate UI components
  - Handle user interactions
- **Example**: `components/pos/Cart.tsx`, `components/products/ProductFormModal.tsx`

```typescript
// Example: components/products/ProductFormModal.tsx
const ProductFormModal = ({ product, onClose }: ProductFormModalProps) => {
  const [createProduct] = useCreateProductMutation();
  const [updateProduct] = useUpdateProductMutation();

  const handleSubmit = async (data: CreateProductDto) => {
    try {
      if (product) {
        await updateProduct({ id: product.id, dto: data }).unwrap();
      } else {
        await createProduct(data).unwrap();
      }
      toast.success('تم الحفظ بنجاح');
      onClose();
    } catch (error) {
      toast.error('فشل الحفظ');
    }
  };

  return <ProductForm onSubmit={handleSubmit} initialData={product} />;
};
```

#### 3. UI Components (Presentational Components)

- **Location**: `src/components/common/`
- **Purpose**: Reusable, generic UI elements
- **Responsibilities**:
  - Render UI based on props
  - No business logic
  - No Redux connections
  - Highly reusable
- **Example**: `Button`, `Input`, `Modal`, `Card`, `Loading`, `Portal`

```typescript
// Example: components/common/Button.tsx
interface ButtonProps {
  children: React.ReactNode;
  onClick?: () => void;
  variant?: 'primary' | 'secondary' | 'danger';
  disabled?: boolean;
}

const Button = ({ children, onClick, variant = 'primary', disabled }: ButtonProps) => {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      className={cn(
        'px-4 py-2 rounded-lg',
        variant === 'primary' && 'bg-primary-600 text-white',
        variant === 'danger' && 'bg-danger-600 text-white'
      )}
    >
      {children}
    </button>
  );
};
```

### Feature-Based Structure

Instead of grouping by type (all components together), we group by feature:

```
components/
├── pos/              # POS-specific components
│   ├── Cart.tsx
│   ├── ProductGrid.tsx
│   ├── OrderSummary.tsx
│   └── PaymentModal.tsx
├── products/         # Product management components
│   ├── ProductList.tsx
│   ├── ProductForm.tsx
│   └── ProductCard.tsx
├── orders/           # Order management components
├── shifts/           # Shift management components
└── common/           # Shared UI components
```

**Why?** This makes it easier to:

- Find related code
- Understand feature boundaries
- Refactor features independently
- Delete features cleanly

---

## 📁 Folder Structure Explained

### Complete Directory Map

```
frontend/
├── src/
│   ├── api/              # RTK Query API definitions (25+ files)
│   ├── components/       # React components organized by feature
│   ├── hooks/            # Custom React hooks
│   ├── lib/              # Third-party library configurations
│   ├── pages/            # Route components (20+ pages)
│   ├── store/            # Redux store configuration
│   ├── styles/           # Global styles
│   ├── types/            # TypeScript type definitions (25+ files)
│   ├── utils/            # Utility functions
│   ├── App.tsx           # Root component with routing
│   ├── main.tsx          # Application entry point
│   └── index.css         # Global CSS with Tailwind
├── e2e/                  # Playwright E2E tests
├── public/               # Static assets
├── package.json          # Dependencies
├── tsconfig.json         # TypeScript configuration
├── tailwind.config.js    # Tailwind CSS configuration
└── vite.config.ts        # Vite build configuration
```

### Import Alias

The project uses `@` as an alias for `src/`:

```typescript
// ✅ Correct - Using alias
import { Button } from "@/components/common/Button";
import { useAuth } from "@/hooks/useAuth";
import { Product } from "@/types/product.types";

// ❌ Avoid - Relative paths
import { Button } from "../../components/common/Button";
```

**Configuration** (in `vite.config.ts`):

```typescript
resolve: {
  alias: {
    "@": path.resolve(__dirname, "./src"),
  },
}
```

### 🔥 Critical: `src/api/` — RTK Query Endpoints

**Purpose**: Define ALL API communication with the backend

**Structure**:

```
api/
├── baseApi.ts           # ⭐ Base API configuration (MOST IMPORTANT)
├── authApi.ts           # Authentication endpoints
├── productsApi.ts       # Product CRUD
├── ordersApi.ts         # Order management
├── shiftsApi.ts         # Shift operations
├── customersApi.ts      # Customer management
├── reportsApi.ts        # Reporting endpoints
└── ... (25+ API files)
```

**When to add a new file**:

- Creating a new feature module (e.g., `discountsApi.ts`)
- Backend adds a new controller (e.g., `/api/promotions`)

**When to modify existing file**:

- Backend adds new endpoints to existing controller
- Need to change request/response types

**Example Structure**:

```typescript
// api/productsApi.ts
import { baseApi } from "./baseApi";
import { Product, CreateProductDto } from "../types/product.types";
import { ApiResponse } from "../types/api.types";

export const productsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    // Query = GET request
    getProducts: builder.query<ApiResponse<Product[]>, void>({
      query: () => "/products",
      providesTags: ["Products"], // Cache tag for invalidation
    }),

    // Mutation = POST/PUT/DELETE request
    createProduct: builder.mutation<ApiResponse<number>, CreateProductDto>({
      query: (dto) => ({
        url: "/products",
        method: "POST",
        body: dto,
      }),
      invalidatesTags: ["Products"], // Invalidate cache after mutation
    }),
  }),
});

// Export hooks for use in components
export const { useGetProductsQuery, useCreateProductMutation } = productsApi;
```

### 🎨 `src/components/` — Component Organization

**Structure by Feature**:

```
components/
├── common/              # Shared UI components
│   ├── Button.tsx
│   ├── Card.tsx
│   ├── Input.tsx
│   ├── Loading.tsx
│   ├── Modal.tsx
│   ├── Portal.tsx
│   └── TaxSettingsSync.tsx
├── layout/              # Layout components
│   ├── MainLayout.tsx
│   ├── BranchSelector.tsx
│   └── NavItemWithSubmenu.tsx
├── pos/                 # POS feature components
│   ├── Cart.tsx
│   ├── ProductGrid.tsx
│   ├── OrderSummary.tsx
│   ├── PaymentModal.tsx
│   └── DiscountModal.tsx
├── products/            # Product management
│   └── ProductFormModal.tsx
├── orders/              # Order management
├── shifts/              # Shift management
├── customers/           # Customer management
├── inventory/           # Inventory management
├── suppliers/           # Supplier management
├── branches/            # Branch management
└── ErrorBoundary.tsx    # Global error boundary
```

**Rules**:

- ✅ Group by feature, not by type
- ✅ Keep feature components in their own folder
- ✅ Shared components go in `common/`
- ❌ Don't create deeply nested folders (max 2 levels)

### 🪝 `src/hooks/` — Custom React Hooks

**Purpose**: Reusable stateful logic

**Current Hooks**:

```
hooks/
├── useAuth.ts           # Authentication logic (login, logout, user state)
├── useCart.ts           # Cart operations
├── useInactivityMonitor.ts  # Auto-logout on inactivity
├── useNumberInput.ts    # Numeric input handling (for POS)
├── useOrders.ts         # Order operations
├── usePermission.ts     # Permission checking
├── usePOSMode.ts        # POS mode state management
├── usePOSShortcuts.ts   # Keyboard shortcuts for POS (F1-F12)
├── useProducts.ts       # Product operations
└── useShift.ts          # Shift management
```

**When to create a new hook**:

- Reusable stateful logic used in 2+ components
- Complex logic that clutters components
- Side effects that need cleanup

**Example**:

```typescript
// hooks/useAuth.ts
export const useAuth = () => {
  const dispatch = useAppDispatch();
  const user = useAppSelector(selectCurrentUser);
  const [loginMutation] = useLoginMutation();

  const login = async (credentials: LoginRequest) => {
    const response = await loginMutation(credentials).unwrap();
    dispatch(setCredentials(response.data));
  };

  const logout = () => {
    dispatch(logoutAction());
  };

  return { user, login, logout };
};
```

### 📄 `src/pages/` — Route Components

**Purpose**: Top-level components for each route

**Structure**:

```
pages/
├── auth/
│   └── LoginPage.tsx
├── pos/
│   ├── POSPage.tsx           # Main POS interface
│   └── POSWorkspacePage.tsx  # Alternative POS layout
├── products/
│   └── ProductsPage.tsx
├── orders/
│   └── OrdersPage.tsx
├── shifts/
│   ├── ShiftPage.tsx
│   └── ShiftsManagementPage.tsx
├── reports/
│   ├── ReportsDashboardPage.tsx
│   ├── DailyReportPage.tsx
│   ├── SalesReportPage.tsx
│   └── ... (15+ report pages)
├── customers/
├── suppliers/
├── inventory/
├── expenses/
├── backup/
└── NotFound.tsx
```

**Page Component Pattern**:

```typescript
const ProductsPage = () => {
  // 1. Fetch data
  const { data, isLoading, error } = useGetProductsQuery();

  // 2. Mutations
  const [createProduct] = useCreateProductMutation();

  // 3. Local state
  const [isModalOpen, setIsModalOpen] = useState(false);

  // 4. Handlers
  const handleCreate = async (dto: CreateProductDto) => {
    await createProduct(dto).unwrap();
    setIsModalOpen(false);
  };

  // 5. Render
  if (isLoading) return <LoadingSpinner />;
  if (error) return <ErrorMessage />;

  return (
    <div>
      <ProductList products={data?.data} />
      <ProductFormModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSubmit={handleCreate}
      />
    </div>
  );
};
```

### 🗄️ `src/store/` — Redux Store Configuration

**Structure**:

```
store/
├── slices/
│   ├── authSlice.ts      # Authentication state
│   ├── cartSlice.ts      # Shopping cart state
│   ├── branchSlice.ts    # Current branch selection
│   └── uiSlice.ts        # UI state (modals, sidebar)
├── middleware/           # Custom Redux middleware
├── hooks.ts              # Typed Redux hooks (useAppDispatch, useAppSelector)
└── index.ts              # Store configuration
```

**When to create a new slice**:

- Need client-side state that persists across components
- State is NOT server data (use RTK Query for server data)
- Examples: UI state, form drafts, user preferences

**When NOT to create a slice**:

- ❌ Server data (products, orders) → Use RTK Query
- ❌ Component-local state → Use `useState`
- ❌ Derived state → Use selectors

### 📦 `src/types/` — TypeScript Type Definitions

**Purpose**: Type definitions that MUST match backend DTOs

**Structure**:

```
types/
├── api.types.ts          # Generic API types (ApiResponse, PaginatedResponse)
├── auth.types.ts         # User, LoginRequest, LoginResponse
├── product.types.ts      # Product, CreateProductDto, UpdateProductDto
├── order.types.ts        # Order, OrderItem, CreateOrderDto
├── shift.types.ts        # Shift, OpenShiftDto, CloseShiftDto
├── customer.types.ts     # Customer, CreateCustomerDto
├── inventory.types.ts    # BranchInventory, TransferDto
└── ... (25+ type files)
```

**⚠️ CRITICAL RULE**: Types MUST match backend DTOs exactly

```typescript
// ✅ CORRECT - Matches backend DTO
interface CreateProductDto {
  name: string;
  barcode?: string;
  price: number;
  categoryId: number;
  isActive?: boolean;
}

// ❌ WRONG - Doesn't match backend
interface CreateProductDto {
  productName: string; // Backend uses 'name'
  cost: number; // Backend uses 'price'
}
```

### 🛠️ `src/utils/` — Utility Functions

**Current Utilities**:

```
utils/
├── apiResponse.ts       # Extract data from ApiResponse<T>
├── constants.ts         # App-wide constants (ERROR_MESSAGES, ORDER_STATUS, PAYMENT_METHODS)
├── errorHandler.ts      # Centralized error handling
├── formatters.ts        # Date, currency, number formatting
├── productStock.ts      # Stock calculation helpers (buildBranchInventoryStockMap, getProductCurrentStock)
└── shiftPersistence.ts  # Shift auto-save logic (localStorage-based)
```

**When to add a utility**:

- Pure function used in 3+ places
- Complex calculation/transformation
- No side effects

---

## 🔄 State Management Strategy

### The Two Types of State

```
┌─────────────────────────────────────────────────────────────┐
│                    STATE ARCHITECTURE                       │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  SERVER STATE (RTK Query)                                   │
│  - Products, Orders, Customers, Reports                     │
│  - Cached, auto-refetched, invalidated                      │
│  - Source of truth: Backend API                             │
│                                                             │
│  CLIENT STATE (Redux Slices)                                │
│  - Auth, Cart, Branch Selection, UI State                   │
│  - Persisted (auth), ephemeral (cart), or session-based     │
│  - Source of truth: Frontend                                │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### RTK Query — Server State Management

**Why RTK Query?**

- Automatic caching
- Automatic refetching
- Optimistic updates
- Request deduplication
- Built-in loading/error states

**Core Concepts**:

#### 1. Queries (GET requests)

```typescript
// Define query
getProducts: builder.query<ApiResponse<Product[]>, void>({
  query: () => "/products",
  providesTags: ["Products"], // Tag for cache invalidation
});

// Use in component
const { data, isLoading, error, refetch } = useGetProductsQuery();

// Access data
const products = data?.data; // ApiResponse<Product[]>.data
```

#### 2. Mutations (POST/PUT/DELETE requests)

```typescript
// Define mutation
createProduct: builder.mutation<ApiResponse<number>, CreateProductDto>({
  query: (dto) => ({
    url: "/products",
    method: "POST",
    body: dto,
  }),
  invalidatesTags: ["Products"], // Refetch all queries with 'Products' tag
});

// Use in component
const [createProduct, { isLoading }] = useCreateProductMutation();

const handleSubmit = async (dto: CreateProductDto) => {
  try {
    const response = await createProduct(dto).unwrap();
    toast.success("تم إنشاء المنتج");
  } catch (error) {
    toast.error("فشل إنشاء المنتج");
  }
};
```

#### 3. Cache Invalidation Strategy

**Tags** define cache boundaries:

```typescript
tagTypes: [
  "Products",
  "Categories",
  "Orders",
  "Shifts",
  "Customers",
  "Inventory",
  // ... more
];
```

**Invalidation Flow**:

```
User creates product
  ↓
createProduct mutation executes
  ↓
invalidatesTags: ['Products']
  ↓
All queries with providesTags: ['Products'] refetch automatically
  ↓
UI updates with fresh data
```

**Example**:

```typescript
// Query provides tag
getProducts: builder.query({
  query: () => "/products",
  providesTags: ["Products"],
});

// Mutation invalidates tag
createProduct: builder.mutation({
  query: (dto) => ({ url: "/products", method: "POST", body: dto }),
  invalidatesTags: ["Products"], // ← Triggers refetch of getProducts
});
```

#### 4. Conditional Fetching

```typescript
// Skip query if condition not met
const { data } = useGetCurrentShiftQuery(undefined, {
  skip: !isAuthenticated, // Don't fetch if not logged in
});

// Polling (auto-refetch every X seconds)
const { data } = useGetOrdersQuery(undefined, {
  pollingInterval: 30000, // Refetch every 30 seconds
});
```

### Redux Slices — Client State Management

**Current Slices**:

#### 1. `authSlice` — Authentication State

```typescript
interface AuthState {
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
}

// Actions
setCredentials({ user, token }); // Login
logout(); // Logout
updateUser(partialUser); // Update user info

// Selectors
selectCurrentUser(state);
selectIsAuthenticated(state);
selectIsAdmin(state);
```

**Persistence**: Persisted to localStorage via `redux-persist`

#### 2. `cartSlice` — Shopping Cart State

```typescript
interface CartState {
  items: CartItem[];
  taxRate: number;
  isTaxEnabled: boolean;
  allowNegativeStock: boolean;
  discountType?: "Percentage" | "Fixed";
  discountValue?: number;
}

// Actions
addItem({ product, quantity });
removeItem(productId);
updateQuantity({ productId, quantity });
updateNotes({ productId, notes });
setDiscount({ type, value });
setItemDiscount({ productId, discount });
setTaxSettings({ taxRate, isTaxEnabled, allowNegativeStock });
clearCart();

// Selectors
selectCartItems(state);
selectSubtotal(state);
selectTaxAmount(state);
selectTotal(state);
selectDiscountAmount(state);
selectItemDiscountsTotal(state);
```

**Why not RTK Query?** Cart is client-side only until order is created

#### 3. `branchSlice` — Branch Selection

```typescript
interface BranchState {
  currentBranch: Branch | null;
  branches: Branch[];
}

// Actions
setCurrentBranch(branch: Branch | null)
setBranches(branches: Branch[] | SetBranchesPayload)
clearBranch()

// Selectors
selectCurrentBranch(state)
selectBranches(state)
```

**Important**: NOT persisted (cleared on logout to prevent branch mismatch)

#### 4. `uiSlice` — UI State

```typescript
interface UiState {
  isPaymentModalOpen: boolean;
  isReceiptModalOpen: boolean;
  isSidebarOpen: boolean;
  currentOrderId: number | null;
}

// Actions
openPaymentModal()
closePaymentModal()
openReceiptModal(orderId: number)
closeReceiptModal()
toggleSidebar()

// Selectors
selectIsPaymentModalOpen(state)
selectIsReceiptModalOpen(state)
selectIsSidebarOpen(state)
selectCurrentOrderId(state)
```

### When to Use What?

| State Type      | Use                                    | Example                       |
| --------------- | -------------------------------------- | ----------------------------- |
| **RTK Query**   | Server data                            | Products, Orders, Customers   |
| **Redux Slice** | Client state across components         | Auth, Cart, Branch            |
| **useState**    | Component-local state                  | Form inputs, modal open/close |
| **useRef**      | Non-reactive values                    | DOM refs, timers              |
| **Context**     | Rarely used (Redux handles most cases) | Theme (if needed)             |

### Caching Strategy

**RTK Query Cache Configuration**:

```typescript
// baseApi.ts
export const baseApi = createApi({
  // ...
  refetchOnFocus: true, // Refetch when window regains focus
  refetchOnReconnect: true, // Refetch when network reconnects
  keepUnusedDataFor: 60, // Keep cache for 60 seconds after last use
});
```

**Cache Behavior**:

- First request: Fetch from API
- Subsequent requests (within 60s): Return cached data
- After 60s of no usage: Cache cleared
- On focus/reconnect: Refetch to ensure fresh data

---

## 🌐 API Integration

### Base API Configuration

**File**: `src/api/baseApi.ts` (⭐ MOST IMPORTANT FILE)

```typescript
// Dynamic API URL based on environment
const getApiUrl = (): string => {
  if (import.meta.env.DEV) {
    return "/api"; // Vite proxy in development
  }
  return `${window.location.origin}/api`; // Production
};

// Base query with auth headers
const baseQuery = fetchBaseQuery({
  baseUrl: API_URL,
  prepareHeaders: (headers, { getState }) => {
    const state = getState() as RootState;
    const token = state.auth.token;
    const branchId = state.branch?.currentBranch?.id;

    if (token) {
      headers.set("Authorization", `Bearer ${token}`);
    }
    if (branchId) {
      headers.set("X-Branch-Id", branchId.toString());
    }
    return headers;
  },
});
```

### Global Error Handling

**Automatic Error Handling in `baseApi.ts`**:

```typescript
const baseQueryWithReauth = retry(
  async (args, api, extraOptions) => {
    const result = await baseQuery(args, api, extraOptions);

    if (result.error) {
      const error = result.error as FetchBaseQueryError;

      // 401 Unauthorized → Logout
      if (error.status === 401) {
        localStorage.removeItem("persist:auth");
        api.dispatch({ type: "auth/logout" });
        window.location.href = "/login";
        retry.fail(error);
        return result;
      }

      // 403 Forbidden → Show error
      if (error.status === 403) {
        const errorData = error.data as ApiErrorResponse;
        toast.error(errorData.message || "ليس لديك صلاحية");
        retry.fail(error);
        return result;
      }

      // 500 Server Error → Retry (for GET only)
      if (error.status === 500) {
        toast.error("حدث خطأ في الخادم");
        // Retry will happen automatically
        return result;
      }

      // Network error → Retry (for GET only)
      if (error.status === "FETCH_ERROR") {
        toast.error("لا يوجد اتصال بالإنترنت");
        // Retry will happen automatically
        return result;
      }
    }

    return result;
  },
  { maxRetries: 3 },
);
```

### Critical: Mutation Safety

**⚠️ NEVER RETRY MUTATIONS**

```typescript
// P0-7: NEVER retry mutations (POST, PUT, DELETE).
// Retrying a payment or order completion can cause double-charges.
// Only GET requests (queries) are safe to retry.

const isMutation =
  typeof args === "object" &&
  args !== null &&
  "method" in args &&
  ["POST", "PUT", "DELETE"].includes(args.method);

if (isMutation) {
  // Show error but do NOT retry
  if (error.status === "FETCH_ERROR") {
    toast.error("فشل الاتصال. تحقق من الشبكة وحاول يدوياً.");
  }
  retry.fail(error); // Stop retry
  return result;
}
```

### Headers Required by Backend

| Header              | Required              | Value              | Purpose                      |
| ------------------- | --------------------- | ------------------ | ---------------------------- |
| `Authorization`     | ✅ (except login)     | `Bearer {token}`   | JWT authentication           |
| `X-Branch-Id`       | ✅ (most endpoints)   | `{branchId}`       | Multi-branch isolation       |
| `X-Idempotency-Key` | ✅ (financial writes) | `{uuid}`           | Prevent duplicate operations |
| `Content-Type`      | ✅ (POST/PUT)         | `application/json` | Request body format          |

### Error Response Format

**Backend always returns**:

```typescript
interface ApiResponse<T> {
  success: boolean;
  data?: T;
  message?: string;
  errorCode?: string; // ← Use this for logic, not message
  errors?: string[];
}
```

**Frontend handling**:

```typescript
try {
  const response = await createOrder(dto).unwrap();
  // response is ApiResponse<OrderDto>
  const order = response.data;
} catch (error) {
  const apiError = error as { data: ApiResponse<null> };

  // ✅ Use errorCode for logic
  if (apiError.data?.errorCode === "NO_OPEN_SHIFT") {
    navigate("/shift");
  } else if (apiError.data?.errorCode === "INSUFFICIENT_STOCK") {
    toast.error("المخزون غير كافٍ");
  } else {
    // ✅ Use message for display
    toast.error(apiError.data?.message || "حدث خطأ");
  }
}
```

---

## 🔀 Data Flow

### Complete Request-Response Lifecycle

```
┌─────────────────────────────────────────────────────────────┐
│                    DATA FLOW DIAGRAM                        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  User Action (Click Button)                                 │
│         ↓                                                   │
│  Component Event Handler                                    │
│         ↓                                                   │
│  RTK Query Hook (useCreateOrderMutation)                    │
│         ↓                                                   │
│  baseQuery (Add Auth Headers)                               │
│         ↓                                                   │
│  HTTP Request → Backend API                                 │
│         ↓                                                   │
│  Backend Processing                                         │
│         ↓                                                   │
│  HTTP Response ← Backend API                                │
│         ↓                                                   │
│  baseQueryWithReauth (Error Handling)                       │
│         ↓                                                   │
│  Cache Update (invalidatesTags)                             │
│         ↓                                                   │
│  Component Re-render (with new data)                        │
│         ↓                                                   │
│  UI Update                                                  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Example: Creating an Order

**Step-by-Step Flow**:

```typescript
// 1. User clicks "Complete Order" button
<Button onClick={handleCompleteOrder}>إتمام الطلب</Button>

// 2. Component handler
const handleCompleteOrder = async () => {
  try {
    // 3. Call RTK Query mutation
    const response = await completeOrder({
      orderId: currentOrder.id,
      paymentMethod: 'Cash',
      paidAmount: 100,
    }).unwrap();

    // 4. Success handling
    const order = response.data;
    toast.success('تم إتمام الطلب');
    navigate('/orders');

  } catch (error) {
    // 5. Error handling
    const apiError = error as { data: ApiResponse<null> };
    if (apiError.data?.errorCode === 'NO_OPEN_SHIFT') {
      toast.error('يجب فتح وردية أولاً');
      navigate('/shift');
    }
  }
};

// Behind the scenes:
// 6. baseQuery adds headers:
//    - Authorization: Bearer {token}
//    - X-Branch-Id: {branchId}
//    - X-Idempotency-Key: {uuid}

// 7. HTTP POST /api/orders/{id}/complete

// 8. Backend processes:
//    - Validates shift is open
//    - Checks inventory
//    - Creates payment record
//    - Updates order status
//    - Returns ApiResponse<OrderDto>

// 9. Frontend receives response:
//    - If success: Cache invalidated, UI updates
//    - If error: Error handler shows toast

// 10. Cache invalidation triggers:
//     - useGetOrdersQuery refetches
//     - useGetCurrentShiftQuery refetches
//     - UI shows updated data
```

### State Update Flow

```
┌─────────────────────────────────────────────────────────────┐
│              STATE UPDATE MECHANISMS                        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  SERVER STATE (RTK Query)                                   │
│  ─────────────────────────                                  │
│  Mutation → invalidatesTags → Queries refetch → UI updates  │
│                                                             │
│  CLIENT STATE (Redux Slice)                                 │
│  ───────────────────────────                                │
│  dispatch(action) → Reducer updates state → UI updates      │
│                                                             │
│  LOCAL STATE (useState)                                     │
│  ───────────────────────                                    │
│  setState(newValue) → Component re-renders                  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Example: Adding Product to Cart

```typescript
// 1. User scans barcode or clicks product
const handleAddToCart = (product: Product) => {
  // 2. Dispatch Redux action (client state)
  dispatch(addItem({ product, quantity: 1 }));

  // 3. Reducer updates cart state
  // cartSlice.ts: state.items.push({ product, quantity: 1 })

  // 4. Selector recomputes derived values
  // selectTotal(state) recalculates total

  // 5. Components using selectors re-render
  // Cart component shows new item
  // OrderSummary shows updated total
};
```

---

## 🔐 Authentication & Authorization

### Authentication Flow

```
┌─────────────────────────────────────────────────────────────┐
│                  AUTHENTICATION FLOW                        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. User enters email + password                            │
│         ↓                                                   │
│  2. POST /api/auth/login                                    │
│         ↓                                                   │
│  3. Backend validates credentials                           │
│         ↓                                                   │
│  4. Backend returns JWT token + user info                   │
│         ↓                                                   │
│  5. Frontend stores in Redux (persisted to localStorage)    │
│         ↓                                                   │
│  6. All subsequent requests include:                        │
│     - Authorization: Bearer {token}                         │
│     - X-Branch-Id: {branchId}                               │
│         ↓                                                   │
│  7. Backend validates token on every request                │
│         ↓                                                   │
│  8. If token expired/invalid → 401 → Auto logout            │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### JWT Token Structure

```typescript
// Token payload (decoded)
{
  userId: number;
  tenantId: number;
  branchId: number;
  role: 'Admin' | 'Cashier' | 'SystemOwner';
  permissions: string[];  // ['ProductsView', 'OrdersCreate', ...]
  securityStamp: string;  // Changes on password reset
  exp: number;            // Expiration timestamp
}
```

### Token Validation on Startup

```typescript
// App.tsx - Validates token on app load
useEffect(() => {
  if (!isAuthenticated || !token) return;

  try {
    const parts = token.split(".");
    const payload = JSON.parse(atob(parts[1]));
    const exp = payload.exp;

    if (exp) {
      const now = Math.floor(Date.now() / 1000);
      if (now >= exp) {
        // Token expired - logout immediately
        localStorage.removeItem("persist:auth");
        dispatch(logoutAction());
        dispatch(clearBranch());
      }
    }
  } catch (e) {
    // Malformed token - logout
    localStorage.removeItem("persist:auth");
    dispatch(logoutAction());
  }
}, []); // Run once on startup
```

### Authorization (Permissions)

**Role Hierarchy**:

```
SystemOwner (highest)
    ↓
Admin
    ↓
Cashier (lowest)
```

**Permission System**:

```typescript
// Backend defines permissions as enum
enum Permission {
  ProductsView = 100,
  ProductsManage = 101,
  OrdersView = 200,
  OrdersCreate = 202,
  ReportsView = 300,
  // ... 50+ permissions
}

// Frontend checks permissions
const { hasPermission } = usePermission();

if (hasPermission("ProductsManage")) {
  // Show edit/delete buttons
}
```

### Route Protection

```typescript
// App.tsx - Route guards
const ProtectedRoute = ({ children }: { children: React.ReactNode }) => {
  const isAuthenticated = useAppSelector(selectIsAuthenticated);
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  return <>{children}</>;
};

const AdminRoute = ({ children }: { children: React.ReactNode }) => {
  const isAdmin = useAppSelector(selectIsAdmin);
  if (!isAdmin) return <Navigate to="/pos" replace />;
  return <>{children}</>;
};

const PermissionRoute = ({
  children,
  permission
}: {
  children: React.ReactNode;
  permission: string;
}) => {
  const { hasPermission } = usePermission();
  if (!hasPermission(permission)) return <Navigate to="/pos" replace />;
  return <>{children}</>;
};

// Usage
<Route path="/products" element={
  <ProtectedRoute>
    <PermissionRoute permission="ProductsView">
      <ProductsPage />
    </PermissionRoute>
  </ProtectedRoute>
} />
```

### Component-Level Authorization

```typescript
// Hide UI elements based on permissions
const ProductList = () => {
  const { hasPermission } = usePermission();

  return (
    <div>
      {products.map(product => (
        <div key={product.id}>
          <span>{product.name}</span>

          {hasPermission('ProductsManage') && (
            <>
              <Button onClick={() => handleEdit(product)}>تعديل</Button>
              <Button onClick={() => handleDelete(product)}>حذف</Button>
            </>
          )}
        </div>
      ))}
    </div>
  );
};
```

### Auto-Logout Scenarios

1. **Token Expired**: Automatic logout when JWT expires
2. **401 Response**: Backend rejects token (e.g., SecurityStamp changed)
3. **Inactivity**: Optional auto-logout after X minutes of inactivity
4. **Manual Logout**: User clicks logout button

```typescript
// hooks/useInactivityMonitor.ts
export const useInactivityMonitor = (timeoutMinutes: number) => {
  const { logout } = useAuth();

  useEffect(() => {
    let timeout: NodeJS.Timeout;

    const resetTimer = () => {
      clearTimeout(timeout);
      timeout = setTimeout(
        () => {
          logout();
          toast.info("تم تسجيل الخروج تلقائياً بسبب عدم النشاط");
        },
        timeoutMinutes * 60 * 1000,
      );
    };

    // Reset timer on user activity
    window.addEventListener("mousemove", resetTimer);
    window.addEventListener("keypress", resetTimer);

    resetTimer();

    return () => {
      clearTimeout(timeout);
      window.removeEventListener("mousemove", resetTimer);
      window.removeEventListener("keypress", resetTimer);
    };
  }, [timeoutMinutes, logout]);
};
```

---

## 📝 Forms & Validation

### Form Libraries

We use **React Hook Form** for form state management (NOT Formik).

**Why React Hook Form?**

- Better performance (uncontrolled inputs)
- Less re-renders
- Built-in validation
- TypeScript support

### Basic Form Pattern

```typescript
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

// 1. Define validation schema with Zod
const productSchema = z.object({
  name: z.string().min(1, 'الاسم مطلوب'),
  price: z.number().min(0, 'السعر يجب أن يكون أكبر من أو يساوي صفر'),
  categoryId: z.number().min(1, 'الفئة مطلوبة'),
  barcode: z.string().optional(),
});

type ProductFormData = z.infer<typeof productSchema>;

// 2. Component with form
const ProductForm = () => {
  const [createProduct, { isLoading }] = useCreateProductMutation();

  const {
    register,
    handleSubmit,
    formState: { errors },
    reset,
  } = useForm<ProductFormData>({
    resolver: zodResolver(productSchema),
  });

  const onSubmit = async (data: ProductFormData) => {
    try {
      await createProduct(data).unwrap();
      toast.success('تم إنشاء المنتج');
      reset();
    } catch (error) {
      toast.error('فشل إنشاء المنتج');
    }
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)}>
      <div>
        <label>الاسم</label>
        <input {...register('name')} />
        {errors.name && <span>{errors.name.message}</span>}
      </div>

      <div>
        <label>السعر</label>
        <input type="number" {...register('price', { valueAsNumber: true })} />
        {errors.price && <span>{errors.price.message}</span>}
      </div>

      <button type="submit" disabled={isLoading}>
        {isLoading ? 'جاري الحفظ...' : 'حفظ'}
      </button>
    </form>
  );
};
```

### Validation Strategy

**Two-Layer Validation**:

1. **Frontend Validation** (Zod): Immediate feedback, better UX
2. **Backend Validation** (Service Layer): Security, business rules

```typescript
// Frontend validation (Zod)
const schema = z.object({
  quantity: z.number().min(1, "الكمية يجب أن تكون أكبر من صفر"),
});

// Backend validation (returns errorCode)
// If backend returns errorCode: 'INSUFFICIENT_STOCK'
// Frontend shows specific error message
```

### Handling Backend Validation Errors

```typescript
const onSubmit = async (data: ProductFormData) => {
  try {
    await createProduct(data).unwrap();
  } catch (error) {
    const apiError = error as { data: ApiResponse<null> };

    // Map backend errorCode to form field
    if (apiError.data?.errorCode === "PRODUCT_NAME_DUPLICATE") {
      setError("name", {
        type: "manual",
        message: "اسم المنتج موجود بالفعل",
      });
    } else {
      toast.error(apiError.data?.message || "حدث خطأ");
    }
  }
};
```

---

## ⚠️ Error Handling

### Error Handling Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                  ERROR HANDLING LAYERS                      │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. Global Error Boundary (React)                           │
│     - Catches React component errors                        │
│     - Shows fallback UI                                     │
│                                                             │
│  2. Global API Error Handler (baseApi.ts)                   │
│     - Catches all API errors                                │
│     - Shows toast notifications                             │
│     - Handles 401/403/500 automatically                     │
│                                                             │
│  3. Component-Level Error Handling                          │
│     - Specific error logic per feature                      │
│     - Maps errorCode to UI behavior                         │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 1. Global Error Boundary

```typescript
// components/ErrorBoundary.tsx
export class ErrorBoundary extends React.Component<Props, State> {
  state = { hasError: false, error: null };

  static getDerivedStateFromError(error: Error) {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: React.ErrorInfo) {
    console.error('React Error:', error, errorInfo);
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="error-screen">
          <h1>حدث خطأ غير متوقع</h1>
          <button onClick={() => window.location.reload()}>
            إعادة تحميل الصفحة
          </button>
        </div>
      );
    }

    return this.props.children;
  }
}

// Usage in main.tsx
<ErrorBoundary>
  <App />
</ErrorBoundary>
```

### 2. Global API Error Handler

**Automatic handling in `baseApi.ts`**:

```typescript
// 401 Unauthorized → Auto logout
if (error.status === 401) {
  localStorage.removeItem("persist:auth");
  api.dispatch({ type: "auth/logout" });
  window.location.href = "/login";
  toast.error("انتهت جلستك، يرجى تسجيل الدخول مرة أخرى");
}

// 403 Forbidden → Show error
if (error.status === 403) {
  toast.error("ليس لديك صلاحية للقيام بهذا الإجراء");
}

// 500 Server Error → Retry (GET only)
if (error.status === 500) {
  toast.error("حدث خطأ في الخادم، حاول مرة أخرى");
}

// Network Error → Retry (GET only)
if (error.status === "FETCH_ERROR") {
  toast.error("لا يوجد اتصال بالإنترنت");
}
```

### 3. Component-Level Error Handling

**Pattern**: Map `errorCode` to specific UI behavior

```typescript
const handleCreateOrder = async () => {
  try {
    await createOrder(orderData).unwrap();
    toast.success("تم إنشاء الطلب");
  } catch (error) {
    const apiError = error as { data: ApiResponse<null> };

    // Use errorCode for logic (NOT message)
    switch (apiError.data?.errorCode) {
      case "NO_OPEN_SHIFT":
        toast.error("يجب فتح وردية أولاً");
        navigate("/shift");
        break;

      case "INSUFFICIENT_STOCK":
        toast.error("المخزون غير كافٍ لإتمام الطلب");
        // Refetch products to get updated stock
        refetchProducts();
        break;

      case "CUSTOMER_CREDIT_LIMIT_EXCEEDED":
        toast.error("تجاوز العميل حد الائتمان المسموح");
        setShowCreditWarning(true);
        break;

      default:
        // Fallback to message
        toast.error(apiError.data?.message || "حدث خطأ");
    }
  }
};
```

### Error Code Mapping

**Common Error Codes** (from backend):

| Error Code                       | Meaning                        | Frontend Action                      |
| -------------------------------- | ------------------------------ | ------------------------------------ |
| `NO_OPEN_SHIFT`                  | No shift is open               | Redirect to shift page               |
| `INSUFFICIENT_STOCK`             | Not enough inventory           | Show stock warning, refetch products |
| `PRODUCT_NOT_FOUND`              | Product doesn't exist          | Show error, remove from cart         |
| `ORDER_NOT_FOUND`                | Order doesn't exist            | Show error, redirect to orders       |
| `PERMISSION_DENIED`              | User lacks permission          | Show error, hide action              |
| `TOKEN_EXPIRED`                  | JWT expired                    | Auto logout (handled globally)       |
| `CUSTOMER_CREDIT_LIMIT_EXCEEDED` | Credit limit exceeded          | Show credit warning modal            |
| `SHIFT_CONCURRENCY_CONFLICT`     | Shift modified by another user | Refetch shift, show warning          |

### Error Message Localization

```typescript
// utils/constants.ts
export const ERROR_MESSAGES: Record<string, string> = {
  // Auth
  TOKEN_EXPIRED: "انتهت جلستك، يرجى تسجيل الدخول مرة أخرى",
  TOKEN_INVALID: "جلسة غير صالحة",
  PERMISSION_DENIED: "ليس لديك صلاحية للقيام بهذا الإجراء",

  // Orders
  NO_OPEN_SHIFT: "يجب فتح وردية قبل إنشاء طلب",
  ORDER_NOT_FOUND: "الطلب غير موجود",
  ORDER_EMPTY: "الطلب فارغ",

  // Products
  PRODUCT_NOT_FOUND: "المنتج غير موجود",
  INSUFFICIENT_STOCK: "المخزون غير كافٍ",
  PRODUCT_INACTIVE: "المنتج غير نشط",

  // Customers
  CUSTOMER_NOT_FOUND: "العميل غير موجود",
  CUSTOMER_CREDIT_LIMIT_EXCEEDED: "تجاوز العميل حد الائتمان المسموح",

  // Generic
  VALIDATION_ERROR: "خطأ في البيانات المدخلة",
  DATABASE_ERROR: "خطأ في قاعدة البيانات",
};

// Usage
const message = ERROR_MESSAGES[errorCode] || errorCode;
toast.error(message);
```

### Loading & Error States

```typescript
const ProductsPage = () => {
  const { data, isLoading, error, refetch } = useGetProductsQuery();

  // Loading state
  if (isLoading) {
    return (
      <div className="flex justify-center items-center h-screen">
        <LoadingSpinner />
      </div>
    );
  }

  // Error state
  if (error) {
    return (
      <div className="error-container">
        <p>حدث خطأ أثناء تحميل المنتجات</p>
        <button onClick={refetch}>إعادة المحاولة</button>
      </div>
    );
  }

  // Success state
  return <ProductList products={data?.data} />;
};
```

---

## 🎨 UI & Styling System

### TailwindCSS Configuration

**File**: `tailwind.config.js`

```javascript
module.exports = {
  content: ["./index.html", "./src/**/*.{js,ts,jsx,tsx}"],
  theme: {
    extend: {
      colors: {
        primary: {
          50: "#eff6ff",
          100: "#dbeafe",
          // ... full color scale
          600: "#2563eb",
          700: "#1d4ed8",
        },
        danger: {
          /* ... */
        },
        success: {
          /* ... */
        },
        warning: {
          /* ... */
        },
      },
      fontFamily: {
        sans: ["Cairo", "sans-serif"], // Arabic font
      },
    },
  },
  plugins: [],
};
```

### RTL (Right-to-Left) Support

**Global RTL Configuration**:

```css
/* index.css */
body {
  direction: rtl;
  text-align: right;
  font-family: "Cairo", sans-serif;
}
```

**Tailwind RTL Classes**:

```tsx
// Use logical properties (start/end instead of left/right)
<div className="mr-4">  {/* ❌ Wrong - always right margin */}
<div className="ms-4">  {/* ✅ Correct - margin-inline-start (right in RTL) */}

<div className="text-left">   {/* ❌ Wrong */}
<div className="text-start">  {/* ✅ Correct */}
```

### Component Styling Patterns

#### 1. Utility-First Approach

```tsx
// ✅ Preferred - Inline Tailwind classes
<button className="px-4 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700">
  حفظ
</button>
```

#### 2. Component Variants with clsx

```tsx
import { clsx } from "clsx";

interface ButtonProps {
  variant?: "primary" | "secondary" | "danger";
  size?: "sm" | "md" | "lg";
}

const Button = ({
  variant = "primary",
  size = "md",
  children,
}: ButtonProps) => {
  return (
    <button
      className={clsx(
        "rounded-lg font-medium transition-colors",
        // Variant styles
        variant === "primary" &&
          "bg-primary-600 text-white hover:bg-primary-700",
        variant === "secondary" &&
          "bg-gray-200 text-gray-800 hover:bg-gray-300",
        variant === "danger" && "bg-danger-600 text-white hover:bg-danger-700",
        // Size styles
        size === "sm" && "px-3 py-1.5 text-sm",
        size === "md" && "px-4 py-2 text-base",
        size === "lg" && "px-6 py-3 text-lg",
      )}
    >
      {children}
    </button>
  );
};
```

### Design System Tokens

**Colors**:

- `primary-*`: Main brand color (blue)
- `danger-*`: Destructive actions (red)
- `success-*`: Success states (green)
- `warning-*`: Warning states (yellow)
- `gray-*`: Neutral colors

**Spacing Scale**:

- `p-1` = 0.25rem (4px)
- `p-2` = 0.5rem (8px)
- `p-4` = 1rem (16px)
- `p-6` = 1.5rem (24px)
- `p-8` = 2rem (32px)

**Typography**:

- `text-xs` = 0.75rem (12px)
- `text-sm` = 0.875rem (14px)
- `text-base` = 1rem (16px)
- `text-lg` = 1.125rem (18px)
- `text-xl` = 1.25rem (20px)

### Custom Animations

**Available Animations**:

- `animate-shake` - Shake effect for errors
- `animate-scale-in` - Scale in effect for modals
- `animate-slide-in-right` - Slide in from right (mobile sidebar)

**Usage**:

```tsx
<div className="animate-shake">Error message</div>
<div className="animate-scale-in">Modal content</div>
<aside className="animate-slide-in-right">Sidebar</aside>
```

**Configuration** (in `tailwind.config.js`):

```javascript
animation: {
  shake: "shake 0.5s ease-in-out",
  "scale-in": "scaleIn 0.2s ease-out",
  "slide-in-right": "slideInRight 0.3s ease-out",
},
keyframes: {
  shake: {
    "0%, 100%": { transform: "translateX(0)" },
    "25%": { transform: "translateX(-4px)" },
    "75%": { transform: "translateX(4px)" },
  },
  scaleIn: {
    "0%": { transform: "scale(0.95)", opacity: "0" },
    "100%": { transform: "scale(1)", opacity: "1" },
  },
  slideInRight: {
    "0%": { transform: "translateX(100%)" },
    "100%": { transform: "translateX(0)" },
  },
}
```

### Responsive Design

```tsx
// Mobile-first approach
<div
  className="
  w-full           /* Mobile: full width */
  md:w-1/2         /* Tablet: half width */
  lg:w-1/3         /* Desktop: third width */
  p-4              /* Mobile: 1rem padding */
  md:p-6           /* Tablet: 1.5rem padding */
"
>
  Content
</div>
```

### Toast Notifications

**Primary Library**: Sonner (preferred for new code)

**Note**: Some legacy code still uses `react-hot-toast`. When adding new features, use Sonner:

```typescript
// ✅ Preferred - Sonner
import { toast } from "sonner";

// Success
toast.success("تم الحفظ بنجاح");

// Error
toast.error("حدث خطأ");

// Warning
toast.warning("تحذير");

// Info
toast.info("معلومة");

// Custom duration
toast.success("رسالة", { duration: 5000 });

// ⚠️ Legacy - react-hot-toast (being phased out)
// Some existing components still use this - avoid for new code
import { toast } from "react-hot-toast";
```

**Configuration** (in `main.tsx`):

```tsx
<Toaster
  position="top-center"
  dir="rtl"
  richColors
  toastOptions={{
    duration: 3000,
    style: {
      fontFamily: "Cairo, sans-serif",
      direction: "rtl",
      textAlign: "right",
    },
  }}
/>
```

---

## 🚀 How to Add New Features

### Step-by-Step Guide: Adding a "Discounts" Feature

This is a complete, practical guide for adding a new feature from scratch.

---

### Step 1: Define Types (Match Backend DTOs)

**File**: `src/types/discount.types.ts`

```typescript
// ⚠️ CRITICAL: Types MUST match backend DTOs exactly

export interface Discount {
  id: number;
  name: string;
  type: "Percentage" | "Fixed";
  value: number;
  startDate: string;
  endDate: string;
  isActive: boolean;
  createdAt: string;
}

export interface CreateDiscountDto {
  name: string;
  type: "Percentage" | "Fixed";
  value: number;
  startDate: string;
  endDate: string;
  isActive?: boolean;
}

export interface UpdateDiscountDto {
  name?: string;
  type?: "Percentage" | "Fixed";
  value?: number;
  startDate?: string;
  endDate?: string;
  isActive?: boolean;
}
```

**Rules**:

- ✅ Use exact same property names as backend
- ✅ Use exact same types (string, number, boolean)
- ✅ Use ISO 8601 strings for dates (not Date objects)
- ✅ Optional properties marked with `?`

---

### Step 2: Create API Slice

**File**: `src/api/discountsApi.ts`

```typescript
import { baseApi } from "./baseApi";
import {
  Discount,
  CreateDiscountDto,
  UpdateDiscountDto,
} from "../types/discount.types";
import { ApiResponse } from "../types/api.types";

export const discountsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    // GET /api/discounts
    getDiscounts: builder.query<ApiResponse<Discount[]>, void>({
      query: () => "/discounts",
      providesTags: ["Discounts"], // ← Add to baseApi tagTypes
    }),

    // GET /api/discounts/{id}
    getDiscount: builder.query<ApiResponse<Discount>, number>({
      query: (id) => `/discounts/${id}`,
      providesTags: (result, error, id) => [{ type: "Discounts", id }],
    }),

    // POST /api/discounts
    createDiscount: builder.mutation<ApiResponse<number>, CreateDiscountDto>({
      query: (dto) => ({
        url: "/discounts",
        method: "POST",
        body: dto,
      }),
      invalidatesTags: ["Discounts"], // ← Refetch all discounts
    }),

    // PUT /api/discounts/{id}
    updateDiscount: builder.mutation<
      ApiResponse<boolean>,
      { id: number; dto: UpdateDiscountDto }
    >({
      query: ({ id, dto }) => ({
        url: `/discounts/${id}`,
        method: "PUT",
        body: dto,
      }),
      invalidatesTags: (result, error, { id }) => [
        "Discounts",
        { type: "Discounts", id },
      ],
    }),

    // DELETE /api/discounts/{id}
    deleteDiscount: builder.mutation<ApiResponse<boolean>, number>({
      query: (id) => ({
        url: `/discounts/${id}`,
        method: "DELETE",
      }),
      invalidatesTags: ["Discounts"],
    }),
  }),
});

// Export hooks
export const {
  useGetDiscountsQuery,
  useGetDiscountQuery,
  useCreateDiscountMutation,
  useUpdateDiscountMutation,
  useDeleteDiscountMutation,
} = discountsApi;
```

**Don't forget**: Add `'Discounts'` to `tagTypes` in `baseApi.ts`:

```typescript
// baseApi.ts
tagTypes: [
  "Products",
  "Orders",
  "Discounts", // ← Add this
  // ...
];
```

---

### Step 3: Create Components

#### 3a. List Component

**File**: `src/components/discounts/DiscountList.tsx`

```typescript
import { Discount } from '../../types/discount.types';
import { usePermission } from '../../hooks/usePermission';

interface DiscountListProps {
  discounts: Discount[];
  onEdit: (discount: Discount) => void;
  onDelete: (id: number) => void;
}

export const DiscountList = ({ discounts, onEdit, onDelete }: DiscountListProps) => {
  const { hasPermission } = usePermission();

  return (
    <div className="space-y-4">
      {discounts.map((discount) => (
        <div key={discount.id} className="bg-white p-4 rounded-lg shadow">
          <div className="flex justify-between items-center">
            <div>
              <h3 className="text-lg font-semibold">{discount.name}</h3>
              <p className="text-gray-600">
                {discount.type === 'Percentage'
                  ? `${discount.value}%`
                  : `${discount.value} جنيه`}
              </p>
            </div>

            {hasPermission('DiscountsManage') && (
              <div className="flex gap-2">
                <button
                  onClick={() => onEdit(discount)}
                  className="px-3 py-1 bg-primary-600 text-white rounded"
                >
                  تعديل
                </button>
                <button
                  onClick={() => onDelete(discount.id)}
                  className="px-3 py-1 bg-danger-600 text-white rounded"
                >
                  حذف
                </button>
              </div>
            )}
          </div>
        </div>
      ))}
    </div>
  );
};
```

#### 3b. Form Component

**File**: `src/components/discounts/DiscountForm.tsx`

```typescript
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { CreateDiscountDto } from '@/types/discount.types';
import { Input } from '@/components/common/Input';
import { Button } from '@/components/common/Button';

const discountSchema = z.object({
  name: z.string().min(1, 'الاسم مطلوب'),
  type: z.enum(['Percentage', 'Fixed']),
  value: z.number().min(0, 'القيمة يجب أن تكون أكبر من أو تساوي صفر'),
  startDate: z.string().min(1, 'تاريخ البداية مطلوب'),
  endDate: z.string().min(1, 'تاريخ النهاية مطلوب'),
  isActive: z.boolean().optional(),
});

interface DiscountFormProps {
  onSubmit: (data: CreateDiscountDto) => Promise<void>;
  initialData?: Partial<CreateDiscountDto>;
  isLoading?: boolean;
}

export const DiscountForm = ({ onSubmit, initialData, isLoading }: DiscountFormProps) => {
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<CreateDiscountDto>({
    resolver: zodResolver(discountSchema),
    defaultValues: initialData,
  });

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <div>
        <label className="block text-sm font-medium mb-1">الاسم</label>
        <Input {...register('name')} />
        {errors.name && (
          <span className="text-danger-600 text-sm">{errors.name.message}</span>
        )}
      </div>

      <div>
        <label className="block text-sm font-medium mb-1">النوع</label>
        <select {...register('type')} className="w-full px-3 py-2 border rounded-lg">
          <option value="Percentage">نسبة مئوية</option>
          <option value="Fixed">مبلغ ثابت</option>
        </select>
      </div>

      <div>
        <label className="block text-sm font-medium mb-1">القيمة</label>
        <Input
          type="number"
          step="0.01"
          {...register('value', { valueAsNumber: true })}
        />
        {errors.value && (
          <span className="text-danger-600 text-sm">{errors.value.message}</span>
        )}
      </div>

      <div>
        <label className="block text-sm font-medium mb-1">تاريخ البداية</label>
        <Input type="date" {...register('startDate')} />
      </div>

      <div>
        <label className="block text-sm font-medium mb-1">تاريخ النهاية</label>
        <Input type="date" {...register('endDate')} />
      </div>

      <Button
        type="submit"
        variant="primary"
        isLoading={isLoading}
        className="w-full"
      >
        حفظ
      </Button>
    </form>
  );
};
```

---

### Step 4: Create Page

**File**: `src/pages/discounts/DiscountsPage.tsx`

```typescript
import { useState } from 'react';
import { toast } from 'sonner';
import {
  useGetDiscountsQuery,
  useCreateDiscountMutation,
  useUpdateDiscountMutation,
  useDeleteDiscountMutation,
} from '../../api/discountsApi';
import { DiscountList } from '../../components/discounts/DiscountList';
import { DiscountForm } from '../../components/discounts/DiscountForm';
import { Discount, CreateDiscountDto } from '../../types/discount.types';

const DiscountsPage = () => {
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingDiscount, setEditingDiscount] = useState<Discount | null>(null);

  // Queries
  const { data, isLoading, error } = useGetDiscountsQuery();

  // Mutations
  const [createDiscount, { isLoading: isCreating }] = useCreateDiscountMutation();
  const [updateDiscount, { isLoading: isUpdating }] = useUpdateDiscountMutation();
  const [deleteDiscount] = useDeleteDiscountMutation();

  // Handlers
  const handleCreate = async (dto: CreateDiscountDto) => {
    try {
      await createDiscount(dto).unwrap();
      toast.success('تم إنشاء الخصم بنجاح');
      setIsModalOpen(false);
    } catch (error) {
      toast.error('فشل إنشاء الخصم');
    }
  };

  const handleUpdate = async (dto: CreateDiscountDto) => {
    if (!editingDiscount) return;

    try {
      await updateDiscount({ id: editingDiscount.id, dto }).unwrap();
      toast.success('تم تحديث الخصم بنجاح');
      setIsModalOpen(false);
      setEditingDiscount(null);
    } catch (error) {
      toast.error('فشل تحديث الخصم');
    }
  };

  const handleDelete = async (id: number) => {
    if (!confirm('هل أنت متأكد من حذف هذا الخصم؟')) return;

    try {
      await deleteDiscount(id).unwrap();
      toast.success('تم حذف الخصم بنجاح');
    } catch (error) {
      toast.error('فشل حذف الخصم');
    }
  };

  const handleEdit = (discount: Discount) => {
    setEditingDiscount(discount);
    setIsModalOpen(true);
  };

  // Render
  if (isLoading) return <div>جاري التحميل...</div>;
  if (error) return <div>حدث خطأ</div>;

  return (
    <div className="p-6">
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-2xl font-bold">الخصومات</h1>
        <button
          onClick={() => setIsModalOpen(true)}
          className="px-4 py-2 bg-primary-600 text-white rounded-lg"
        >
          إضافة خصم
        </button>
      </div>

      <DiscountList
        discounts={data?.data || []}
        onEdit={handleEdit}
        onDelete={handleDelete}
      />

      {isModalOpen && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center">
          <div className="bg-white p-6 rounded-lg w-full max-w-md">
            <h2 className="text-xl font-bold mb-4">
              {editingDiscount ? 'تعديل خصم' : 'إضافة خصم'}
            </h2>
            <DiscountForm
              onSubmit={editingDiscount ? handleUpdate : handleCreate}
              initialData={editingDiscount || undefined}
              isLoading={isCreating || isUpdating}
            />
            <button
              onClick={() => {
                setIsModalOpen(false);
                setEditingDiscount(null);
              }}
              className="mt-4 w-full px-4 py-2 bg-gray-200 rounded-lg"
            >
              إلغاء
            </button>
          </div>
        </div>
      )}
    </div>
  );
};

export default DiscountsPage;
```

---

### Step 5: Add Route

**File**: `src/App.tsx`

```typescript
import DiscountsPage from './pages/discounts/DiscountsPage';

// Inside <Routes>
<Route
  path="/discounts"
  element={
    <ProtectedRoute>
      <PermissionRoute permission="DiscountsView">
        <DiscountsPage />
      </PermissionRoute>
    </ProtectedRoute>
  }
/>
```

---

### Step 6: Add Navigation Link

**File**: `src/components/layout/Sidebar.tsx`

```typescript
<Link
  to="/discounts"
  className="flex items-center gap-3 px-4 py-2 hover:bg-gray-100 rounded-lg"
>
  <TagIcon className="w-5 h-5" />
  <span>الخصومات</span>
</Link>
```

---

### Step 7: Test

```bash
# 1. Type check
npm run build

# 2. Manual testing
npm run dev
# Navigate to /discounts
# Test create, edit, delete

# 3. Check network tab
# Verify API calls match backend endpoints
```

---

### Checklist for New Features

```
Types:
- [ ] Created types file matching backend DTOs
- [ ] Exported all interfaces

API:
- [ ] Created API slice with injectEndpoints
- [ ] Added queries (GET)
- [ ] Added mutations (POST/PUT/DELETE)
- [ ] Added providesTags and invalidatesTags
- [ ] Added tag to baseApi tagTypes
- [ ] Exported hooks

Components:
- [ ] Created list component
- [ ] Created form component
- [ ] Added permission checks
- [ ] Added loading states
- [ ] Added error handling

Page:
- [ ] Created page component
- [ ] Used RTK Query hooks
- [ ] Handled success/error cases
- [ ] Added toast notifications

Routing:
- [ ] Added route in App.tsx
- [ ] Added route protection (ProtectedRoute, PermissionRoute)
- [ ] Added navigation link in Sidebar

Testing:
- [ ] Type check passes (npm run build)
- [ ] Manual testing completed
- [ ] Network requests verified
```

---

## ⚡ Performance Considerations

### 1. RTK Query Caching

**Automatic Optimization**:

- Requests are deduplicated (multiple components requesting same data = 1 API call)
- Cache is shared across components
- Stale data is refetched automatically

**Configuration**:

```typescript
// baseApi.ts
keepUnusedDataFor: 60,       // Cache for 60 seconds after last use
refetchOnFocus: true,        // Refetch when window regains focus
refetchOnReconnect: true,    // Refetch when network reconnects
```

**Manual Cache Control**:

```typescript
// Force refetch
const { refetch } = useGetProductsQuery();
refetch();

// Skip query conditionally
const { data } = useGetProductsQuery(undefined, {
  skip: !isAuthenticated, // Don't fetch if not logged in
});

// Polling (auto-refetch)
const { data } = useGetOrdersQuery(undefined, {
  pollingInterval: 30000, // Refetch every 30 seconds
});
```

### 2. Component Optimization

#### React.memo for Expensive Components

```typescript
import { memo } from 'react';

// Prevent re-render if props haven't changed
export const ProductCard = memo(({ product }: { product: Product }) => {
  return (
    <div className="product-card">
      <h3>{product.name}</h3>
      <p>{product.price}</p>
    </div>
  );
});
```

#### useMemo for Expensive Calculations

```typescript
import { useMemo } from 'react';

const OrderSummary = ({ items }: { items: CartItem[] }) => {
  // Only recalculate when items change
  const total = useMemo(() => {
    return items.reduce((sum, item) => {
      return sum + (item.product.price * item.quantity);
    }, 0);
  }, [items]);

  return <div>الإجمالي: {total}</div>;
};
```

#### useCallback for Event Handlers

```typescript
import { useCallback } from 'react';

const ProductList = ({ products }: { products: Product[] }) => {
  // Prevent function recreation on every render
  const handleDelete = useCallback((id: number) => {
    deleteProduct(id);
  }, [deleteProduct]);

  return (
    <div>
      {products.map(product => (
        <ProductCard
          key={product.id}
          product={product}
          onDelete={handleDelete}
        />
      ))}
    </div>
  );
};
```

### 3. Code Splitting & Lazy Loading

**Route-based Code Splitting**:

```typescript
import { lazy, Suspense } from 'react';

// Lazy load page components
const ProductsPage = lazy(() => import('./pages/products/ProductsPage'));
const OrdersPage = lazy(() => import('./pages/orders/OrdersPage'));

// Wrap with Suspense
<Route
  path="/products"
  element={
    <Suspense fallback={<LoadingSpinner />}>
      <ProductsPage />
    </Suspense>
  }
/>
```

### 4. List Virtualization

For large lists (1000+ items), use virtualization:

```typescript
import { FixedSizeList } from 'react-window';

const ProductList = ({ products }: { products: Product[] }) => {
  const Row = ({ index, style }: { index: number; style: React.CSSProperties }) => (
    <div style={style}>
      <ProductCard product={products[index]} />
    </div>
  );

  return (
    <FixedSizeList
      height={600}
      itemCount={products.length}
      itemSize={100}
      width="100%"
    >
      {Row}
    </FixedSizeList>
  );
};
```

### 5. Image Optimization

```tsx
// Use appropriate image formats
<img
  src="/products/image.webp" // WebP for modern browsers
  alt="Product"
  loading="lazy" // Lazy load images
  width={200}
  height={200}
/>
```

### 6. Bundle Size Optimization

**Vite Configuration** (`vite.config.ts`):

```typescript
export default defineConfig({
  build: {
    rollupOptions: {
      output: {
        manualChunks: {
          // Split vendor code
          vendor: ["react", "react-dom", "react-router-dom"],
          redux: ["@reduxjs/toolkit", "react-redux", "redux-persist"],
          ui: ["@headlessui/react", "@heroicons/react", "lucide-react"],
        },
      },
    },
    minify: "terser",
    terserOptions: {
      compress: {
        drop_console: mode === "production", // Remove console.log in production only
        drop_debugger: true,
      },
    },
  },
});
```

### 7. Redux Performance

**Selector Optimization**:

```typescript
import { createSelector } from "@reduxjs/toolkit";

// ❌ Bad - Recalculates on every state change
const selectTotal = (state: RootState) => {
  return state.cart.items.reduce((sum, item) => sum + item.price, 0);
};

// ✅ Good - Memoized, only recalculates when items change
const selectTotal = createSelector(
  [(state: RootState) => state.cart.items],
  (items) => items.reduce((sum, item) => sum + item.price, 0),
);
```

### 8. Network Performance

**Request Batching**:

```typescript
// ❌ Bad - Multiple sequential requests
const product1 = await getProduct(1);
const product2 = await getProduct(2);
const product3 = await getProduct(3);

// ✅ Good - Parallel requests
const [product1, product2, product3] = await Promise.all([
  getProduct(1),
  getProduct(2),
  getProduct(3),
]);
```

**Prefetching**:

```typescript
// Prefetch data before user navigates
const handleMouseEnter = () => {
  dispatch(productsApi.util.prefetch('getProducts', undefined, { force: false }));
};

<Link to="/products" onMouseEnter={handleMouseEnter}>
  المنتجات
</Link>
```

---

## 🧪 Testing

### E2E Testing with Playwright

**Location**: `frontend/e2e/`

**Run Tests**:

```bash
# Run all tests
npm run test:e2e

# Run with UI
npm run test:e2e:ui

# Run in headed mode (see browser)
npm run test:e2e:headed
```

### Test Structure

```typescript
// e2e/complete-flow.spec.ts
import { test, expect } from "@playwright/test";

test.describe("Complete POS Flow", () => {
  test.beforeEach(async ({ page }) => {
    // Login
    await page.goto("http://localhost:3000/login");
    await page.fill('input[name="email"]', "admin@kasserpro.com");
    await page.fill('input[name="password"]', "Admin@123");
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/.*pos/);
  });

  test("should create order and complete payment", async ({ page }) => {
    // 1. Open shift
    await page.goto("http://localhost:3000/shift");
    await page.click('button:has-text("فتح وردية")');
    await page.fill('input[name="openingBalance"]', "1000");
    await page.click('button:has-text("تأكيد")');

    // 2. Navigate to POS
    await page.goto("http://localhost:3000/pos");

    // 3. Add product to cart
    await page.click(".product-card:first-child");
    await expect(page.locator(".cart-item")).toHaveCount(1);

    // 4. Complete order
    await page.click('button:has-text("إتمام الطلب")');
    await page.click('button:has-text("نقدي")');
    await page.fill('input[name="paidAmount"]', "100");
    await page.click('button:has-text("تأكيد الدفع")');

    // 5. Verify success
    await expect(page.locator("text=تم إتمام الطلب")).toBeVisible();
  });
});
```

### What to Test

**Critical Flows**:

1. ✅ Login/Logout
2. ✅ Open/Close Shift
3. ✅ Create Order → Add Items → Complete Payment
4. ✅ Product CRUD
5. ✅ Customer CRUD
6. ✅ Reports Generation
7. ✅ Permission-based Access

**Don't Test**:

- ❌ Unit tests for every component (overkill for this project)
- ❌ API mocking (test against real backend)
- ❌ Styling/visual regression (manual QA)

---

## ⚠️ Common Pitfalls & Rules

### ❌ NEVER Do This

#### 1. Using `any` Type

```typescript
// ❌ WRONG
const data: any = response;
data.whatever; // No type safety

// ✅ CORRECT
const data: ApiResponse<Product[]> = response;
data.data; // Type-safe
```

#### 2. Direct API Calls (Bypassing RTK Query)

```typescript
// ❌ WRONG
const response = await fetch("/api/products");
const data = await response.json();

// ✅ CORRECT
const { data } = useGetProductsQuery();
```

#### 3. Mutating Redux State Directly

```typescript
// ❌ WRONG
state.items.push(newItem); // Direct mutation

// ✅ CORRECT (Redux Toolkit uses Immer)
state.items.push(newItem); // Actually correct in RTK!
// But outside reducers:
dispatch(addItem(newItem)); // Always use actions
```

#### 4. Checking `response.data?.success` Manually

```typescript
// ❌ WRONG
const response = await createProduct(dto);
if (response.data?.success) {
  // ...
}

// ✅ CORRECT
try {
  await createProduct(dto).unwrap(); // Throws on error
  toast.success("نجح");
} catch (error) {
  toast.error("فشل");
}
```

#### 5. Using Message for Logic

```typescript
// ❌ WRONG
if (error.message === "المنتج غير موجود") {
  // Message can change!
}

// ✅ CORRECT
if (error.errorCode === "PRODUCT_NOT_FOUND") {
  // errorCode is stable
}
```

#### 6. Hardcoding Tax Rate

```typescript
// ❌ WRONG
const tax = total * 0.14;

// ✅ CORRECT
const taxRate = useAppSelector(selectTaxRate);
const tax = total * (taxRate / 100);
```

#### 7. Persisting Branch State

```typescript
// ❌ WRONG - Causes branch mismatch on user switch
const branchPersistConfig = {
  key: "branch",
  storage,
  whitelist: ["currentBranch"],
};

// ✅ CORRECT - Branch selected fresh on each login
// No persistence for branch state
```

### ✅ ALWAYS Do This

#### 1. Type Everything

```typescript
// ✅ Props
interface ProductCardProps {
  product: Product;
  onEdit: (id: number) => void;
}

// ✅ State
const [isOpen, setIsOpen] = useState<boolean>(false);

// ✅ Functions
const calculateTotal = (items: CartItem[]): number => {
  return items.reduce((sum, item) => sum + item.price, 0);
};
```

#### 2. Use ErrorCode for Logic

```typescript
// ✅ Always check errorCode first
if (error.data?.errorCode === "NO_OPEN_SHIFT") {
  navigate("/shift");
} else {
  toast.error(error.data?.message || "حدث خطأ");
}
```

#### 3. Invalidate Cache After Mutations

```typescript
// ✅ Always invalidate related tags
createProduct: builder.mutation({
  query: (dto) => ({ url: "/products", method: "POST", body: dto }),
  invalidatesTags: ["Products"], // ← Don't forget!
});
```

#### 4. Handle Loading & Error States

```typescript
// ✅ Always handle all states
const { data, isLoading, error } = useGetProductsQuery();

if (isLoading) return <LoadingSpinner />;
if (error) return <ErrorMessage />;
return <ProductList products={data?.data} />;
```

#### 5. Use Permissions

```typescript
// ✅ Always check permissions
const { hasPermission } = usePermission();

{hasPermission('ProductsManage') && (
  <button onClick={handleDelete}>حذف</button>
)}
```

---

## 🤖 AI Agent Instructions

### For AI Agents Working on This Codebase

This section provides specific guidance for AI coding assistants (like Kiro, Cursor, GitHub Copilot) to work effectively with this frontend codebase.

---

### 🎯 Before You Start

**ALWAYS read these files first**:

1. `frontend/README.md` (this file) — Frontend architecture
2. `.kiro/steering/architecture.md` — System architecture
3. `.kiro/steering/api-contract.md` — Backend API contract
4. `.kiro/skills/kasserpro-bestpractices/SKILL.md` — Best practices

**Golden Rule**: The code is the source of truth. If documentation conflicts with code, follow the code and report the mismatch.

---

### 📋 Pre-Flight Checklist

Before generating ANY code, ask yourself:

```
1. Does this feature need backend support?
   → Check api-contract.md for existing endpoints
   → If new endpoint needed, backend must be updated first

2. Do types exist for this feature?
   → Check src/types/ folder
   → Types MUST match backend DTOs exactly

3. Does an API slice exist?
   → Check src/api/ folder
   → If not, create one following the pattern

4. What permissions are needed?
   → Check usePermission hook usage
   → Add permission checks to UI

5. Is this a query or mutation?
   → Query = GET (read data)
   → Mutation = POST/PUT/DELETE (write data)

6. What cache tags are affected?
   → providesTags for queries
   → invalidatesTags for mutations

7. What error codes can this return?
   → Check api-contract.md error codes table
   → Handle specific errorCodes in component

8. Is this feature multi-tenant/multi-branch?
   → Most features are (except Auth)
   → Headers added automatically by baseQuery
```

---

### 🚫 Forbidden Patterns

**NEVER generate code with these patterns**:

```typescript
// ❌ 1. Using 'any' type
const data: any = response;

// ❌ 2. Direct fetch calls
const response = await fetch('/api/products');

// ❌ 3. Checking success manually
if (response.data?.success) { }

// ❌ 4. Using message for logic
if (error.message === 'المنتج غير موجود') { }

// ❌ 5. Hardcoded values
const taxRate = 0.14;
const branchId = 1;

// ❌ 6. Missing error handling
await createProduct(dto); // No try/catch

// ❌ 7. Missing permission checks
<button onClick={handleDelete}>حذف</button> // No permission check

// ❌ 8. Wrong folder structure
// client/ instead of frontend/
// components/Button.tsx instead of components/common/Button.tsx
```

---

### ✅ Required Patterns

**ALWAYS generate code following these patterns**:

#### 1. Type Definitions

```typescript
// ✅ Always match backend DTOs exactly
export interface Product {
  id: number; // int in C#
  name: string; // string in C#
  price: number; // decimal in C#
  isActive: boolean; // bool in C#
  createdAt: string; // DateTime in C# → ISO 8601 string
}

export interface CreateProductDto {
  name: string;
  price: number;
  categoryId: number;
  isActive?: boolean; // Optional in C# → ? in TypeScript
}
```

#### 2. API Slice

```typescript
// ✅ Always use this exact pattern
export const productsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getProducts: builder.query<ApiResponse<Product[]>, void>({
      query: () => "/products",
      providesTags: ["Products"],
    }),

    createProduct: builder.mutation<ApiResponse<number>, CreateProductDto>({
      query: (dto) => ({
        url: "/products",
        method: "POST",
        body: dto,
      }),
      invalidatesTags: ["Products"],
    }),
  }),
});

export const { useGetProductsQuery, useCreateProductMutation } = productsApi;
```

#### 3. Component with Data Fetching

```typescript
// ✅ Always handle loading, error, and success states
const ProductsPage = () => {
  const { data, isLoading, error } = useGetProductsQuery();
  const [createProduct] = useCreateProductMutation();

  if (isLoading) return <LoadingSpinner />;
  if (error) return <ErrorMessage error={error} />;

  return <ProductList products={data?.data || []} />;
};
```

#### 4. Error Handling

```typescript
// ✅ Always use errorCode for logic
const handleCreate = async (dto: CreateProductDto) => {
  try {
    await createProduct(dto).unwrap();
    toast.success("تم الإنشاء بنجاح");
  } catch (error) {
    const apiError = error as { data: ApiResponse<null> };

    // Use errorCode for logic
    if (apiError.data?.errorCode === "PRODUCT_NAME_DUPLICATE") {
      toast.error("اسم المنتج موجود بالفعل");
    } else {
      // Fallback to message
      toast.error(apiError.data?.message || "حدث خطأ");
    }
  }
};
```

#### 5. Permission Checks

```typescript
// ✅ Always check permissions before showing actions
const { hasPermission } = usePermission();

{hasPermission('ProductsManage') && (
  <button onClick={handleDelete}>حذف</button>
)}
```

#### 6. Form with Validation

```typescript
// ✅ Always use React Hook Form + Zod
const schema = z.object({
  name: z.string().min(1, "الاسم مطلوب"),
  price: z.number().min(0, "السعر يجب أن يكون أكبر من أو يساوي صفر"),
});

const {
  register,
  handleSubmit,
  formState: { errors },
} = useForm({
  resolver: zodResolver(schema),
});
```

---

### 🗂️ File Placement Rules

**Where to put new files**:

| File Type         | Location                                | Example                                     |
| ----------------- | --------------------------------------- | ------------------------------------------- |
| Types             | `src/types/{feature}.types.ts`          | `src/types/discount.types.ts`               |
| API Slice         | `src/api/{feature}Api.ts`               | `src/api/discountsApi.ts`                   |
| Page              | `src/pages/{feature}/{Feature}Page.tsx` | `src/pages/discounts/DiscountsPage.tsx`     |
| Feature Component | `src/components/{feature}/`             | `src/components/discounts/DiscountList.tsx` |
| Shared Component  | `src/components/common/`                | `src/components/common/Button.tsx`          |
| Hook              | `src/hooks/use{Feature}.ts`             | `src/hooks/useDiscounts.ts`                 |
| Utility           | `src/utils/{feature}.ts`                | `src/utils/discountCalculations.ts`         |

---

### 🔄 Workflow for Adding Features

**Step-by-step process**:

```
1. Check if backend endpoint exists
   → Read api-contract.md
   → If missing, stop and request backend implementation

2. Create types file
   → src/types/{feature}.types.ts
   → Match backend DTOs exactly

3. Create API slice
   → src/api/{feature}Api.ts
   → Add queries and mutations
   → Add tag to baseApi.ts tagTypes

4. Create components
   → List component (display data)
   → Form component (create/edit)
   → Add permission checks

5. Create page
   → src/pages/{feature}/{Feature}Page.tsx
   → Use RTK Query hooks
   → Handle loading/error states

6. Add route
   → App.tsx
   → Add ProtectedRoute + PermissionRoute

7. Add navigation
   → Sidebar.tsx or relevant menu

8. Test
   → npm run build (type check)
   → Manual testing in browser
   → Verify network requests
```

---

### 🧪 Verification Steps

**After generating code, verify**:

```bash
# 1. Type check
npm run build
# Expected: 0 errors

# 2. Check imports
# All imports should resolve
# No missing dependencies

# 3. Check API contract
# Endpoint exists in api-contract.md
# Types match backend DTOs

# 4. Check permissions
# Permission checks added to UI
# Permission exists in backend

# 5. Check error handling
# try/catch around mutations
# errorCode used for logic
# toast notifications added
```

---

### 📝 Code Generation Templates

#### Template: New Feature (Complete)

```typescript
// 1. Types (src/types/feature.types.ts)
export interface Feature {
  id: number;
  name: string;
  createdAt: string;
}

export interface CreateFeatureDto {
  name: string;
}

// 2. API (src/api/featureApi.ts)
export const featureApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getFeatures: builder.query<ApiResponse<Feature[]>, void>({
      query: () => '/features',
      providesTags: ['Features'],
    }),
    createFeature: builder.mutation<ApiResponse<number>, CreateFeatureDto>({
      query: (dto) => ({
        url: '/features',
        method: 'POST',
        body: dto,
      }),
      invalidatesTags: ['Features'],
    }),
  }),
});

export const { useGetFeaturesQuery, useCreateFeatureMutation } = featureApi;

// 3. Component (src/components/feature/FeatureList.tsx)
interface FeatureListProps {
  features: Feature[];
}

export const FeatureList = ({ features }: FeatureListProps) => {
  return (
    <div>
      {features.map(feature => (
        <div key={feature.id}>{feature.name}</div>
      ))}
    </div>
  );
};

// 4. Page (src/pages/feature/FeaturePage.tsx)
const FeaturePage = () => {
  const { data, isLoading, error } = useGetFeaturesQuery();
  const [createFeature] = useCreateFeatureMutation();

  if (isLoading) return <LoadingSpinner />;
  if (error) return <ErrorMessage />;

  return <FeatureList features={data?.data || []} />;
};

export default FeaturePage;

// 5. Route (src/App.tsx)
<Route
  path="/features"
  element={
    <ProtectedRoute>
      <PermissionRoute permission="FeaturesView">
        <FeaturePage />
      </PermissionRoute>
    </ProtectedRoute>
  }
/>
```

---

### 🚨 Critical Reminders

1. **Types MUST match backend DTOs** — No exceptions
2. **Always use RTK Query** — Never direct fetch
3. **Always use errorCode** — Never message for logic
4. **Always check permissions** — Before showing actions
5. **Always handle errors** — try/catch around mutations
6. **Always invalidate cache** — After mutations
7. **Always add loading states** — Better UX
8. **Frontend folder is `frontend/`** — Not `client/`

---

### 🔍 Debugging Checklist

**If something doesn't work**:

```
1. Check browser console
   → Any TypeScript errors?
   → Any network errors?

2. Check Network tab
   → Is request being sent?
   → What's the response status?
   → What's the response body?

3. Check Redux DevTools
   → Is state updating?
   → Are actions being dispatched?

4. Check types
   → Do frontend types match backend DTOs?
   → Run: npm run build

5. Check permissions
   → Does user have required permission?
   → Check: user.permissions array

6. Check backend logs
   → Is backend receiving request?
   → What error is backend returning?
```

---

### 💡 Pro Tips

1. **Start with types** — Get types right, everything else follows
2. **Copy existing patterns** — Don't reinvent, copy similar features
3. **Test incrementally** — Don't write everything then test
4. **Use browser DevTools** — Network tab is your friend
5. **Check api-contract.md** — Before assuming endpoint exists
6. **Ask before breaking changes** — Coordinate with backend

---

### 📚 Quick Reference

**Most Common Hooks**:

```typescript
useGetProductsQuery(); // Fetch products
useCreateProductMutation(); // Create product
useAppSelector(selectCartItems); // Get Redux state
useAppDispatch(); // Dispatch Redux actions
usePermission(); // Check permissions
useAuth(); // Auth operations
useNavigate(); // Navigation
```

**Most Common Patterns**:

```typescript
// Fetch data
const { data, isLoading, error } = useGetXQuery();

// Mutate data
const [createX, { isLoading }] = useCreateXMutation();
await createX(dto).unwrap();

// Redux state
const items = useAppSelector(selectCartItems);
dispatch(addItem(item));

// Permission check
const { hasPermission } = usePermission();
if (hasPermission("XManage")) {
}

// Error handling
try {
  await mutation(dto).unwrap();
} catch (error) {
  const apiError = error as { data: ApiResponse<null> };
  if (apiError.data?.errorCode === "X") {
  }
}
```

---

## 🧠 Advanced Operational & Architectural Addendum (Senior + AI Agents)

This addendum extends the existing guide with production-focused mental models, strict development rules, and safe modification playbooks. Use it when implementing or modifying money-impacting POS flows.

---

### 1. Application Mental Model (CRITICAL)

Think of KasserPro frontend as a **state machine around one business moment: a branch shift**.

```
Login
  ↓
Tenant context established (JWT)
  ↓
Branch selected (X-Branch-Id)
  ↓
Shift opened (cash accountability starts)
  ↓
Order lifecycle repeats (create/edit/complete/refund)
  ↓
Shift closes (financial reconciliation ends)
```

#### Core Workflow (POS Mental Loop)

1. Cashier authenticates.
2. Cashier selects branch.
3. Cashier opens shift with opening balance.
4. Cashier creates order(s), adds products, applies discounts, takes payment.
5. System updates inventory and financial records.
6. Cashier handles returns/refunds if needed.
7. Cashier closes shift and reconciles cash.

#### Critical Entities and How They Relate

```
Tenant
  └── Branch
       ├── Shift (open/close window)
       │    ├── Order (draft/completed/refunded)
       │    │    ├── OrderItem (Product + quantity + price snapshot)
       │    │    └── Payment (cash/card/fawry/...)
       │    └── Expense / Cash Movements
       └── BranchInventory (ProductId + BranchId + Quantity)
```

#### Data Movement Between Entities (Operational View)

1. `Product` is displayed from server state (RTK Query cache).
2. `OrderItem` exists first in client state (`cartSlice`) while order is in-progress.
3. On complete order mutation:
   - Cart draft becomes persisted `Order + OrderItems + Payment` in backend.
   - `BranchInventory.Quantity` is reduced per item (branch-scoped).
   - Related RTK Query tags are invalidated.
4. UI rehydrates from fresh server state (orders, shift totals, inventory snapshots).

Operational invariant: **No open shift = no valid sales completion path**.

---

### 2. Feature Deep Dive (VERY IMPORTANT) — POS Complete Order

This is the most critical click path because it touches revenue, inventory, and shift reconciliation.

#### Step-by-Step Timeline

1. **User click**
   - Cashier clicks "إتمام الطلب" in POS page.

2. **Component event handler executes**
   - Page/component reads cart totals and payment inputs.
   - Performs local sanity checks (non-empty cart, paid amount format, etc.).

3. **State read for context**
   - Reads auth token and current branch from Redux store.
   - Reads cart state from `cartSlice` selectors.

4. **API mutation call**
   - Calls RTK Query mutation (for example `useCompleteOrderMutation`).
   - Uses `.unwrap()` so success/error branches are explicit.

5. **baseApi request preparation**
   - Adds `Authorization` header.
   - Adds `X-Branch-Id` header.
   - Adds idempotency header for financial write endpoint.

6. **Backend processes transactionally**
   - Validates open shift + permissions + stock + business rules.
   - Persists order, payment, inventory movement in a single transaction.

7. **Response returns as `ApiResponse<T>`**
   - Success path: receives completed order payload.
   - Failure path: receives `errorCode` (e.g., `NO_OPEN_SHIFT`, `INSUFFICIENT_STOCK`).

8. **RTK Query cache invalidation**
   - Mutation invalidates related tags (`Orders`, `Shifts`, `Inventory`, etc.).
   - Dependent queries refetch.

9. **Redux/UI updates**
   - Cart is cleared (client state).
   - New query results flow to components.
   - POS summary, shift totals, and lists re-render.

10. **User-visible outcome**
    - Success toast + receipt path + updated totals.
    - On failure, branch-specific recovery action is shown (open shift, restock, retry manually).

---

### 3. Data Flow Diagrams (TEXT-BASED)

#### A) Generic Flow (Reference)

```
User Action
  ↓
Component
  ↓
Hook / Handler
  ↓
RTK Query Endpoint
  ↓
Backend API
  ↓
Redux Store (RTK Query cache + slices)
  ↓
UI Re-render
```

#### B) Real Example: Add Product to Cart (Client-State First)

```
Cashier clicks product card
  ↓
POS component onProductClick(product)
  ↓
dispatch(addItem({ product, quantity: 1 }))
  ↓
cartSlice state updates
  ↓
selectors recompute subtotal/tax/total
  ↓
Cart + OrderSummary components re-render
```

#### C) Real Example: Complete Order (Server-State + Invalidation)

```
Cashier clicks "Complete Order"
  ↓
handleCompleteOrder() in POS page
  ↓
completeOrderMutation(payload).unwrap()
  ↓
baseApi adds auth + branch + idempotency headers
  ↓
POST /api/orders/{id}/complete
  ↓
ApiResponse<OrderDto>
  ↓
invalidatesTags(['Orders', 'Shifts', 'Inventory'])
  ↓
related queries refetch
  ↓
UI shows committed order + fresh shift/inventory numbers
```

---

### 4. Architectural Decisions (WHY)

#### Why RTK Query?

1. Centralized API policy (headers, retry policy, auth/logout behavior).
2. Built-in cache + invalidation reduces stale UI and duplicate requests.
3. Standardized loading/error states across pages.
4. Safer mutation boundaries for financial operations (explicit, inspectable).

#### Why Feature-Based Folder Structure?

1. Keeps business context local (`components/pos`, `api/ordersApi`, `types/order.types`).
2. Reduces cross-feature coupling and makes deletion/refactor safer.
3. Enables incremental scaling (new module without global churn).

#### Why State Split (RTK Query + Redux Slices + local state)?

1. Server truth belongs to RTK Query cache (products/orders/shifts/reports).
2. Cross-page client workflows belong to Redux slices (auth/cart/branch/ui).
3. Ephemeral per-component state belongs to `useState`.

#### Why `errorCode`-driven logic?

1. `errorCode` is stable and automation-friendly.
2. Message text can change due to localization or copy updates.
3. Allows deterministic behavior in UI and AI-driven edits.

---

### 5. Strict Development Rules (CRITICAL)

#### Non-Negotiable

- ❌ No direct API calls outside RTK Query (`fetch`, `axios`, etc.) for app data flows.
- ❌ No business logic inside presentational UI components.
- ❌ No branch/tenant assumptions from hardcoded values.
- ❌ No mutation flow without explicit error handling.
- ❌ No financial mutation retries that can duplicate charges.

#### Required

- ✅ Use feature hooks and handlers for orchestration logic.
- ✅ Keep each feature in its own folder (`api`, `types`, `components`, `pages`).
- ✅ Use `errorCode` for behavior, message for display only.
- ✅ Always configure proper `providesTags`/`invalidatesTags`.
- ✅ Keep branch isolation intact by relying on centralized header injection and branch-aware endpoints.

#### Enforcement Heuristic (Before Merge)

```
If code touches money/inventory/shift:
  - Has transactional backend endpoint?
  - Uses idempotency for write path?
  - Has explicit frontend errorCode handling?
  - Invalidates/refetches affected cache tags?
If any answer is "No" → Not production-ready.
```

---

### 6. Anti-Patterns (And Correct Fixes)

#### Anti-Pattern 1: Duplicated State Source

Bad:

- Storing `products` in component `useState` and also relying on RTK Query data.

Risk:

- UI drift and stale data after mutations.

Fix:

- Keep server entities in RTK Query only.
- Keep only UI-local flags in `useState`.

#### Anti-Pattern 2: Broken Cache Boundaries

Bad:

- Mutation updates backend but endpoint has no `invalidatesTags`.

Risk:

- User sees old values and repeats actions (can cause duplicate business actions).

Fix:

- Define stable tag strategy and invalidate all dependent views.

#### Anti-Pattern 3: Over-Invalidation

Bad:

- Invalidating too broad tags on every small change.

Risk:

- Excess network load, flickering UI, and reduced POS responsiveness.

Fix:

- Use item-level tags when possible plus collection-level only when needed.

#### Anti-Pattern 4: Business Rule in JSX

Bad:

- Complex discount/tax/payment eligibility conditions inside render blocks.

Risk:

- Hard-to-test logic and inconsistent behavior across pages.

Fix:

- Move rule logic to hook/util layer and unit-test separately.

---

### 7. AI Agent Playbook (🔥 Safe Modification Protocol)

Use this protocol for every non-trivial change.

#### A) Adding a New Feature Safely

1. Map feature boundary and required backend endpoints.
2. Create/align `types` with backend DTOs.
3. Add or extend RTK Query endpoints with tags.
4. Implement feature components with permission-aware UI.
5. Add route and navigation entry.
6. Verify loading/error/success states.
7. Run build and manual flow test in browser.

#### B) Modifying Existing API Usage

1. Locate endpoint definition in `src/api/*Api.ts`.
2. Update request/response types first.
3. Update endpoint `query` shape and tag strategy.
4. Update all call sites using generated hooks.
5. Validate `errorCode` handling did not regress.

#### C) Extending State Safely

1. Decide state home:
   - Server state → RTK Query.
   - Cross-page client workflow → Redux slice.
   - Page-local UI state → `useState`.
2. Add selectors instead of duplicating derivations in components.
3. Keep reducers minimal and predictable.

#### D) Debugging as an Agent

1. Reproduce with exact user path.
2. Trace event handler -> hook -> endpoint -> response.
3. Validate headers (`Authorization`, `X-Branch-Id`, idempotency where needed).
4. Confirm cache invalidation/refetch behavior.
5. Confirm UI selector inputs changed after response.

#### Agent Do/Don't Summary

```
DO:
  - Start from existing patterns in the same feature.
  - Keep modifications local and typed.
  - Preserve tenant/branch isolation assumptions.

DON'T:
  - Introduce direct networking in components.
  - Move domain rules into presentational UI.
  - Change endpoint contracts without aligning types + handlers.
```

---

### 8. Debugging Guide (Operational Triage)

#### If API is not working

1. Check browser network tab:
   - Is request sent?
   - Endpoint URL/method correct?
   - Status code?
2. Check request headers:
   - `Authorization` present and valid?
   - `X-Branch-Id` present for branch-scoped endpoint?
3. Check response payload:
   - Is `errorCode` present?
   - Does UI handle this `errorCode` branch?
4. Check backend logs for same correlation window.

#### If UI is not updating

1. Confirm mutation actually succeeded (`unwrap` did not throw).
2. Confirm endpoint has correct `invalidatesTags`.
3. Confirm query has matching `providesTags`.
4. Confirm component is reading from hook/selector, not stale copied local state.
5. Check memoization traps (`useMemo`/`React.memo`) with stale dependencies.

#### If cache seems broken

1. Identify tag ownership for the affected entity.
2. Validate invalidation coverage for create/update/delete.
3. Force refetch once to verify backend data correctness.
4. If polling is involved, verify interval and skip conditions.
5. For urgent production issue, apply targeted invalidation instead of full reset.

#### If financial action appears duplicated

1. Check if mutation endpoint was retried incorrectly.
2. Check idempotency key was included on write request.
3. Check user performed repeated click due to missing loading lock.
4. Validate backend treated second request as duplicate/no-op.

---

### 9. Scaling Strategy (Grow Without Breaking)

#### Module Scaling Blueprint

When adding a new module (e.g., Promotions, Loyalty, Procurement), keep the same vertical slice:

```
src/
  api/{module}Api.ts
  types/{module}.types.ts
  components/{module}/...
  pages/{module}/{Module}Page.tsx
  hooks/use{Module}.ts (if orchestration logic is shared)
```

#### Scaling Rules

1. Prefer extending existing patterns over inventing new conventions.
2. Keep each module's cache tags explicit and bounded.
3. Avoid cross-module direct state coupling; share via selectors/hooks.
4. Split components when a file mixes orchestration + presentation heavily.
5. Add E2E path for any feature that impacts money, stock, or permissions.

#### Change Budget Strategy

For high-risk production modules (Orders, Shifts, Payments, Inventory):

1. Contract first (types and API endpoint behavior).
2. UI second (hooks/components).
3. Reliability third (error codes, retries, idempotency, loading locks).
4. Verification last (build + manual scenario + regression path).

#### Practical Expansion Example: Adding Loyalty Points

```
Phase 1: Read-only
  - Add loyalty balances query + UI badge

Phase 2: Earn points
  - Invalidate customer + order tags on complete order

Phase 3: Redeem points
  - Add guarded mutation with explicit error codes

Phase 4: Reporting
  - Add report endpoint + page with branch-aware filters
```

This phased approach reduces risk and keeps POS checkout flow stable.

---

### 10. How to Think in This Codebase

Use this decision model before writing any code.

#### State Decision Tree (Slice vs RTK Query vs Local State)

```
Is data from backend and shared across screens?
  ├─ Yes  → RTK Query endpoint + tags
  └─ No   → continue

Is data needed across multiple distant components/routes?
  ├─ Yes  → Redux slice + selectors
  └─ No   → continue

Is data only for one screen/component interaction?
  └─ Yes  → useState / useReducer local state
```

Quick mapping for POS:

- `Orders`, `Products`, `Shifts`, `Payments` snapshots -> RTK Query.
- `Cart draft`, `Auth session`, `Selected branch`, `UI toggles` -> Redux slices.
- Modal open state, current tab, input drafts in a single page -> local state.

#### When to Create a Custom Hook

Create a hook when at least one of these is true:

1. The same orchestration logic appears in 2+ components.
2. The component mixes UI with side-effects (API calls, dispatches, navigation).
3. You need a stable, testable boundary for business workflow in frontend.

POS examples:

- `useOrders` for create/complete/cancel order orchestration.
- `useShift` for open/close shift actions and state transitions.
- `usePOSShortcuts` for keyboard shortcuts isolated from UI rendering.

#### When to Build Reusable Component vs Feature Component

Build reusable component (`components/common`) when:

1. It has no domain language (e.g., `Button`, `Modal`, `Input`).
2. It can be reused unchanged in 3+ places.

Build feature component (`components/pos`, `components/orders`, etc.) when:

1. It speaks domain language (`PaymentModal`, `OrderSummary`, `ShiftBanner`).
2. It relies on feature-specific types and behaviors.

#### 5-Minute Implementation Checklist (Developer/AI)

```
1) Define state home (RTK Query / slice / local)
2) Define/confirm DTO types
3) Implement endpoint + tag strategy
4) Implement hook orchestration
5) Render UI component (lean, mostly props)
6) Handle loading/error/success and errorCode branches
7) Verify refetch/invalidation behavior in DevTools
```

---

### 🔥 Golden Rules (Must Not Break)

These are non-negotiable for production safety.

1. Never call backend APIs directly from components; all app data requests must go through RTK Query.
2. Never place business rules in JSX render paths; keep domain logic in hooks/utils.
3. Never use message text for branching logic; use `errorCode` only.
4. Never duplicate server entities in local component state if RTK Query already owns them.
5. Never merge feature code without explicit `providesTags`/`invalidatesTags` coverage.
6. Never allow financial mutations to auto-retry in frontend; protect against duplicate operations.
7. Never bypass branch and tenant context assumptions; headers and branch scope must remain intact.
8. Always use `.unwrap()` for mutations and handle both success and failure branches explicitly.
9. Always keep feature boundaries clear: `types` + `api` + `components` + `pages` grouped by feature.
10. Always keep presentational components pure (props in, UI out) with no side-effects.
11. Always add permission guards for sensitive UI actions (create/update/delete/financial).
12. Always validate changes with type-check + critical manual flow before considering the task done.

Operational enforcement:

```
If any Golden Rule is violated:
  - Block merge
  - Fix architecture first
  - Re-run critical POS flow validation
```

---

## 🚀 Getting Started

### Prerequisites

- Node.js 18+ (LTS recommended)
- npm 9+
- Backend API running on `http://localhost:5243`

### Installation

```bash
# Clone repository
git clone <repo-url>
cd frontend

# Install dependencies
npm install

# Copy environment file
cp .env.example .env

# Start development server
npm run dev
```

### Development

```bash
# Start dev server (http://localhost:3000)
npm run dev

# Type check
npm run build

# Run E2E tests
npm run test:e2e

# Run E2E tests with UI
npm run test:e2e:ui
```

### Production Build

```bash
# Build for production
npm run build

# Preview production build
npm run preview
```

### Environment Variables

```env
# .env
VITE_API_URL=http://localhost:5243/api
VITE_APP_NAME=KasserPro
```

---

## 📞 Support & Resources

### Documentation

- **Architecture**: `.kiro/steering/architecture.md`
- **API Contract**: `.kiro/steering/api-contract.md`
- **Best Practices**: `.kiro/skills/kasserpro-bestpractices/SKILL.md`
- **Developer Guide**: `.kiro/steering/kasserpro-developer-guide.md`

### Key Technologies

- [React Documentation](https://react.dev/)
- [TypeScript Handbook](https://www.typescriptlang.org/docs/)
- [Redux Toolkit](https://redux-toolkit.js.org/)
- [RTK Query](https://redux-toolkit.js.org/rtk-query/overview)
- [TailwindCSS](https://tailwindcss.com/)
- [React Hook Form](https://react-hook-form.com/)
- [Zod](https://zod.dev/)
- [Playwright](https://playwright.dev/)

---

## 📄 License

Proprietary - KasserPro © 2026

---

## 🔁 Documentation Sync Rules

### ⚠️ CRITICAL: Keep README in Sync with Code

This README is a **living document**. It MUST be updated whenever code changes affect documented behavior.

### When to Update README

| Code Change                      | Required README Update | Section to Update                             |
| -------------------------------- | ---------------------- | --------------------------------------------- |
| **Add/modify API endpoint**      | ✅ MANDATORY           | "API Integration" + "How to Add New Features" |
| **Add/modify Redux slice**       | ✅ MANDATORY           | "State Management Strategy"                   |
| **Add/modify hook**              | ✅ MANDATORY           | "Folder Structure" → `src/hooks/`             |
| **Add/modify component pattern** | ✅ MANDATORY           | "Component Architecture"                      |
| **Change folder structure**      | ✅ MANDATORY           | "Folder Structure Explained"                  |
| **Add/remove dependency**        | ✅ MANDATORY           | Tech stack section                            |
| **Change error handling**        | ✅ MANDATORY           | "Error Handling"                              |
| **Change authentication flow**   | ✅ MANDATORY           | "Authentication & Authorization"              |
| **Add/modify utility**           | ✅ MANDATORY           | "Folder Structure" → `src/utils/`             |
| **Change build config**          | ✅ MANDATORY           | "Performance Considerations"                  |

### Update Workflow

```
1. Make code change
   ↓
2. Identify affected README sections
   ↓
3. Update README in SAME commit/PR
   ↓
4. Run documentation verification (see below)
   ↓
5. Get PR approval (reviewer MUST check README)
```

---

## ✅ Pull Request Checklist

### Before Submitting ANY PR

**Copy this checklist to your PR description:**

```markdown
## Documentation Checklist

- [ ] **API Changes**: Did you add/modify API endpoints?
  - [ ] Updated API slice in `src/api/`
  - [ ] Updated "API Integration" section in README
  - [ ] Updated "How to Add New Features" examples if pattern changed

- [ ] **State Changes**: Did you add/modify Redux slices?
  - [ ] Updated slice interface in README
  - [ ] Updated actions list in README
  - [ ] Updated selectors list in README
  - [ ] Updated "State Management Strategy" section

- [ ] **Component Changes**: Did you add/modify component patterns?
  - [ ] Updated "Component Architecture" section
  - [ ] Updated component examples if pattern changed
  - [ ] Updated folder structure if new component type added

- [ ] **Hook Changes**: Did you add/modify hooks?
  - [ ] Added hook to list in "Folder Structure" → `src/hooks/`
  - [ ] Documented hook purpose and usage

- [ ] **Utility Changes**: Did you add/modify utilities?
  - [ ] Added utility to list in "Folder Structure" → `src/utils/`
  - [ ] Documented utility purpose

- [ ] **Flow Changes**: Did you change data flow or architecture?
  - [ ] Updated "Data Flow" section
  - [ ] Updated architecture diagrams if needed

- [ ] **Error Handling Changes**: Did you add/modify error codes?
  - [ ] Updated "Error Handling" section
  - [ ] Updated error code mapping table

- [ ] **Breaking Changes**: Does this PR break existing behavior?
  - [ ] Added entry to "Recent Breaking Changes" section
  - [ ] Updated migration guide if needed

- [ ] **Configuration Changes**: Did you modify build/config files?
  - [ ] Updated relevant configuration sections
  - [ ] Updated "Getting Started" if setup changed

## Verification

- [ ] Ran `npm run build` - no TypeScript errors
- [ ] Compared README examples with actual code - all match
- [ ] Checked all links in README - all work
- [ ] Reviewed "Common Pitfalls" - added new ones if discovered
```

### Reviewer Responsibilities

**Reviewers MUST:**

1. ✅ Verify README updates match code changes
2. ✅ Check that examples in README still work
3. ✅ Ensure no outdated information remains
4. ❌ REJECT PR if README is not updated

---

## 🤖 AI Agent Enforcement Rules

### For AI Coding Assistants (Kiro, Cursor, Copilot, etc.)

**⚠️ MANDATORY BEHAVIOR:**

```
IF you modify ANY of the following:
  - API endpoints (add/modify/delete)
  - Redux slices (state/actions/selectors)
  - Component patterns
  - Hooks
  - Utilities
  - Error handling
  - Authentication flow
  - Data flow
  - Folder structure

THEN you MUST:
  1. Update the corresponding README section
  2. Verify examples still work
  3. Update code snippets if needed
  4. Mention README update in your response
```

### AI Agent Checklist

Before completing ANY task:

```
1. [ ] Did I change code behavior?
2. [ ] Is this behavior documented in README?
3. [ ] Did I update README to match?
4. [ ] Did I verify examples still work?
5. [ ] Did I check for broken links/references?
```

### Forbidden AI Behaviors

```
❌ NEVER modify code without checking README impact
❌ NEVER assume README is up-to-date
❌ NEVER skip README updates "for later"
❌ NEVER copy outdated examples from README
❌ NEVER reference non-existent files/folders
```

### Required AI Behaviors

```
✅ ALWAYS read README before generating code
✅ ALWAYS update README in same response as code change
✅ ALWAYS verify folder structure matches reality
✅ ALWAYS check if examples reference existing files
✅ ALWAYS mention documentation updates explicitly
```

---

## 🧪 Documentation Verification Strategy

### How to Verify README Accuracy

Run these checks periodically (monthly or after major changes):

#### 1. **Folder Structure Verification**

```bash
# Compare documented structure with actual
# Check src/api/
ls frontend/src/api/*.ts | wc -l
# Should match count in README

# Check src/hooks/
ls frontend/src/hooks/*.ts
# Compare with list in README

# Check src/components/
ls -R frontend/src/components/
# Verify folder organization matches README
```

#### 2. **Redux Slice Verification**

```bash
# For each slice documented in README:
# 1. Open the slice file
# 2. Compare interface with README
# 3. Compare actions with README
# 4. Compare selectors with README

# Example for authSlice:
cat frontend/src/store/slices/authSlice.ts | grep "interface AuthState" -A 10
# Compare output with README documentation
```

#### 3. **API Endpoint Verification**

```bash
# List all API files
ls frontend/src/api/*.ts

# For each API file:
# 1. Check endpoints match README examples
# 2. Verify query/mutation patterns are documented
# 3. Check tag types are listed in baseApi

# Example:
grep "endpoints:" frontend/src/api/productsApi.ts -A 50
```

#### 4. **Hook Verification**

```bash
# List all hooks
ls frontend/src/hooks/*.ts

# Compare with documented list in README
# Check for:
# - Missing hooks in README
# - Documented hooks that don't exist
# - Hook descriptions that don't match implementation
```

#### 5. **Component Pattern Verification**

```bash
# Check if documented component examples exist
# Example: Button component
test -f frontend/src/components/common/Button.tsx && echo "✅ Exists" || echo "❌ Missing"

# Check if documented patterns match actual code
cat frontend/src/components/common/Button.tsx | grep "interface ButtonProps" -A 10
```

#### 6. **Configuration Verification**

```bash
# Verify vite.config.ts matches README
cat frontend/vite.config.ts | grep "manualChunks" -A 10

# Verify tailwind.config.js matches README
cat frontend/tailwind.config.js | grep "colors:" -A 20

# Verify package.json scripts match README
cat frontend/package.json | grep "scripts" -A 10
```

### Automated Verification Script

Create `scripts/verify-docs.sh`:

```bash
#!/bin/bash

echo "🔍 Verifying README accuracy..."

# Check folder structure
echo "📁 Checking folder structure..."
if [ ! -d "frontend/src/components/ui" ]; then
  echo "✅ components/ui/ correctly not documented (doesn't exist)"
else
  echo "❌ components/ui/ exists but shouldn't"
fi

# Check hooks count
HOOKS_COUNT=$(ls frontend/src/hooks/*.ts 2>/dev/null | wc -l)
echo "📊 Found $HOOKS_COUNT hooks (verify against README)"

# Check API files count
API_COUNT=$(ls frontend/src/api/*.ts 2>/dev/null | wc -l)
echo "📊 Found $API_COUNT API files (verify against README)"

# Check slices
SLICES_COUNT=$(ls frontend/src/store/slices/*.ts 2>/dev/null | wc -l)
echo "📊 Found $SLICES_COUNT slices (verify against README)"

echo "✅ Verification complete - manually review counts"
```

---

## 🔍 Documentation Drift Detection Guide

### How to Detect Outdated Documentation

**Symptoms of Documentation Drift:**

#### 1. **Code-Documentation Mismatch**

```typescript
// ❌ README says:
interface UIState {
  isSidebarOpen: boolean;
  activeModal: string | null;
}

// ✅ Code actually has:
interface UiState {
  isPaymentModalOpen: boolean;
  isReceiptModalOpen: boolean;
  isSidebarOpen: boolean;
  currentOrderId: number | null;
}

// 🚨 DRIFT DETECTED - Update README immediately
```

#### 2. **Missing Documentation**

```typescript
// ✅ Code has new hook:
// frontend/src/hooks/useInactivityMonitor.ts

// ❌ README doesn't mention it
// 🚨 DRIFT DETECTED - Add to hooks list
```

#### 3. **Outdated Examples**

```typescript
// ❌ README example references:
import { ProductList } from "@/components/products/ProductList";

// ✅ File doesn't exist
// 🚨 DRIFT DETECTED - Update example
```

#### 4. **Wrong Folder Structure**

```
❌ README documents:
components/
├── ui/  ← Doesn't exist

✅ Reality:
components/
├── common/  ← Actual folder

🚨 DRIFT DETECTED - Fix folder structure
```

#### 5. **Outdated Configuration**

```typescript
// ❌ README shows:
manualChunks: {
  'react-vendor': [...],
  'redux-vendor': [...],
}

// ✅ vite.config.ts has:
manualChunks: {
  vendor: [...],
  redux: [...],
  ui: [...],
}

// 🚨 DRIFT DETECTED - Update config section
```

### Detection Workflow

```
1. Developer notices discrepancy
   ↓
2. Create GitHub issue: "Documentation Drift: [Section Name]"
   ↓
3. Assign to documentation owner
   ↓
4. Fix in next PR
   ↓
5. Add to "Recent Changes" section
```

### Prevention Strategies

1. **Code Review**: Reviewer checks README updates
2. **CI/CD**: Add automated checks (folder structure, file existence)
3. **Monthly Audit**: Run verification script
4. **AI Enforcement**: AI agents update docs automatically
5. **Team Culture**: Make documentation a priority

---

## 🚨 Breaking Change Protocol

### Definition of Breaking Change

A change is "breaking" if it:

- Changes API endpoint signatures
- Removes/renames Redux actions or selectors
- Changes component prop interfaces
- Modifies data flow significantly
- Changes authentication/authorization behavior
- Removes/renames hooks or utilities

### Breaking Change Workflow

```
1. Identify breaking change during development
   ↓
2. Document the change in code comments
   ↓
3. Update README with migration guide
   ↓
4. Add entry to "Recent Breaking Changes" section
   ↓
5. Notify team via Slack/email
   ↓
6. Update all affected code in same PR
```

### Breaking Change Template

Add to "Recent Breaking Changes" section:

````markdown
### [Date] - [Change Title]

**Type**: Breaking Change

**What Changed**:

- Old behavior: [describe]
- New behavior: [describe]

**Migration Guide**:

```typescript
// ❌ Old code (no longer works)
const data = useOldHook();

// ✅ New code (correct way)
const data = useNewHook();
```
````

**Files Affected**:

- `src/hooks/useOldHook.ts` → Removed
- `src/hooks/useNewHook.ts` → Added

**README Sections Updated**:

- "Folder Structure" → hooks list
- "How to Add New Features" → updated examples

```

---

## 📝 Recent Breaking Changes

### April 4, 2026 - Documentation Audit & Fixes

**Type**: Documentation Update (Non-Breaking)

**What Changed**:
- Fixed `uiSlice` documentation (state interface was wrong)
- Fixed `branchSlice` documentation (property name was wrong)
- Removed references to non-existent `components/ui/` folder
- Added `@` alias documentation
- Clarified toast library usage (Sonner vs react-hot-toast)

**Migration Guide**: No code changes required - documentation only

**README Sections Updated**:
- "State Management Strategy" → Redux Slices
- "Folder Structure Explained" → components organization
- "UI & Styling System" → Toast notifications
- Added "Import Alias" section

---

## 🎯 Documentation Ownership

### Responsibilities

| Role | Responsibility |
|------|----------------|
| **Developer** | Update README when changing code |
| **Reviewer** | Verify README updates in PR |
| **Tech Lead** | Monthly documentation audit |
| **AI Agent** | Auto-update docs when generating code |

### Documentation SLA

- **Critical Updates** (API, state, auth): Same PR as code change
- **Minor Updates** (examples, typos): Within 1 week
- **Audit Fixes**: Monthly review cycle

---

## 🔧 Documentation Maintenance Checklist

### Monthly (1st of each month)

- [ ] Run `scripts/verify-docs.sh`
- [ ] Compare folder structure with README
- [ ] Verify all code examples still work
- [ ] Check for broken links
- [ ] Update "Last Updated" date
- [ ] Review "Recent Breaking Changes" - archive old ones

### Quarterly (Every 3 months)

- [ ] Full README audit (compare with codebase)
- [ ] Update architecture diagrams if needed
- [ ] Review and update "Common Pitfalls"
- [ ] Update dependency versions in tech stack
- [ ] Verify all external links still work

### After Major Release

- [ ] Document all breaking changes
- [ ] Update version number
- [ ] Archive old breaking changes
- [ ] Update "Getting Started" if setup changed

---

**Last Updated**: April 4, 2026
**Maintained By**: Frontend Development Team
**Version**: 1.0.0
**Next Audit Due**: May 1, 2026

---

> **Remember**: This is a production system handling real money. Code quality, security, and reliability are non-negotiable. When in doubt, ask. When confident, test. Always prioritize correctness over speed.
>
> **Documentation is Code**: Treat README updates with the same rigor as code changes. Outdated documentation is worse than no documentation.

```
