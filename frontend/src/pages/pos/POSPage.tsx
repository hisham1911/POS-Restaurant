import { useState, useRef, useEffect, useCallback, useMemo } from "react";
import { useProducts, useCategories } from "@/hooks/useProducts";
import { ProductGrid } from "@/components/pos/ProductGrid";
import { CategoryTabs } from "@/components/pos/CategoryTabs";
import { Cart } from "@/components/pos/Cart";
import { PaymentModal } from "@/components/pos/PaymentModal";
import { TableSelectionModal } from "@/components/pos/TableSelectionModal";
import { SavedNotesModal } from "@/components/pos/SavedNotesModal";
import { LowStockAlert } from "@/components/pos/LowStockAlert";
import { BatchExpiryAlertBanner } from "@/components/inventory";
import { ProductQuickCreateModal } from "@/components/pos/ProductQuickCreateModal";
import { CustomItemModal } from "@/components/pos/CustomItemModal";
import { BatchSelectionModal } from "@/components/pos/BatchSelectionModal";
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
import { useGetAvailableBatchesQuery } from "@/api/productBatchApi";
import { useGetRestaurantTablesQuery } from "@/api/restaurantTablesApi";
import { useGetSavedOrderNotesQuery } from "@/api/savedOrderNotesApi";
import { usePOSMode } from "@/hooks/usePOSMode";
import { Customer } from "@/types/customer.types";
import type { OrderSource, OrderType } from "@/types/order.types";
import type { RestaurantTable } from "@/types/restaurant.types";
import { Product, ProductType, UnitOfMeasure } from "@/types/product.types";
import { ProductBatch } from "@/types/productBatch.types";
import { toast } from "sonner";
import clsx from "clsx";
import { Link, Navigate } from "react-router-dom";
import { ShiftWarningBanner } from "@/components/shifts";
import {
  buildBranchInventoryStockMap,
  getProductAvailableStock,
  getProductCurrentStock,
} from "@/utils/productStock";
import { usePermission } from "@/hooks/usePermission";
import { useAppSelector } from "@/store/hooks";
import { selectCurrentBranch } from "@/store/slices/branchSlice";
import { selectAllowNegativeStock } from "@/store/slices/cartSlice";

export const POSPage = () => {
  const { mode } = usePOSMode();
  const shouldRedirectToWorkspace = mode === "standard";

  const [selectedCategory, setSelectedCategory] = useState<number | null>(null);
  const [showPayment, setShowPayment] = useState(false);
  const [showMobileCart, setShowMobileCart] = useState(false);
  const [showAvailableOnly, setShowAvailableOnly] = useState(false);
  const [showQuickCreate, setShowQuickCreate] = useState(false);
  const [showCustomItem, setShowCustomItem] = useState(false);
  const [selectedCustomer, setSelectedCustomer] = useState<Customer | null>(
    null,
  );
  const [orderType, setOrderType] = useState<OrderType>("DineIn");
  const [selectedTable, setSelectedTable] = useState<RestaurantTable | null>(
    null,
  );
  const [showTableModal, setShowTableModal] = useState(false);
  const [showSavedNotesModal, setShowSavedNotesModal] = useState(false);
  const [orderSource, setOrderSource] = useState<OrderSource>("POS");
  const [externalOrderNumber, setExternalOrderNumber] = useState("");
  const [orderNotes, setOrderNotes] = useState("");
  const [deliveryAddress, setDeliveryAddress] = useState("");
  const [deliveryFee, setDeliveryFee] = useState("");
  const [deliveryNotes, setDeliveryNotes] = useState("");
  const [searchInput, setSearchInput] = useState("");
  const searchInputRef = useRef<HTMLInputElement>(null);
  const [dismissedWarning, setDismissedWarning] = useState(false);

  // Batch selection state
  const [showBatchModal, setShowBatchModal] = useState(false);
  const [selectedProductForBatch, setSelectedProductForBatch] = useState<Product | null>(null);
  const [pendingBatchQuantity, setPendingBatchQuantity] = useState(1);

  // Hooks must be called at the top level before any callbacks that use their data
  const { products, isLoading } = useProducts();
  const { categories } = useCategories();
  const { addItem, items, itemsCount, taxRate } = useCart();
  const { hasActiveShift, isLoading: isLoadingShift } = useShift();
  const currentBranch = useAppSelector(selectCurrentBranch);
  const allowNegativeStock = useAppSelector(selectAllowNegativeStock);
  const { hasPermission } = usePermission();
  const canQuickCreateProduct =
    hasPermission("ProductsCreateFromPOS") || hasPermission("ProductsManage");
  const { data: branchInventory, isLoading: isInventoryLoading } =
    useGetBranchInventoryQuery(currentBranch?.id ?? 0, {
      skip: !currentBranch?.id,
    });
  const { data: restaurantTablesResponse } = useGetRestaurantTablesQuery(
    currentBranch?.id ?? 0,
    {
      skip: !currentBranch?.id,
    },
  );
  const { data: savedNotesResponse } = useGetSavedOrderNotesQuery(
    currentBranch?.id ?? 0,
    {
      skip: !currentBranch?.id,
    },
  );
  const restaurantTables = restaurantTablesResponse?.data ?? [];
  const savedNotes = savedNotesResponse?.data ?? [];
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

  // Reset dismissed warning when warning message changes
  useEffect(() => {
    setDismissedWarning(false);
  }, [shiftWarning?.message]);

  useEffect(() => {
    if (
      orderType === "Delivery" &&
      selectedCustomer?.address &&
      !deliveryAddress.trim()
    ) {
      setDeliveryAddress(selectedCustomer.address);
    }
  }, [deliveryAddress, orderType, selectedCustomer?.address]);

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

  const handleAddProductToCart = useCallback(
    (product: Product, options?: { showToast?: boolean }) => {
      const quantityInCart = items
        .filter((item) => item.product.id === product.id)
        .reduce((sum, item) => sum + item.quantity, 0);
      const totalStock = getProductCurrentStock(product, stockByProductId);
      const availableStock = hasInventorySnapshot
        ? getProductAvailableStock(product, quantityInCart, stockByProductId)
        : Number.POSITIVE_INFINITY;
      const requiresDirectStock =
        product.type !== ProductType.Manufactured && product.trackInventory;
      const canAddMore = product.isBatchTracked
        ? !hasInventorySnapshot || availableStock > 0
        : allowNegativeStock ||
          !requiresDirectStock ||
          !hasInventorySnapshot ||
          availableStock > 0;
      const isOutOfStock =
        !allowNegativeStock &&
        requiresDirectStock &&
        hasInventorySnapshot &&
        totalStock <= 0;

      if (!product.isActive) {
        toast.error(`Ø§Ù„Ù…Ù†ØªØ¬ ØºÙŠØ± Ù…ØªØ§Ø­ Ø§Ù„Ø¢Ù†: ${product.name}`);
        return false;
      }

      if (isOutOfStock || !canAddMore) {
        toast.error(`Ù„Ø§ ÙŠÙ…ÙƒÙ† Ø¥Ø¶Ø§ÙØ© ${product.name} Ù„Ø¹Ø¯Ù… ØªÙˆÙØ± Ù…Ø®Ø²ÙˆÙ† ÙƒØ§ÙÙ`);
        return false;
      }

      if (product.isBatchTracked && currentBranch?.id) {
        setSelectedProductForBatch(product);
        setPendingBatchQuantity(1);
        setShowBatchModal(true);
        return true;
      }

      const productForCart = hasInventorySnapshot
        ? ({
            ...product,
            branchInventoryQuantity: totalStock,
          } as Product)
        : product;

      addItem(productForCart, 1);

      if (options?.showToast) {
        toast.success(`ØªÙ…Øª Ø§Ù„Ø¥Ø¶Ø§ÙØ©: ${product.name}`);
      }

      return true;
    },
    [
      addItem,
      allowNegativeStock,
      hasInventorySnapshot,
      items,
      stockByProductId,
      currentBranch,
    ],
  );

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
        const added = handleAddProductToCart(foundProduct, { showToast: true });
        if (added) {
          setSearchInput("");
          searchInputRef.current?.focus();
        }
      } else {
        toast.error(`Ù„Ù… ÙŠØªÙ… Ø§Ù„Ø¹Ø«ÙˆØ± Ø¹Ù„Ù‰ Ù…Ù†ØªØ¬: ${trimmedValue}`);
      }
    },
    [handleAddProductToCart, products],
  );

  const handleSearchKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter") {
      e.preventDefault();
      handleSearchSubmit(searchInput);
    }
  };

  const handleOrderTypeChange = (nextOrderType: OrderType) => {
    setOrderType(nextOrderType);

    if (nextOrderType !== "DineIn") {
      setSelectedTable(null);
    }

    if (nextOrderType !== "Delivery") {
      setOrderSource("POS");
      setExternalOrderNumber("");
      setDeliveryAddress("");
      setDeliveryFee("");
      setDeliveryNotes("");
    }
  };

  const appendOrderNote = (note: string) => {
    setOrderNotes((current) => {
      const trimmed = note.trim();
      if (!trimmed) return current;
      return current.trim() ? `${current.trim()} - ${trimmed}` : trimmed;
    });
  };


  const resetRestaurantOrderState = () => {
    setSelectedCustomer(null);
    setOrderType("DineIn");
    setSelectedTable(null);
    setDeliveryAddress("");
    setDeliveryFee("");
    setDeliveryNotes("");
    setOrderSource("POS");
    setExternalOrderNumber("");
    setOrderNotes("");
  };

  // Filter products by search, category and availability
  let filteredProducts = products;

  // Filter out raw materials (only show sellable products in POS)
  filteredProducts = filteredProducts.filter(
    (p) => p.type !== ProductType.RawMaterial,
  );

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
      if (p.type === ProductType.Manufactured || !p.trackInventory) return true;
      if (!hasInventorySnapshot) return true;
      // Only show products with stock > 0
      return getProductCurrentStock(p, stockByProductId) > 0;
    });
  }

  if (shouldRedirectToWorkspace) {
    return <Navigate to="/pos-workspace" replace />;
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
            Ù„Ø§ ØªÙˆØ¬Ø¯ ÙˆØ±Ø¯ÙŠØ© Ù…ÙØªÙˆØ­Ø©
          </h2>
          <p className="text-gray-600 mb-6">
            ÙŠØ¬Ø¨ ÙØªØ­ ÙˆØ±Ø¯ÙŠØ© Ù‚Ø¨Ù„ Ø§Ù„Ø¨Ø¯Ø¡ ÙÙŠ Ø§Ù„Ø¨ÙŠØ¹. Ø§Ø°Ù‡Ø¨ Ø¥Ù„Ù‰ ØµÙØ­Ø© Ø§Ù„ÙˆØ±Ø¯ÙŠØ§Øª Ù„ÙØªØ­ ÙˆØ±Ø¯ÙŠØ©
            Ø¬Ø¯ÙŠØ¯Ø©.
          </p>
          <Link
            to="/shift"
            className="inline-flex items-center justify-center px-6 py-3 bg-primary-600 text-white rounded-xl hover:bg-primary-700 transition-colors font-medium"
          >
            Ø§Ù„Ø°Ù‡Ø§Ø¨ Ø¥Ù„Ù‰ Ø§Ù„ÙˆØ±Ø¯ÙŠØ§Øª
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
        {shiftWarning && shiftWarning.shouldWarn && !dismissedWarning && (
          <div className="mb-4">
            <ShiftWarningBanner 
              warning={shiftWarning} 
              onClose={() => setDismissedWarning(true)}
            />
          </div>
        )}

        {/* Low Stock Alert */}
        <LowStockAlert />

        {/* Batch Expiry Alert */}
        <BatchExpiryAlertBanner />

        {/* Search Input */}
        <div className="mb-3">
          <div className="relative">
            <ScanBarcode className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-primary-400" />
            <input
              ref={searchInputRef}
              type="text"
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              onKeyDown={handleSearchKeyDown}
              placeholder="Ø§Ø¨Ø­Ø« Ø¨Ø§Ù„Ø§Ø³Ù… Ø£Ùˆ Ø§Ù„Ø¨Ø§Ø±ÙƒÙˆØ¯ Ø£Ùˆ SKU"
              className="w-full rounded-xl border-2 border-gray-200 bg-white py-3 pe-11 ps-3 text-base focus:border-primary-500 focus:ring-2 focus:ring-primary-100 focus:outline-none"
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
            title="Ø§Ù„ÙƒÙ„"
          >
            ðŸª
            <span className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 px-2 py-1 bg-gray-900 text-white text-xs rounded whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none">
              Ø§Ù„ÙƒÙ„
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
              {category.imageUrl || "ðŸ“"}
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
            title="Ø§Ù„Ù…ØªØ§Ø­ ÙÙ‚Ø·"
          >
            <PackageCheck className="w-5 h-5" />
            <span className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 px-2 py-1 bg-gray-900 text-white text-xs rounded whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none z-10">
              Ø§Ù„Ù…ØªØ§Ø­ ÙÙ‚Ø·
            </span>
          </button>

          {/* Quick Create Product */}
          <button
            onClick={() => {
              if (!canQuickCreateProduct) {
                toast.error("Ù„ÙŠØ³ Ù„Ø¯ÙŠÙƒ ØµÙ„Ø§Ø­ÙŠØ© Ø¥Ø¶Ø§ÙØ© Ù…Ù†ØªØ¬ Ø³Ø±ÙŠØ¹");
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
            title="Ù…Ù†ØªØ¬ Ø¬Ø¯ÙŠØ¯"
          >
            <PlusCircle className="w-5 h-5" />
            {canQuickCreateProduct && (
              <span className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 px-2 py-1 bg-gray-900 text-white text-xs rounded whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none z-10">
                Ù…Ù†ØªØ¬ Ø¬Ø¯ÙŠØ¯
              </span>
            )}
          </button>

          {/* Custom Item */}
          <button
            onClick={() => setShowCustomItem(true)}
            className="relative group p-2.5 rounded-lg border-2 bg-secondary-600 text-white border-secondary-500 hover:bg-secondary-700 transition-all"
            title="Ù…Ù†ØªØ¬ Ù…Ø®ØµØµ"
          >
            <FileText className="w-5 h-5" />
            <span className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 px-2 py-1 bg-gray-900 text-white text-xs rounded whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none z-10">
              Ù…Ù†ØªØ¬ Ù…Ø®ØµØµ
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
              <option value="">ðŸª Ø§Ù„ÙƒÙ„</option>
              {categories.map((category) => (
                <option key={category.id} value={category.id}>
                  {category.imageUrl || "ðŸ“"} {category.name}
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
              title="Ø§Ù„Ù…ØªØ§Ø­ ÙÙ‚Ø·"
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
                  toast.error("Ù„ÙŠØ³ Ù„Ø¯ÙŠÙƒ ØµÙ„Ø§Ø­ÙŠØ© Ø¥Ø¶Ø§ÙØ© Ù…Ù†ØªØ¬ Ø³Ø±ÙŠØ¹");
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
              <span>Ù…Ù†ØªØ¬ Ø¬Ø¯ÙŠØ¯</span>
            </button>

            {/* Custom Item */}
            <button
              onClick={() => setShowCustomItem(true)}
              className="flex-1 flex items-center justify-center gap-2 px-3 py-2 rounded-lg text-sm font-medium border-2 bg-secondary-600 text-white border-secondary-500"
            >
              <FileText className="w-4 h-4" />
              <span>Ù…Ù†ØªØ¬ Ù…Ø®ØµØµ</span>
            </button>
          </div>
        </div>

        {/* Products Grid */}
        <div className="flex-1 overflow-y-auto scrollbar-thin min-h-0">
          <ProductGrid
            products={filteredProducts}
            categories={categories}
            onAddProduct={(product) => handleAddProductToCart(product)}
            stockByProductId={stockByProductId}
            hasInventorySnapshot={hasInventorySnapshot}
            isInventoryLoading={isInventoryLoading}
          />
        </div>
      </div>

      {/* Cart Section - Desktop */}
      <div className="hidden min-h-0 w-[440px] shrink-0 flex-col border-s-2 border-gray-100 bg-white lg:flex xl:w-[470px] 2xl:w-[500px]">
        <Cart
          onCheckout={() => setShowPayment(true)}
          selectedCustomer={selectedCustomer}
          onCustomerSelect={setSelectedCustomer}
          orderType={orderType}
          onOrderTypeChange={handleOrderTypeChange}
          selectedTable={selectedTable}
          onTableSelectClick={() => setShowTableModal(true)}
          deliveryAddress={deliveryAddress}
          onDeliveryAddressChange={setDeliveryAddress}
          deliveryFee={deliveryFee}
          onDeliveryFeeChange={setDeliveryFee}
          deliveryNotes={deliveryNotes}
          onDeliveryNotesChange={setDeliveryNotes}
          orderSource={orderSource}
          onOrderSourceChange={setOrderSource}
          externalOrderNumber={externalOrderNumber}
          onExternalOrderNumberChange={setExternalOrderNumber}
          orderNotes={orderNotes}
          onOrderNotesChange={setOrderNotes}
          onSavedNotesClick={() => setShowSavedNotesModal(true)}
        />
      </div>

      {/* Cart Section - Mobile */}
      {showMobileCart && (
        <div className="lg:hidden fixed inset-0 z-40">
          <div
            className="absolute inset-0 bg-black/50"
            onClick={() => setShowMobileCart(false)}
          />
          <div className="absolute bottom-0 right-0 top-0 flex w-[92%] max-w-[28rem] flex-col bg-white animate-slide-in-right">
            <Cart
              onCheckout={() => {
                setShowMobileCart(false);
                setShowPayment(true);
              }}
              selectedCustomer={selectedCustomer}
              onCustomerSelect={setSelectedCustomer}
              orderType={orderType}
              onOrderTypeChange={handleOrderTypeChange}
              selectedTable={selectedTable}
              onTableSelectClick={() => setShowTableModal(true)}
              deliveryAddress={deliveryAddress}
              onDeliveryAddressChange={setDeliveryAddress}
              deliveryFee={deliveryFee}
              onDeliveryFeeChange={setDeliveryFee}
              deliveryNotes={deliveryNotes}
              onDeliveryNotesChange={setDeliveryNotes}
              orderSource={orderSource}
              onOrderSourceChange={setOrderSource}
              externalOrderNumber={externalOrderNumber}
              onExternalOrderNumberChange={setExternalOrderNumber}
              orderNotes={orderNotes}
              onOrderNotesChange={setOrderNotes}
              onSavedNotesClick={() => setShowSavedNotesModal(true)}
            />
          </div>
        </div>
      )}

      {/* Payment Modal */}
      {showPayment && (
        <PaymentModal
          onClose={() => setShowPayment(false)}
          selectedCustomer={selectedCustomer}
          orderType={orderType}
          tableId={orderType === "DineIn" ? selectedTable?.id : undefined}
          deliveryAddress={deliveryAddress}
          deliveryFee={deliveryFee}
          deliveryNotes={deliveryNotes}
          orderSource={orderType === "Delivery" ? orderSource : "POS"}
          externalOrderNumber={
            orderType === "Delivery" ? externalOrderNumber : undefined
          }
          orderNotes={orderNotes}
          onOrderComplete={resetRestaurantOrderState}
        />
      )}

      {showTableModal && (
        <TableSelectionModal
          tables={restaurantTables}
          selectedTableId={selectedTable?.id}
          onSelect={(table) => {
            setSelectedTable(table);
            setShowTableModal(false);
          }}
          onClose={() => setShowTableModal(false)}
        />
      )}

      {showSavedNotesModal && (
        <SavedNotesModal
          notes={savedNotes}
          onApply={appendOrderNote}
          onClose={() => setShowSavedNotesModal(false)}
        />
      )}

      {/* Quick Create Product Modal */}
      {showQuickCreate && canQuickCreateProduct && (
        <ProductQuickCreateModal
          onClose={() => setShowQuickCreate(false)}
          onSuccess={(productId) => {
            toast.success("ØªÙ… Ø¥Ø¶Ø§ÙØ© Ø§Ù„Ù…Ù†ØªØ¬ Ø¨Ù†Ø¬Ø§Ø­");
          }}
        />
      )}

      {/* Custom Item Modal */}
      {showCustomItem && (
        <CustomItemModal
          onClose={() => setShowCustomItem(false)}
          onSuccess={(item) => {
            const customProduct: Product = {
              id: -Date.now(),
              name: item.name,
              price: item.unitPrice,
              suggestedPrice: item.unitPrice,
              taxRate: item.taxRate ?? taxRate,
              taxInclusive: false,
              categoryId: 0,
              isActive: true,
              type: ProductType.Service,
              unit: UnitOfMeasure.Piece,
              trackInventory: false,
              isBatchTracked: false,
              createdAt: new Date().toISOString(),
            };

            addItem(customProduct, item.quantity ?? 1);
            toast.success(`ØªÙ…Øª Ø¥Ø¶Ø§ÙØ©: ${item.name}`);
          }}
        />
      )}

      {/* Batch Selection Modal */}
      {showBatchModal && selectedProductForBatch && currentBranch && (
        <BatchSelectionModalWithData
          product={selectedProductForBatch}
          branchId={currentBranch.id}
          onClose={() => {
            setShowBatchModal(false);
            setSelectedProductForBatch(null);
          }}
          onSelectBatch={(batch) => {
            const productForCart = hasInventorySnapshot
              ? ({
                  ...selectedProductForBatch,
                  branchInventoryQuantity: batch.quantity,
                } as Product)
              : selectedProductForBatch;

            addItem(productForCart, pendingBatchQuantity, {
              batchId: batch.id,
              batchNumber: batch.batchNumber,
              expiryDate: batch.expiryDate,
              sellingPrice: batch.sellingPrice,
              batchQuantity: batch.quantity,
            });

            toast.success(`ØªÙ…Øª Ø§Ù„Ø¥Ø¶Ø§ÙØ©: ${selectedProductForBatch.name} Ù…Ù† ${batch.batchNumber || "Ø¨Ø¯ÙˆÙ† Ø±Ù‚Ù… Ø¯ÙØ¹Ø©"}`);
            setShowBatchModal(false);
            setSelectedProductForBatch(null);
          }}
        />
      )}
    </div>
  );
};

// Helper component to fetch batches and show modal
const BatchSelectionModalWithData = ({
  product,
  branchId,
  selectedBatchId,
  onClose,
  onSelectBatch,
}: {
  product: Product;
  branchId: number;
  selectedBatchId?: number;
  onClose: () => void;
  onSelectBatch: (batch: ProductBatch) => void;
}) => {
  const { data: batchesResponse, isLoading, isSuccess } = useGetAvailableBatchesQuery({
    productId: product.id,
    branchId,
  });

  const batches = batchesResponse?.data ?? [];
  const firstBatch = batches[0];
  const autoSelectedBatchIdRef = useRef<number | null>(null);

  useEffect(() => {
    if (isLoading || !firstBatch) return;
    if (autoSelectedBatchIdRef.current === firstBatch.id) return;

    autoSelectedBatchIdRef.current = firstBatch.id;
    onSelectBatch(firstBatch);
    onClose();
  }, [firstBatch, isLoading, onClose, onSelectBatch]);

  useEffect(() => {
    if (isLoading || !isSuccess || batches.length > 0) return;
    toast.error(`Ù„Ø§ ØªÙˆØ¬Ø¯ Ø¯ÙØ¹Ø§Øª Ù…ØªØ§Ø­Ø© Ù„Ù„Ø¨ÙŠØ¹ Ù„Ù„Ù…Ù†ØªØ¬: ${product.name}`);
    onClose();
  }, [batches.length, isLoading, isSuccess, onClose, product.name]);

  if (isLoading) {
    return null;
  }

  if (batches.length > 0) {
    return isLoading ? <Loading /> : null;
  }

  return (
    <BatchSelectionModal
      isOpen={true}
      onClose={onClose}
      productName={product.name}
      batches={batches}
      selectedBatchId={selectedBatchId}
      onSelectBatch={onSelectBatch}
    />
  );
};

export default POSPage;
