import { useState, useRef, useEffect, useCallback } from "react";
import { useProducts, useCategories } from "@/hooks/useProducts";
import { ProductGrid } from "@/components/pos/ProductGrid";
import { CategoryTabs } from "@/components/pos/CategoryTabs";
import { Cart } from "@/components/pos/Cart";
import { PaymentModal } from "@/components/pos/PaymentModal";
import { LowStockAlert } from "@/components/pos/LowStockAlert";
import { ProductQuickCreateModal } from "@/components/pos/ProductQuickCreateModal";
import { CustomItemModal } from "@/components/pos/CustomItemModal";
import { Loading } from "@/components/common/Loading";
import { Menu, ScanBarcode, PackageCheck, AlertCircle, PlusCircle, FileText } from "lucide-react";
import { useCart } from "@/hooks/useCart";
import { useShift } from "@/hooks/useShift";
import { usePOSShortcuts } from "@/hooks/usePOSShortcuts";
import { useGetShiftWarningsQuery } from "@/api/shiftsApi";
import { usePOSMode } from "@/hooks/usePOSMode";
import { Customer } from "@/types/customer.types";
import { toast } from "sonner";
import clsx from "clsx";
import { Link, Navigate } from "react-router-dom";
import { ShiftWarningBanner } from "@/components/shifts";

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
    null
  );
  const [searchInput, setSearchInput] = useState("");
  const searchInputRef = useRef<HTMLInputElement>(null);

  // Hooks must be called at the top level before any callbacks that use their data
  const { products, isLoading } = useProducts();
  const { categories } = useCategories();
  const { addItem, itemsCount } = useCart();
  const { hasActiveShift, isLoading: isLoadingShift } = useShift();

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
          p.name.toLowerCase() === trimmedValue.toLowerCase()
      );

      if (foundProduct) {
        addItem(foundProduct, 1);
        toast.success(`تمت الإضافة: ${foundProduct.name}`);
        // Clear input and refocus
        setSearchInput("");
        searchInputRef.current?.focus();
      } else {
        toast.error(`لم يتم العثور على منتج: ${trimmedValue}`);
      }
    },
    [products, addItem]
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
    filteredProducts = filteredProducts.filter((p) =>
      p.name.toLowerCase().includes(searchLower) ||
      (p.barcode && p.barcode.toLowerCase().includes(searchLower)) ||
      (p.sku && p.sku.toLowerCase().includes(searchLower))
    );
  }

  // Filter by category
  if (selectedCategory) {
    filteredProducts = filteredProducts.filter((p) => p.categoryId === selectedCategory);
  }

  // Filter by available stock if enabled
  if (showAvailableOnly) {
    filteredProducts = filteredProducts.filter((p) => {
      // If product doesn't track inventory, it's always available
      if (!p.trackInventory) return true;
      // Only show products with stock > 0
      return (p.stockQuantity ?? 0) > 0;
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
            يجب فتح وردية قبل البدء في البيع. اذهب إلى صفحة الورديات لفتح وردية جديدة.
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
    <div className="h-full flex overflow-hidden">
      {/* Products Section */}
      <div className="flex-1 flex flex-col bg-gray-50 p-4 min-w-0">
        {/* Shift Warning Banner */}
        {shiftWarning && shiftWarning.shouldWarn && (
          <div className="mb-4">
            <ShiftWarningBanner warning={shiftWarning} />
          </div>
        )}

        {/* Low Stock Alert - Admin/Manager only */}
        <LowStockAlert />

        {/* Search Input */}
        <div className="mb-4">
          <div className="relative">
            <ScanBarcode className="absolute right-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
            <input
              ref={searchInputRef}
              type="text"
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              onKeyDown={handleSearchKeyDown}
              placeholder="🔍 بحث بالاسم، الباركود أو SKU (اضغط Enter للإضافة)"
              className="w-full pl-4 pr-10 py-2.5 border border-gray-200 rounded-xl bg-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 text-sm"
              autoComplete="off"
            />
          </div>
        </div>

        {/* Categories and Filters */}
        <div className="flex items-center justify-between mb-4 gap-2">
          <div className="flex items-center gap-3 flex-1 min-w-0">
            <CategoryTabs
              categories={categories}
              selectedId={selectedCategory}
              onSelect={setSelectedCategory}
            />

            {/* Available Stock Filter */}
            <button
              onClick={() => setShowAvailableOnly(!showAvailableOnly)}
              className={clsx(
                "flex items-center gap-1.5 px-3 py-2 rounded-lg text-sm font-medium transition-colors whitespace-nowrap",
                showAvailableOnly
                  ? "bg-success-100 text-success-700 border border-success-300"
                  : "bg-white text-gray-600 border border-gray-200 hover:bg-gray-50"
              )}
              title="عرض المنتجات المتاحة في المخزون فقط"
            >
              <PackageCheck className="w-4 h-4" />
              <span className="hidden sm:inline">المتاح فقط</span>
            </button>

            {/* Quick Create Product */}
            <button
              onClick={() => setShowQuickCreate(true)}
              className="flex items-center gap-1.5 px-3 py-2 rounded-lg text-sm font-medium transition-colors whitespace-nowrap bg-primary-600 text-white hover:bg-primary-700"
              title="إضافة منتج سريع"
            >
              <PlusCircle className="w-4 h-4" />
              <span className="hidden sm:inline">منتج جديد</span>
            </button>

            {/* Custom Item */}
            <button
              onClick={() => setShowCustomItem(true)}
              className="flex items-center gap-1.5 px-3 py-2 rounded-lg text-sm font-medium transition-colors whitespace-nowrap bg-secondary-600 text-white hover:bg-secondary-700"
              title="إضافة منتج مخصص للطلب الحالي"
            >
              <FileText className="w-4 h-4" />
              <span className="hidden sm:inline">منتج مخصص</span>
            </button>
          </div>

          {/* Mobile cart toggle */}
          <button
            onClick={() => setShowMobileCart(!showMobileCart)}
            className="lg:hidden relative p-2 border border-gray-200 rounded-lg hover:bg-gray-100 shrink-0"
          >
            <Menu className="w-5 h-5" />
            {itemsCount > 0 && (
              <span className="absolute -top-2 -right-2 bg-primary-600 text-white text-xs w-5 h-5 rounded-full flex items-center justify-center">
                {itemsCount}
              </span>
            )}
          </button>
        </div>

        {/* Products Grid */}
        <div className="flex-1 overflow-y-auto scrollbar-thin min-h-0">
          <ProductGrid products={filteredProducts} categories={categories} />
        </div>
      </div>

      {/* Cart Section - Desktop */}
      <div className="hidden lg:flex w-96 bg-white border-l border-gray-200 p-4 flex-col shrink-0">
        <Cart
          onCheckout={() => setShowPayment(true)}
          selectedCustomer={selectedCustomer}
          onCustomerSelect={setSelectedCustomer}
        />
      </div>

      {/* Cart Section - Mobile Slide-in */}
      {showMobileCart && (
        <div className="lg:hidden fixed inset-0 z-40">
          <div
            className="absolute inset-0 bg-black/50"
            onClick={() => setShowMobileCart(false)}
          />
          <div className="absolute right-0 top-0 bottom-0 w-80 bg-white p-4 animate-slide-in-right shadow-2xl flex flex-col">
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
      {showQuickCreate && (
        <ProductQuickCreateModal
          onClose={() => setShowQuickCreate(false)}
          onSuccess={(productId) => {
            toast.success("تم إضافة المنتج بنجاح");
            // Optionally add to cart immediately
          }}
        />
      )}

      {/* Custom Item Modal */}
      {showCustomItem && (
        <CustomItemModal
          onClose={() => setShowCustomItem(false)}
        />
      )}
    </div>
  );
};

export default POSPage;
