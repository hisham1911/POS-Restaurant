import { useState, useRef, useEffect, useCallback, useMemo } from "react";
import { useProducts, useCategories } from "@/hooks/useProducts";
import { ProductGrid } from "@/components/pos/ProductGrid";
import { CategoryTabs } from "@/components/pos/CategoryTabs";
import { Cart } from "@/components/pos/Cart";
import { PaymentModal } from "@/components/pos/PaymentModal";
import { LowStockAlert } from "@/components/pos/LowStockAlert";
import { ProductQuickCreateModal } from "@/components/pos/ProductQuickCreateModal";
import { CustomItemModal } from "@/components/pos/CustomItemModal";
import { Loading } from "@/components/common/Loading";
import {
  Menu,
  ScanBarcode,
  PackageCheck,
  AlertCircle,
  PlusCircle,
  FileText,
} from "lucide-react";
import { useCart } from "@/hooks/useCart";
import { useShift } from "@/hooks/useShift";
import { usePOSShortcuts } from "@/hooks/usePOSShortcuts";
import { useGetShiftWarningsQuery } from "@/api/shiftsApi";
import { useGetBranchInventoryQuery } from "@/api/inventoryApi";
import { usePOSMode } from "@/hooks/usePOSMode";
import { Customer } from "@/types/customer.types";
import { toast } from "sonner";
import clsx from "clsx";
import { Link, Navigate } from "react-router-dom";
import { ShiftWarningBanner } from "@/components/shifts";
import {
  buildBranchInventoryStockMap,
  getProductCurrentStock,
} from "@/utils/productStock";
import { usePermission } from "@/hooks/usePermission";
import { useAppSelector } from "@/store/hooks";
import { selectCurrentBranch } from "@/store/slices/branchSlice";

export const POSPage = () => {
  const { mode } = usePOSMode();

  // Redirect to workspace if mode is standard
  if (mode === "standard") {
    return <Navigate to="/pos-workspace" replace />;
  }

  const [selectedCategory, setSelectedCategory] = useState<number | null>(null);
  const [showPayment, setShowPayment] = useState(false);
  const [showMobileCart, setShowMobileCart] = useState(false);
  const [showAvailableOnly, setShowAvailableOnly] = useState(false);
  const [showQuickCreate, setShowQuickCreate] = useState(false);
  const [showCustomItem, setShowCustomItem] = useState(false);
  const [selectedCustomer, setSelectedCustomer] = useState<Customer | null>(
    null,
  );
  const [searchInput, setSearchInput] = useState("");
  const searchInputRef = useRef<HTMLInputElement>(null);

  // Hooks must be called at the top level before any callbacks that use their data
  const { products, isLoading } = useProducts();
  const { categories } = useCategories();
  const { addItem, itemsCount } = useCart();
  const { hasActiveShift, isLoading: isLoadingShift } = useShift();
  const currentBranch = useAppSelector(selectCurrentBranch);
  const { hasPermission } = usePermission();
  const canQuickCreateProduct =
    hasPermission("ProductsCreateFromPOS") || hasPermission("ProductsManage");
  const { data: branchInventory, isLoading: isInventoryLoading } =
    useGetBranchInventoryQuery(currentBranch?.id ?? 0, {
      skip: !currentBranch?.id,
    });
  const stockByProductId = useMemo(
    () => buildBranchInventoryStockMap(branchInventory),
    [branchInventory],
  );
  const hasInventorySnapshot = Array.isArray(branchInventory);

  // Fetch shift warnings (polls every 10 minutes in POS)
  const { data: warningsData } = useGetShiftWarningsQuery(undefined, {
    pollingInterval: 10 * 60 * 1000, // 10 minutes
    skip: !hasActiveShift, // Only fetch if shift is open
  });

  const shiftWarning = warningsData?.data;

  // Shortcuts
  usePOSShortcuts({
    onCheckout: () => setShowPayment(true),
    onSearch: () => {
      searchInputRef.current?.focus();
    },
  });

  // Auto-focus search input on mount
  useEffect(() => {
    if (searchInputRef.current) {
      searchInputRef.current.focus();
    }
  }, []);

  // Handle search/barcode scan (Enter key)
  const handleSearchSubmit = useCallback(
    (value: string) => {
      const trimmedValue = value.trim();
      if (!trimmedValue) return;

      // Search by barcode, SKU, or name
      const foundProduct = products.find(
        (p) =>
          (p.barcode &&
            p.barcode.toLowerCase() === trimmedValue.toLowerCase()) ||
          (p.sku && p.sku.toLowerCase() === trimmedValue.toLowerCase()) ||
          p.name.toLowerCase() === trimmedValue.toLowerCase(),
      );

      if (foundProduct) {
        const branchInventoryQuantity = getProductCurrentStock(
          foundProduct,
          stockByProductId,
        );
        const productForCart = hasInventorySnapshot
          ? ({
              ...foundProduct,
              branchInventoryQuantity,
            } as typeof foundProduct)
          : foundProduct;

        addItem(productForCart, 1);
        toast.success(`تمت الإضافة: ${foundProduct.name}`);
        // Clear input and refocus
        setSearchInput("");
        searchInputRef.current?.focus();
      } else {
        toast.error(`لم يتم العثور على منتج: ${trimmedValue}`);
      }
    },
    [products, addItem, hasInventorySnapshot, stockByProductId],
  );

  const handleSearchKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter") {
      e.preventDefault();
      handleSearchSubmit(searchInput);
    }
  };

  // Filter products by search, category and availability
  let filteredProducts = products;

  // Filter by search text
  if (searchInput.trim()) {
    const searchLower = searchInput.toLowerCase().trim();
    filteredProducts = filteredProducts.filter(
      (p) =>
        p.name.toLowerCase().includes(searchLower) ||
        (p.barcode && p.barcode.toLowerCase().includes(searchLower)) ||
        (p.sku && p.sku.toLowerCase().includes(searchLower)),
    );
  }

  // Filter by category
  if (selectedCategory) {
    filteredProducts = filteredProducts.filter(
      (p) => p.categoryId === selectedCategory,
    );
  }

  // Filter by available stock if enabled
  if (showAvailableOnly) {
    filteredProducts = filteredProducts.filter((p) => {
      // If product doesn't track inventory, it's always available
      if (!p.trackInventory) return true;
      if (!hasInventorySnapshot) return true;
      // Only show products with stock > 0
      return getProductCurrentStock(p, stockByProductId) > 0;
    });
  }

  if (isLoading || isLoadingShift) {
    return (
      <div className="h-full flex items-center justify-center bg-gray-50">
        <Loading />
      </div>
    );
  }

  // Show warning if no active shift
  if (!hasActiveShift) {
    return (
      <div className="h-full flex items-center justify-center bg-gray-50 p-4">
        <div className="max-w-md w-full bg-white rounded-2xl shadow-lg p-8 text-center">
          <div className="w-16 h-16 bg-warning-100 rounded-full flex items-center justify-center mx-auto mb-4">
            <AlertCircle className="w-8 h-8 text-warning-600" />
          </div>
          <h2 className="text-2xl font-bold text-gray-800 mb-2">
            لا توجد وردية مفتوحة
          </h2>
          <p className="text-gray-600 mb-6">
            يجب فتح وردية قبل البدء في البيع. اذهب إلى صفحة الورديات لفتح وردية
            جديدة.
          </p>
          <Link
            to="/shift"
            className="inline-flex items-center justify-center px-6 py-3 bg-primary-600 text-white rounded-xl hover:bg-primary-700 transition-colors font-medium"
          >
            الذهاب إلى الورديات
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="h-full flex overflow-hidden bg-gray-50">
      {/* Products Section */}
      <div className="flex-1 flex flex-col p-4 min-w-0">
        {/* Shift Warning Banner */}
        {shiftWarning && shiftWarning.shouldWarn && (
          <div className="mb-4">
            <ShiftWarningBanner warning={shiftWarning} />
          </div>
        )}

        {/* Low Stock Alert */}
        <LowStockAlert />

        {/* Search Input */}
        <div className="mb-3">
          <div className="relative">
            <ScanBarcode className="absolute right-3 top-1/2 -translate-y-1/2 w-5 h-5 text-primary-400" />
            <input
              ref={searchInputRef}
              type="text"
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              onKeyDown={handleSearchKeyDown}
              placeholder="🔍 بحث (اضغط Enter للإضافة)"
              className="w-full pl-3 pr-11 py-3 border-2 border-gray-200 rounded-xl bg-white focus:border-primary-500 focus:ring-2 focus:ring-primary-100 focus:outline-none text-base"
              autoComplete="off"
            />
          </div>
        </div>

        {/* Desktop: Categories and Action Buttons - Icon Only with Tooltips */}
        <div className="hidden lg:flex items-center gap-2 mb-4 flex-wrap">
          {/* All Categories */}
          <button
            onClick={() => setSelectedCategory(null)}
            className={clsx(
              "relative group p-2.5 rounded-lg text-xl border-2 transition-all",
              selectedCategory === null
                ? "bg-primary-600 text-white border-primary-500"
                : "bg-white text-gray-700 border-gray-300 hover:border-primary-400 hover:bg-primary-50"
            )}
            title="الكل"
          >
            🏪
            <span className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 px-2 py-1 bg-gray-900 text-white text-xs rounded whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none">
              الكل
            </span>
          </button>

          {/* Categories */}
          {categories.map((category) => (
            <button
              key={category.id}
              onClick={() => setSelectedCategory(category.id)}
              className={clsx(
                "relative group p-2.5 rounded-lg text-xl border-2 transition-all",
                selectedCategory === category.id
                  ? "bg-primary-600 text-white border-primary-500"
                  : "bg-white text-gray-700 border-gray-300 hover:border-primary-400 hover:bg-primary-50"
              )}
              title={category.name}
            >
              {category.imageUrl || "📁"}
              <span className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 px-2 py-1 bg-gray-900 text-white text-xs rounded whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none z-10">
                {category.name}
              </span>
            </button>
          ))}

          {/* Divider */}
          <div className="w-px h-8 bg-gray-300 mx-1" />

          {/* Available Stock Filter */}
          <button
            onClick={() => setShowAvailableOnly(!showAvailableOnly)}
            className={clsx(
              "relative group p-2.5 rounded-lg border-2 transition-all",
              showAvailableOnly
                ? "bg-green-100 text-green-700 border-green-400"
                : "bg-white text-gray-700 border-gray-300 hover:border-green-400 hover:bg-green-50",
            )}
            title="المتاح فقط"
          >
            <PackageCheck className="w-5 h-5" />
            <span className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 px-2 py-1 bg-gray-900 text-white text-xs rounded whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none z-10">
              المتاح فقط
            </span>
          </button>

          {/* Quick Create Product */}
          <button
            onClick={() => {
              if (!canQuickCreateProduct) {
                toast.error("ليس لديك صلاحية إضافة منتج سريع");
                return;
              }
              setShowQuickCreate(true);
            }}
            disabled={!canQuickCreateProduct}
            className={clsx(
              "relative group p-2.5 rounded-lg border-2 transition-all",
              canQuickCreateProduct
                ? "bg-primary-600 text-white border-primary-500 hover:bg-primary-700"
                : "bg-gray-100 text-gray-400 border-gray-200 cursor-not-allowed",
            )}
            title="منتج جديد"
          >
            <PlusCircle className="w-5 h-5" />
            {canQuickCreateProduct && (
              <span className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 px-2 py-1 bg-gray-900 text-white text-xs rounded whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none z-10">
                منتج جديد
              </span>
            )}
          </button>

          {/* Custom Item */}
          <button
            onClick={() => setShowCustomItem(true)}
            className="relative group p-2.5 rounded-lg border-2 bg-secondary-600 text-white border-secondary-500 hover:bg-secondary-700 transition-all"
            title="منتج مخصص"
          >
            <FileText className="w-5 h-5" />
            <span className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 px-2 py-1 bg-gray-900 text-white text-xs rounded whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none z-10">
              منتج مخصص
            </span>
          </button>
        </div>

        {/* Mobile: Compact Filter Bar */}
        <div className="lg:hidden space-y-2 mb-3">
          {/* Row 1: Main Filters */}
          <div className="flex items-center gap-2">
            {/* Category Selector - Compact */}
            <select
              value={selectedCategory ?? ""}
              onChange={(e) => setSelectedCategory(e.target.value ? Number(e.target.value) : null)}
              className="flex-1 px-3 py-2 border-2 border-gray-200 rounded-lg bg-white text-sm font-medium text-gray-700 focus:border-primary-500 focus:outline-none"
            >
              <option value="">🏪 الكل</option>
              {categories.map((category) => (
                <option key={category.id} value={category.id}>
                  {category.imageUrl || "📁"} {category.name}
                </option>
              ))}
            </select>

            {/* Available Stock Toggle - Compact */}
            <button
              onClick={() => setShowAvailableOnly(!showAvailableOnly)}
              className={clsx(
                "p-2 rounded-lg border-2 transition-all shrink-0",
                showAvailableOnly
                  ? "bg-green-100 text-green-700 border-green-400"
                  : "bg-white text-gray-700 border-gray-300",
              )}
              title="المتاح فقط"
            >
              <PackageCheck className="w-5 h-5" />
            </button>

            {/* Mobile cart toggle */}
            <button
              onClick={() => setShowMobileCart(!showMobileCart)}
              className="relative p-2 border-2 border-gray-300 rounded-lg hover:bg-primary-50 hover:border-primary-400 shrink-0"
            >
              <Menu className="w-5 h-5" />
              {itemsCount > 0 && (
                <span className="absolute -top-1.5 -right-1.5 bg-primary-600 text-white text-xs font-bold w-5 h-5 rounded-full flex items-center justify-center">
                  {itemsCount}
                </span>
              )}
            </button>
          </div>

          {/* Row 2: Quick Actions */}
          <div className="flex items-center gap-2">
            {/* Quick Create Product */}
            <button
              onClick={() => {
                if (!canQuickCreateProduct) {
                  toast.error("ليس لديك صلاحية إضافة منتج سريع");
                  return;
                }
                setShowQuickCreate(true);
              }}
              disabled={!canQuickCreateProduct}
              className={clsx(
                "flex-1 flex items-center justify-center gap-2 px-3 py-2 rounded-lg text-sm font-medium border-2 transition-all",
                canQuickCreateProduct
                  ? "bg-primary-600 text-white border-primary-500"
                  : "bg-gray-100 text-gray-400 border-gray-200 cursor-not-allowed",
              )}
            >
              <PlusCircle className="w-4 h-4" />
              <span>منتج جديد</span>
            </button>

            {/* Custom Item */}
            <button
              onClick={() => setShowCustomItem(true)}
              className="flex-1 flex items-center justify-center gap-2 px-3 py-2 rounded-lg text-sm font-medium border-2 bg-secondary-600 text-white border-secondary-500"
            >
              <FileText className="w-4 h-4" />
              <span>منتج مخصص</span>
            </button>
          </div>
        </div>

        {/* Products Grid */}
        <div className="flex-1 overflow-y-auto scrollbar-thin min-h-0">
          <ProductGrid
            products={filteredProducts}
            categories={categories}
            stockByProductId={stockByProductId}
            hasInventorySnapshot={hasInventorySnapshot}
            isInventoryLoading={isInventoryLoading}
          />
        </div>
      </div>

      {/* Cart Section - Desktop */}
      <div className="hidden lg:flex w-[400px] bg-white border-l-2 border-gray-100 p-5 flex-col shrink-0">
        <Cart
          onCheckout={() => setShowPayment(true)}
          selectedCustomer={selectedCustomer}
          onCustomerSelect={setSelectedCustomer}
        />
      </div>

      {/* Cart Section - Mobile */}
      {showMobileCart && (
        <div className="lg:hidden fixed inset-0 z-40">
          <div
            className="absolute inset-0 bg-black/50"
            onClick={() => setShowMobileCart(false)}
          />
          <div className="absolute right-0 top-0 bottom-0 w-[85%] max-w-md bg-white p-5 animate-slide-in-right flex flex-col">
            <Cart
              onCheckout={() => {
                setShowMobileCart(false);
                setShowPayment(true);
              }}
              selectedCustomer={selectedCustomer}
              onCustomerSelect={setSelectedCustomer}
            />
          </div>
        </div>
      )}

      {/* Payment Modal */}
      {showPayment && (
        <PaymentModal
          onClose={() => setShowPayment(false)}
          selectedCustomer={selectedCustomer}
          onOrderComplete={() => setSelectedCustomer(null)}
        />
      )}

      {/* Quick Create Product Modal */}
      {showQuickCreate && canQuickCreateProduct && (
        <ProductQuickCreateModal
          onClose={() => setShowQuickCreate(false)}
          onSuccess={(productId) => {
            toast.success("تم إضافة المنتج بنجاح");
          }}
        />
      )}

      {/* Custom Item Modal */}
      {showCustomItem && (
        <CustomItemModal onClose={() => setShowCustomItem(false)} />
      )}
    </div>
  );
};

export default POSPage;
