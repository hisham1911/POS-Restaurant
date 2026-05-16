import {
  useState,
  useRef,
  useEffect,
  useCallback,
  useMemo,
  useDeferredValue,
  type ReactNode,
} from "react";
import { useProducts, useCategories } from "@/hooks/useProducts";
import { useCart } from "@/hooks/useCart";
import { useShift } from "@/hooks/useShift";
import { useOrders } from "@/hooks/useOrders";
import { usePOSShortcuts } from "@/hooks/usePOSShortcuts";
import { useGetShiftWarningsQuery } from "@/api/shiftsApi";
import { useGetBranchInventoryQuery } from "@/api/inventoryApi";
import { useGetAvailableBatchesQuery } from "@/api/productBatchApi";
import { useGetActiveWalletsQuery } from "@/api/walletApi";
import { useGetRestaurantTablesQuery } from "@/api/restaurantTablesApi";
import { useGetSavedOrderNotesQuery } from "@/api/savedOrderNotesApi";
import { usePOSMode } from "@/hooks/usePOSMode";
import { Customer } from "@/types/customer.types";
import { OrderSource, OrderType, PaymentMethod } from "@/types/order.types";
import type { RestaurantTable } from "@/types/restaurant.types";
import { Product, ProductType, UnitOfMeasure } from "@/types/product.types";
import { ProductBatch } from "@/types/productBatch.types";
import { toast } from "sonner";
import { Link, Navigate, useLocation, useNavigate } from "react-router-dom";
import {
  ScanBarcode,
  PackageCheck,
  AlertCircle,
  PlusCircle,
  FileText,
  ShoppingCart,
  User,
  CreditCard,
  Receipt,
  Trash2,
  Tag,
  Phone,
  Star,
  Banknote,
  Building2,
  Check,
  X as XIcon,
  Plus,
  Package,
  Store,
  Clock3,
  Wallet,
  Armchair,
  MessageSquare,
  type LucideIcon,
} from "lucide-react";
import clsx from "clsx";
import {
  buildBranchInventoryStockMap,
  getProductAvailableStock,
  getProductCurrentStock,
} from "@/utils/productStock";
import { ProductListView } from "@/components/pos/ProductListView";
import { CategoryChips } from "@/components/pos/CategoryChips";
import { CartItemComponent } from "@/components/pos/CartItem";
import { CustomerQuickCreateModal } from "@/components/pos/CustomerQuickCreateModal";
import { ProductQuickCreateModal } from "@/components/pos/ProductQuickCreateModal";
import { CustomItemModal } from "@/components/pos/CustomItemModal";
import { BatchSelectionModal } from "@/components/pos/BatchSelectionModal";
import { TableSelectionModal } from "@/components/pos/TableSelectionModal";
import { SavedNotesModal } from "@/components/pos/SavedNotesModal";
import { LowStockAlert } from "@/components/pos/LowStockAlert";
import { ShiftWarningBanner } from "@/components/shifts";
import { BatchExpiryAlertBanner } from "@/components/inventory";
import { Loading } from "@/components/common/Loading";
import { Button } from "@/components/common/Button";
import { formatCurrency } from "@/utils/formatters";
import { useLazyGetCustomerByPhoneQuery } from "@/api/customersApi";
import { usePermission } from "@/hooks/usePermission";
import { useAppSelector } from "@/store/hooks";
import { selectCurrentBranch } from "@/store/slices/branchSlice";
import { selectAllowNegativeStock } from "@/store/slices/cartSlice";

type WorkspaceTab = "cart" | "customer" | "payment" | "summary";

interface SummaryLineProps {
  label: string;
  value: string;
  icon?: ReactNode;
  valueClassName?: string;
}

interface SurfaceCardProps {
  children: ReactNode;
  className?: string;
}

interface WorkspaceTabButtonProps {
  icon: LucideIcon;
  label: string;
  active: boolean;
  disabled?: boolean;
  indicator?: ReactNode;
  onClick: () => void;
}

interface POSWorkspaceLocationState {
  selectedTable?: RestaurantTable;
}

const SurfaceCard = ({ children, className }: SurfaceCardProps) => (
  <div
    className={clsx(
      "rounded-2xl border border-gray-200 bg-white p-4 shadow-sm",
      className,
    )}
  >
    {children}
  </div>
);

const SummaryLine = ({
  label,
  value,
  icon,
  valueClassName,
}: SummaryLineProps) => (
  <div className="flex items-center justify-between gap-3 text-sm">
    <div className="flex items-center gap-2 text-slate-600">
      {icon}
      <span>{label}</span>
    </div>
    <span className={clsx("font-semibold text-slate-900", valueClassName)}>
      {value}
    </span>
  </div>
);

const WorkspaceTabButton = ({
  icon: Icon,
  label,
  active,
  disabled,
  indicator,
  onClick,
}: WorkspaceTabButtonProps) => (
  <button
    type="button"
    onClick={onClick}
    disabled={disabled}
    className={clsx(
      "relative flex min-h-[52px] flex-col items-center justify-center gap-1 rounded-xl border px-2 py-2 text-xs font-semibold transition-all",
      active
        ? "border-primary-600 bg-primary-600 text-white"
        : "border-gray-200 bg-white text-gray-600 hover:bg-gray-50",
      disabled && "cursor-not-allowed opacity-45",
    )}
  >
    <Icon className="h-4.5 w-4.5" />
    <span>{label}</span>
    {indicator && (
      <span
        className={clsx(
          "absolute -top-1 inline-flex min-w-[1.25rem] items-center justify-center rounded-full px-1 text-[10px] font-bold",
          active
            ? "bg-white text-slate-900"
            : "bg-primary-600 text-white shadow-sm",
        )}
      >
        {indicator}
      </span>
    )}
  </button>
);

const formatShiftDuration = (hours: number, minutes: number) => {
  if (hours > 0) {
    return `${hours}س ${minutes}د`;
  }

  return `${minutes}د`;
};

export const POSWorkspacePage = () => {
  const navigate = useNavigate();
  const { mode } = usePOSMode();
  const shouldRedirectToCashier = mode === "cashier";
  const location = useLocation();

  const [selectedCategory, setSelectedCategory] = useState<number | null>(null);
  const [showAvailableOnly, setShowAvailableOnly] = useState(false);
  const [showQuickCreate, setShowQuickCreate] = useState(false);
  const [showCustomItem, setShowCustomItem] = useState(false);
  const [selectedCustomer, setSelectedCustomer] = useState<Customer | null>(
    null,
  );
  const [searchInput, setSearchInput] = useState("");
  const [showCatalog, setShowCatalog] = useState(false);
  const [activeTab, setActiveTab] = useState<WorkspaceTab>("cart");
  const [showCustomerCreateModal, setShowCustomerCreateModal] = useState(false);
  const [customerPhone, setCustomerPhone] = useState("");
  const [selectedPaymentMethod, setSelectedPaymentMethod] =
    useState<string>("Cash");
  const [transactionReference, setTransactionReference] = useState("");
  const [amountPaid, setAmountPaid] = useState<string>("");
  const [allowPartialPayment, setAllowPartialPayment] = useState(false);
  const [printKitchenTicket, setPrintKitchenTicket] = useState(true);
  const [showPaymentError, setShowPaymentError] = useState(false);
  const [showDiscountInput, setShowDiscountInput] = useState(false);
  const [discountInputValue, setDiscountInputValue] = useState("");
  const [dismissedWarning, setDismissedWarning] = useState(false);
  const [discountInputType, setDiscountInputType] = useState<
    "Percentage" | "Fixed"
  >("Percentage");
  const [orderType, setOrderType] = useState<OrderType>("DineIn");
  const [deliveryAddress, setDeliveryAddress] = useState("");
  const [deliveryFee, setDeliveryFee] = useState("");
  const [deliveryNotes, setDeliveryNotes] = useState("");
  const [selectedTable, setSelectedTable] = useState<RestaurantTable | null>(
    null,
  );
  const [showTableModal, setShowTableModal] = useState(false);
  const [showSavedNotesModal, setShowSavedNotesModal] = useState(false);
  const [orderNotes, setOrderNotes] = useState("");
  const [orderSource, setOrderSource] = useState<OrderSource>("POS");
  const [externalOrderNumber, setExternalOrderNumber] = useState("");

  // Batch selection state
  const [showBatchModal, setShowBatchModal] = useState(false);
  const [selectedProductForBatch, setSelectedProductForBatch] = useState<Product | null>(null);
  const [pendingBatchQuantity, setPendingBatchQuantity] = useState(1);

  const searchInputRef = useRef<HTMLInputElement>(null);
  const customerPhoneRef = useRef<HTMLInputElement>(null);

  const { products, isLoading } = useProducts();
  const { categories } = useCategories();
  const {
    items,
    itemsCount,
    subtotal,
    discountAmount,
    discountType,
    discountValue,
    taxAmount,
    total,
    taxRate,
    isTaxEnabled,
    serviceChargeAmount,
    serviceChargeRate,
    addItem,
    clearCart,
    applyDiscount,
    removeDiscount,
    canManageDiscounts,
  } = useCart();
  const {
    hasActiveShift,
    isLoading: isLoadingShift,
    currentShift,
  } = useShift();
  const {
    createOrder,
    completeOrder,
    isCreating,
    isCompleting,
  } = useOrders();
  const currentBranch = useAppSelector(selectCurrentBranch);
  const allowNegativeStock = useAppSelector(selectAllowNegativeStock);
  const { hasPermission } = usePermission();
  const canQuickCreateProduct =
    hasPermission("ProductsCreateFromPOS") || hasPermission("ProductsManage");
  const canSellOnCredit = hasPermission("PosCreditSale");
  const { data: branchInventory, isLoading: isInventoryLoading } =
    useGetBranchInventoryQuery(currentBranch?.id ?? 0, {
      skip: !currentBranch?.id,
    });
  const stockByProductId = useMemo(
    () => buildBranchInventoryStockMap(branchInventory),
    [branchInventory],
  );
  const hasInventorySnapshot = Array.isArray(branchInventory);
  const { data: walletsData } = useGetActiveWalletsQuery();
  const { data: tablesData } = useGetRestaurantTablesQuery(
    currentBranch?.id ?? 0,
    {
      skip: !currentBranch?.id,
    },
  );
  const { data: savedNotesData } = useGetSavedOrderNotesQuery(
    currentBranch?.id ?? 0,
    {
      skip: !currentBranch?.id,
    },
  );
  const deferredSearchInput = useDeferredValue(searchInput);

  const [
    searchCustomer,
    { data: searchResult, isFetching: isSearchingCustomer },
  ] = useLazyGetCustomerByPhoneQuery();
  const { data: warningsData } = useGetShiftWarningsQuery(undefined, {
    pollingInterval: 10 * 60 * 1000,
    skip: !hasActiveShift,
  });

  const shiftWarning = warningsData?.data;
  const restaurantTables = tablesData?.data ?? [];
  const savedOrderNotes = savedNotesData?.data ?? [];
  const deliveryFeeAmount =
    orderType === "Delivery"
      ? parseFloat(deliveryFee || "0") || 0
      : 0;
  const checkoutTotal = total + deliveryFeeAmount;
  const paymentTotal = checkoutTotal;
  const displayWorkspaceTotal =
    activeTab === "payment" ? paymentTotal : checkoutTotal;

  useEffect(() => {
    setDismissedWarning(false);
  }, [shiftWarning?.message]);

  const openPaymentWorkspace = useCallback(() => {
    if (paymentTotal <= 0) {
      return;
    }

    setActiveTab("payment");
  }, [paymentTotal]);

  usePOSShortcuts({
    onCheckout: openPaymentWorkspace,
    onSearch: () => searchInputRef.current?.focus(),
  });

  useEffect(() => {
    searchInputRef.current?.focus();
  }, []);

  useEffect(() => {
    if (!allowPartialPayment) {
      setAmountPaid(paymentTotal.toFixed(2));
    }
  }, [allowPartialPayment, paymentTotal]);

  useEffect(() => {
    if (activeTab === "customer" && !selectedCustomer) {
      customerPhoneRef.current?.focus();
    }
  }, [activeTab, selectedCustomer]);

  useEffect(() => {
    const state = location.state as POSWorkspaceLocationState | null;

    if (!state?.selectedTable) return;

    setOrderType("DineIn");
    setSelectedTable(state.selectedTable);
  }, [location.state]);

  useEffect(() => {
    if (
      orderType === "Delivery" &&
      selectedCustomer?.address &&
      !deliveryAddress.trim()
    ) {
      setDeliveryAddress(selectedCustomer.address);
    }
  }, [deliveryAddress, orderType, selectedCustomer?.address]);

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
        toast.error(`المنتج غير متاح الآن: ${product.name}`);
        return false;
      }

      if (isOutOfStock || !canAddMore) {
        toast.error(`لا يمكن إضافة ${product.name} لعدم توفر مخزون كافٍ`);
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
        toast.success(`تمت الإضافة: ${product.name}`);
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

  const handleSearchSubmit = useCallback(
    (value: string) => {
      const trimmedValue = value.trim();
      if (!trimmedValue) return;

      const foundProduct = products.find(
        (product) =>
          (product.barcode &&
            product.barcode.toLowerCase() === trimmedValue.toLowerCase()) ||
          (product.sku &&
            product.sku.toLowerCase() === trimmedValue.toLowerCase()) ||
          product.name.toLowerCase() === trimmedValue.toLowerCase(),
      );

      if (foundProduct) {
        const added = handleAddProductToCart(foundProduct, { showToast: true });
        if (added) {
          setSearchInput("");
          setShowCatalog(false);
          searchInputRef.current?.focus();
        }
      } else {
        toast.error(`لم يتم العثور على منتج: ${trimmedValue}`);
      }
    },
    [handleAddProductToCart, products],
  );

  const handleSearchKeyDown = (
    event: React.KeyboardEvent<HTMLInputElement>,
  ) => {
    if (event.key === "Enter") {
      event.preventDefault();
      handleSearchSubmit(searchInput);
    }
  };

  useEffect(() => {
    const timer = setTimeout(() => {
      if (customerPhone.length >= 8) {
        searchCustomer(customerPhone);
      }
    }, 300);

    return () => clearTimeout(timer);
  }, [customerPhone, searchCustomer]);

  useEffect(() => {
    if (selectedPaymentMethod === "Cash") {
      setTransactionReference("");
      return;
    }

    setAllowPartialPayment(false);
    setAmountPaid(paymentTotal.toFixed(2));
  }, [selectedPaymentMethod, paymentTotal]);

  const handleSelectCustomer = (customer: Customer) => {
    setSelectedCustomer(customer);
    setCustomerPhone("");
    toast.success(`تم اختيار العميل: ${customer.name || customer.phone}`);
  };

  const handleClearCustomer = () => {
    setSelectedCustomer(null);
    setCustomerPhone("");
  };

  const handleQuickAmount = (amount: number) => {
    setAmountPaid(amount.toFixed(2));
  };

  const resetDiscountEditor = () => {
    setShowDiscountInput(false);
    setDiscountInputValue("");
  };

  const handleApplyDiscount = () => {
    if (!canManageDiscounts) {
      toast.error("ليس لديك صلاحية تطبيق أو تعديل الخصومات");
      return;
    }

    const parsedValue = parseFloat(discountInputValue);
    if (Number.isNaN(parsedValue) || parsedValue <= 0) {
      return;
    }

    if (discountInputType === "Percentage") {
      if (parsedValue > 100) {
        toast.error("النسبة يجب أن تكون بين 0 و 100");
        return;
      }

      applyDiscount("Percentage", parsedValue);
      toast.success(`تم تطبيق خصم ${parsedValue}%`);
      resetDiscountEditor();
      return;
    }

    applyDiscount("Fixed", parsedValue);
    toast.success(`تم تطبيق خصم ${formatCurrency(parsedValue)}`);
    resetDiscountEditor();
  };

  const handleWorkspaceTabChange = (tab: WorkspaceTab) => {
    if ((tab === "payment" || tab === "summary") && paymentTotal <= 0) {
      return;
    }

    setActiveTab(tab);
  };

  const handleCategorySelect = (categoryId: number | null) => {
    setSelectedCategory(categoryId);
    if (categoryId !== null) {
      setShowCatalog(true);
      return;
    }

    if (!searchInput.trim()) {
      setShowCatalog(false);
    }
  };

  const handleResetDiscovery = () => {
    setSearchInput("");
    setSelectedCategory(null);
    setShowCatalog(false);
  };

  const handleOpenWorkspaceSheet = (tab: WorkspaceTab = "cart") => {
    if ((tab === "payment" || tab === "summary") && paymentTotal <= 0) {
      return;
    }

    setActiveTab(tab);
  };

  const handleOpenQuickCreate = () => {
    if (!canQuickCreateProduct) {
      toast.error("ليس لديك صلاحية إضافة منتج سريع");
      return;
    }

    setShowQuickCreate(true);
  };

  const handleOrderTypeChange = (nextOrderType: OrderType) => {
    if (nextOrderType === "Return") return;

    setOrderType(nextOrderType);

    if (nextOrderType !== "DineIn") {
      setSelectedTable(null);
      setShowTableModal(false);
    }

    if (nextOrderType !== "Delivery") {
      setOrderSource("POS");
      setExternalOrderNumber("");
    }
  };

  const appendOrderNote = (text: string) => {
    const trimmed = text.trim();
    if (!trimmed) return;

    setOrderNotes((current) =>
      current.trim() ? `${current.trim()}\n${trimmed}` : trimmed,
    );
  };

  const resetOrderWorkspace = () => {
    clearCart();
    setSelectedCustomer(null);
    setCustomerPhone("");
    setSelectedPaymentMethod("Cash");
    setTransactionReference("");
    setAmountPaid("");
    setAllowPartialPayment(false);
    setOrderType("DineIn");
    setDeliveryAddress("");
    setDeliveryFee("");
    setDeliveryNotes("");
    setSelectedTable(null);
    setOrderNotes("");
    setOrderSource("POS");
    setExternalOrderNumber("");
  };

  const validateOrderContext = () => {
    if (orderType === "DineIn" && !selectedTable) {
      setShowTableModal(true);
      toast.error("اختر طاولة قبل إنشاء طلب الصالة");
      return false;
    }

    if (
      orderType === "Delivery" &&
      !deliveryAddress.trim()
    ) {
      toast.error("عنوان التوصيل مطلوب قبل إنشاء طلب دليفري");
      setActiveTab("summary");
      return false;
    }

    return true;
  };

  const createOrderOptions = () => ({
    tableId: orderType === "DineIn" ? selectedTable?.id : undefined,
    orderSource: orderType === "Delivery" ? orderSource : "POS",
    externalOrderNumber:
      orderType === "Delivery" &&
      orderSource !== "POS" &&
      externalOrderNumber.trim()
        ? externalOrderNumber.trim()
        : undefined,
    notes: orderNotes.trim() || undefined,
  });

  const handleCompletePayment = async () => {
    if (items.length === 0) {
      return;
    }

    if (!validateOrderContext()) {
      return;
    }

    const numericAmount = parseFloat(amountPaid) || 0;
    const amountDue = paymentTotal - numericAmount;

    if (numericAmount < paymentTotal && !canSellOnCredit) {
      toast.error("ليس لديك صلاحية البيع الآجل أو الدفع الجزئي");
      return;
    }

    if (numericAmount < paymentTotal && !allowPartialPayment) {
      setShowPaymentError(true);
      setTimeout(() => setShowPaymentError(false), 500);
      toast.error("المبلغ المدفوع أقل من الإجمالي");
      return;
    }

    if (numericAmount < paymentTotal && !selectedCustomer) {
      toast.error("البيع الآجل يتطلب ربط عميل بالطلب");
      return;
    }

    if (
      numericAmount < paymentTotal &&
      selectedCustomer &&
      !selectedCustomer.isActive
    ) {
      toast.error("العميل غير نشط - لا يمكن البيع الآجل");
      return;
    }

    if (selectedCustomer && selectedCustomer.creditLimit > 0) {
      const availableCredit =
        selectedCustomer.creditLimit - selectedCustomer.totalDue;
      const creditLimitExceeded = amountDue > availableCredit;

      if (numericAmount < paymentTotal && creditLimitExceeded) {
        toast.error(
          `تجاوز حد الائتمان. المتاح بعد رصيد العميل عبر كل الفروع: ${formatCurrency(availableCredit)} ج.م، المطلوب آجلاً: ${formatCurrency(amountDue)} ج.م`,
          { duration: 5000 },
        );
        return;

        toast.error(
          `تجاوز حد الائتمان. المتاح: ${formatCurrency(availableCredit)} ج.م، المطلوب: ${formatCurrency(amountDue)} ج.م`,
          { duration: 5000 },
        );
        return;
      }
    }

    if (selectedPaymentMethod !== "Cash" && !transactionReference.trim()) {
      toast.error("رقم المعاملة مطلوب لطرق الدفع غير النقدية");
      return;
    }

    try {
      const wasDeliveryOrder = orderType === "Delivery";
      const order = await createOrder(
        selectedCustomer?.id,
        orderType,
        orderType === "Delivery" ? deliveryAddress || undefined : undefined,
        orderType === "Delivery" ? deliveryFeeAmount : 0,
        orderType === "Delivery" ? deliveryNotes || undefined : undefined,
        createOrderOptions(),
      );

      if (!order) {
        return;
      }

      const orderId = order.id;

      const completedOrder = await completeOrder(orderId, {
        payments: [
          {
            method: (selectedWallet ? selectedWallet.type : selectedPaymentMethod) as PaymentMethod,
            amount: numericAmount,
            reference:
              selectedPaymentMethod !== "Cash"
                ? transactionReference.trim()
                : undefined,
            walletId: selectedWallet?.id,
          },
        ],
      }, {
        printKitchenTicket,
      });

      if (!completedOrder) {
        // ✅ فشل إكمال الطلب - نلغي المسودة تلقائيًا
        return;
      }

      const changeAmount = completedOrder.changeAmount ?? 0;
      const completedAmountDue = completedOrder.amountDue ?? 0;

      // ✅ CRITICAL: Change tab FIRST to hide payment UI immediately
      setActiveTab("cart");

      // ✅ Then clear cart and reset state in next tick
      // This ensures tab change renders before cleanup
      setTimeout(() => {
        resetOrderWorkspace();

        // Show success toasts after UI is cleared
        if (changeAmount > 0) {
          toast.success(
            `تم إتمام الدفع! الباقي: ${formatCurrency(changeAmount)}`,
          );
        } else if (completedAmountDue > 0) {
          toast.success(
            `تم إتمام البيع الآجل! المبلغ المستحق: ${formatCurrency(completedAmountDue)}`,
          );
        } else {
          toast.success("تم إتمام الدفع بنجاح!");
        }

        if (wasDeliveryOrder) {
          toast.info(
            "تم إنشاء طلب التوصيل — يمكن تعيين المندوب من شاشة إدارة التوصيل",
          );
        }
      }, 0);
    } catch {
      toast.error("حدث خطأ غير متوقع");
    }
  };

  const filteredProducts = useMemo(() => {
    let nextProducts = products.filter(
      (product) => product.type !== ProductType.RawMaterial,
    );

    if (deferredSearchInput.trim()) {
      const searchLower = deferredSearchInput.toLowerCase().trim();
      nextProducts = nextProducts.filter(
        (product) =>
          product.name.toLowerCase().includes(searchLower) ||
          (product.barcode &&
            product.barcode.toLowerCase().includes(searchLower)) ||
          (product.sku && product.sku.toLowerCase().includes(searchLower)),
      );
    }

    if (selectedCategory) {
      nextProducts = nextProducts.filter(
        (product) => product.categoryId === selectedCategory,
      );
    }

    if (showAvailableOnly) {
      nextProducts = nextProducts.filter((product) => {
        if (product.type === ProductType.Manufactured || !product.trackInventory) {
          return true;
        }
        if (!hasInventorySnapshot) return true;
        return getProductCurrentStock(product, stockByProductId) > 0;
      });
    }

    return nextProducts;
  }, [
    deferredSearchInput,
    hasInventorySnapshot,
    products,
    selectedCategory,
    showAvailableOnly,
    stockByProductId,
  ]);

  const showProductResults =
    showCatalog ||
    Boolean(deferredSearchInput.trim()) ||
    selectedCategory !== null;
  const visibleSearchResults = showCatalog
    ? filteredProducts
    : filteredProducts.slice(0, 12);

  if (shouldRedirectToCashier) {
    return <Navigate to="/pos" replace />;
  }

  if (isLoading || isLoadingShift) {
    return (
      <div className="flex h-full items-center justify-center bg-gray-50">
        <Loading />
      </div>
    );
  }

  if (!hasActiveShift) {
    return (
      <div className="flex h-full items-center justify-center bg-[linear-gradient(180deg,#eef4ff_0%,#f8fafc_100%)] p-4">
        <div className="w-full max-w-md rounded-[2rem] border border-warning-200 bg-white p-8 text-center shadow-[0_24px_60px_rgba(15,23,42,0.08)]">
          <div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-warning-100">
            <AlertCircle className="h-8 w-8 text-warning-600" />
          </div>
          <h2 className="mb-2 text-2xl font-black text-slate-900">
            لا توجد وردية مفتوحة
          </h2>
          <p className="mb-6 text-sm leading-7 text-slate-600">
            يجب فتح وردية قبل البدء في البيع. انتقل إلى صفحة الورديات لفتح وردية
            جديدة ثم عد مباشرة إلى مساحة العمل.
          </p>
          <Link
            to="/shift"
            className="inline-flex min-h-[48px] items-center justify-center rounded-2xl bg-primary-600 px-6 py-3 font-semibold text-white transition-colors hover:bg-primary-700"
          >
            الذهاب إلى الورديات
          </Link>
        </div>
      </div>
    );
  }

  const numericAmount = parseFloat(amountPaid) || 0;
  const change = numericAmount - paymentTotal;
  const amountDue = paymentTotal - numericAmount;
  const availableCredit = selectedCustomer
    ? selectedCustomer.creditLimit - selectedCustomer.totalDue
    : 0;
  const canTakeCredit =
    selectedCustomer &&
    selectedCustomer.isActive &&
    (selectedCustomer.creditLimit === 0 || amountDue <= availableCredit);
  const creditLimitExceeded =
    selectedCustomer &&
    selectedCustomer.creditLimit > 0 &&
    amountDue > availableCredit;
  const shiftOpenedAt = currentShift
    ? new Date(currentShift.openedAt).toLocaleTimeString("ar-EG", {
        hour: "2-digit",
        minute: "2-digit",
      })
    : null;
  const shiftDuration = currentShift
    ? formatShiftDuration(
        currentShift.durationHours,
        currentShift.durationMinutes,
      )
    : null;

  const wallets = walletsData?.data ?? [];

  const paymentMethods: Array<{
    id: string;
    label: string;
    icon: ReactNode;
    walletId?: number;
  }> = [
    { id: "Cash", label: "نقدي", icon: <Banknote className="h-5 w-5" /> },
    ...wallets.map((wallet) => ({
      id: `wallet-${wallet.id}`,
      label: wallet.name,
      icon: <Wallet className="h-5 w-5" />,
      walletId: wallet.id,
    })),
  ];

  const quickAmounts = [50, 100, 200, 500];

  const workspaceTabs: Array<{
    id: WorkspaceTab;
    label: string;
    icon: LucideIcon;
    disabled?: boolean;
    indicator?: ReactNode;
  }> = [
    {
      id: "cart",
      label: "السلة",
      icon: ShoppingCart,
      indicator: itemsCount > 0 ? itemsCount : undefined,
    },
    {
      id: "customer",
      label: "العميل",
      icon: User,
      indicator: selectedCustomer ? (
        <span className="h-2 w-2 rounded-full bg-success-500" />
      ) : undefined,
    },
    {
      id: "payment",
      label: "الدفع",
      icon: CreditCard,
      disabled: paymentTotal <= 0,
    },
    {
      id: "summary",
      label: "الملخص",
      icon: Receipt,
      disabled: paymentTotal <= 0,
    },
  ];

  const selectedPaymentMethodLabel =
    paymentMethods.find((method) => method.id === selectedPaymentMethod)
      ?.label ?? selectedPaymentMethod;
  const requiresTransactionReference = selectedPaymentMethod !== "Cash";
  const selectedWallet =
    selectedPaymentMethod.startsWith("wallet-")
      ? wallets.find((w) => w.id === parseInt(selectedPaymentMethod.replace("wallet-", ""), 10))
      : null;
  const selectedCategoryName =
    selectedCategory !== null
      ? (categories.find((category) => category.id === selectedCategory)
          ?.name ?? null)
      : null;

  const renderCartTab = () => {
    if (items.length === 0) {
      return (
        <div className="flex h-full flex-col items-center justify-center py-12 text-center">
          <div className="mb-4 flex h-20 w-20 items-center justify-center rounded-full bg-slate-100">
            <ShoppingCart className="h-10 w-10 text-slate-400" />
          </div>
          <h3 className="text-lg font-black text-slate-900">
            السلة فارغة
          </h3>
          <p className="mt-2 max-w-xs text-sm leading-7 text-slate-500">
            ابحث عن منتج أو امسح الباركود ثم أضفه، وستظهر العناصر هنا مباشرة.
          </p>
          <button
            type="button"
            onClick={() => searchInputRef.current?.focus()}
            className="mt-4 inline-flex min-h-[44px] items-center justify-center rounded-2xl border border-slate-200 bg-white px-4 py-2 text-sm font-semibold text-slate-700 transition-colors hover:bg-slate-50"
          >
            الرجوع للبحث
          </button>
        </div>
      );
    }

    return (
      <div className="space-y-4">
        <div className="flex items-start justify-between gap-3">
          <div>
            <h3 className="text-lg font-black text-slate-900">
              السلة ({itemsCount})
            </h3>
            <p className="mt-1 text-sm text-slate-500">
              راجع الكميات والإجمالي قبل الدفع.
            </p>
          </div>
          <button
            type="button"
            onClick={clearCart}
            className="inline-flex min-h-[40px] items-center gap-2 rounded-2xl border border-danger-200 bg-danger-50 px-3 py-2 text-sm font-semibold text-danger-600 transition-colors hover:bg-danger-100"
          >
            <Trash2 className="h-4 w-4" />
            إفراغ
          </button>
        </div>

        <div className="space-y-3">
          {items.map((item) => (
            <CartItemComponent key={item.product.id} item={item} />
          ))}
        </div>

        {(canManageDiscounts || discountAmount > 0) && (
          <SurfaceCard>
          <div className="mb-3 flex items-center justify-between">
            <h4 className="text-sm font-bold text-slate-900">الخصم</h4>
            {canManageDiscounts && discountAmount > 0 && (
              <button
                type="button"
                onClick={() => {
                  removeDiscount();
                  toast.success("تم إلغاء الخصم");
                }}
                className="inline-flex items-center gap-1 text-xs font-semibold text-danger-500 hover:text-danger-600"
              >
                <XIcon className="h-4 w-4" />
                إزالة
              </button>
            )}
          </div>

          {discountAmount === 0 && canManageDiscounts ? (
            showDiscountInput ? (
              <div className="space-y-3">
                <div className="grid grid-cols-2 gap-2">
                  <button
                    type="button"
                    onClick={() => setDiscountInputType("Percentage")}
                    className={clsx(
                      "min-h-[44px] rounded-2xl px-3 py-2 text-sm font-semibold transition-all",
                      discountInputType === "Percentage"
                        ? "bg-primary-600 text-white shadow-sm"
                        : "bg-white text-slate-700 ring-1 ring-slate-200 hover:bg-slate-50",
                    )}
                  >
                    نسبة %
                  </button>
                  <button
                    type="button"
                    onClick={() => setDiscountInputType("Fixed")}
                    className={clsx(
                      "min-h-[44px] rounded-2xl px-3 py-2 text-sm font-semibold transition-all",
                      discountInputType === "Fixed"
                        ? "bg-success-600 text-white shadow-sm"
                        : "bg-white text-slate-700 ring-1 ring-slate-200 hover:bg-slate-50",
                    )}
                  >
                    مبلغ ثابت
                  </button>
                </div>

                <input
                  type="number"
                  value={discountInputValue === "0" ? "" : discountInputValue}
                  onChange={(event) =>
                    setDiscountInputValue(event.target.value)
                  }
                  placeholder={
                    discountInputType === "Percentage" ? "0-100" : "0.00"
                  }
                  className="w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-900 outline-none transition focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
                  autoFocus
                />

                <div className="flex gap-2">
                  <button
                    type="button"
                    onClick={handleApplyDiscount}
                    className="inline-flex min-h-[44px] flex-1 items-center justify-center rounded-2xl bg-primary-600 px-4 py-3 text-sm font-semibold text-white transition-colors hover:bg-primary-700"
                  >
                    تطبيق
                  </button>
                  <button
                    type="button"
                    onClick={resetDiscountEditor}
                    className="inline-flex min-h-[44px] items-center justify-center rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm font-semibold text-slate-700 transition-colors hover:bg-slate-50"
                  >
                    إلغاء
                  </button>
                </div>
              </div>
            ) : (
              <button
                type="button"
                onClick={() => setShowDiscountInput(true)}
                className="flex min-h-[52px] w-full items-center justify-center gap-2 rounded-[1.35rem] border border-dashed border-slate-300 bg-white px-4 py-3 text-sm font-semibold text-slate-600 transition-colors hover:border-primary-400 hover:bg-primary-50 hover:text-primary-700"
              >
                <Tag className="h-4 w-4" />
                إضافة خصم على الطلب
              </button>
            )
          ) : (
            <div className="rounded-[1.2rem] border border-success-200 bg-success-50 px-4 py-3">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <p className="text-sm font-bold text-success-700">
                    {discountType === "Percentage"
                      ? `خصم ${discountValue}%`
                      : "خصم ثابت"}
                  </p>
                  <p className="mt-1 text-xs text-success-600">
                    مطبق على إجمالي الطلب الحالي
                  </p>
                </div>
                <span className="text-lg font-black text-success-600">
                  - {formatCurrency(discountAmount)}
                </span>
              </div>
            </div>
          )}
          </SurfaceCard>
        )}

        <SurfaceCard className="space-y-3">
          {discountAmount > 0 && (
            <SummaryLine
              label="خصم الطلب"
              value={`- ${formatCurrency(discountAmount)}`}
              valueClassName="text-success-600"
              icon={<Tag className="h-4 w-4 text-success-500" />}
            />
          )}
          {isTaxEnabled && (
            <SummaryLine
              label={`الضريبة (${taxRate}%)`}
              value={formatCurrency(taxAmount)}
            />
          )}
          {serviceChargeAmount > 0 && (
            <SummaryLine
              label={`رسوم الخدمة (${serviceChargeRate}%)`}
              value={formatCurrency(serviceChargeAmount)}
            />
          )}
          {orderType === "Delivery" && (
            <SummaryLine
              label="رسوم التوصيل"
              value={formatCurrency(deliveryFeeAmount)}
              icon={<Store className="h-4 w-4 text-primary-500" />}
            />
          )}
          <div className="border-t border-slate-200 pt-3">
            <SummaryLine
              label="الإجمالي الحالي"
              value={formatCurrency(checkoutTotal)}
              valueClassName="text-primary-600 text-base font-black"
              icon={<Wallet className="h-4 w-4 text-primary-500" />}
            />
            <p className="mt-2 text-xs text-slate-400">
              * الإجمالي تقديري، ويتم تأكيده نهائيًا عند إنشاء الطلب.
            </p>
          </div>
        </SurfaceCard>

        {/* Order Type Selector */}
        <SurfaceCard className="space-y-3">
          <div className="flex items-center justify-between gap-3">
            <h4 className="text-sm font-bold text-slate-900">نوع الطلب</h4>
          </div>

          <div className="grid grid-cols-3 gap-2">
            {[
              { id: "DineIn" as const, label: "صالة" },
              { id: "Takeaway" as const, label: "تيك أواي" },
              { id: "Delivery" as const, label: "دليفري" },
            ].map((t) => (
              <button
                key={t.id}
                type="button"
                onClick={() => handleOrderTypeChange(t.id)}
                className={clsx(
                  "min-h-[44px] rounded-2xl px-3 py-2 text-sm font-semibold transition-all",
                  orderType === t.id
                    ? "bg-primary-600 text-white shadow-sm"
                    : "bg-white text-slate-700 ring-1 ring-slate-200 hover:bg-slate-50",
                )}
              >
                {t.label}
              </button>
            ))}
          </div>

          {orderType === "DineIn" && (
            <button
              type="button"
              onClick={() => setShowTableModal(true)}
              className={clsx(
                "flex w-full items-center justify-between gap-3 rounded-2xl border px-4 py-3 text-start transition",
                selectedTable
                  ? "border-emerald-200 bg-emerald-50 text-emerald-800"
                  : "border-dashed border-primary-200 bg-primary-50 text-primary-800",
              )}
            >
              <span className="flex items-center gap-2 text-sm font-bold">
                <Armchair className="h-4 w-4" />
                {selectedTable
                  ? `طاولة ${selectedTable.number}`
                  : "اختر طاولة للطلب"}
              </span>
              <span className="text-xs font-semibold">تغيير</span>
            </button>
          )}

          {orderType === "Delivery" && (
            <div className="space-y-3">
              <div className="grid gap-2 sm:grid-cols-2">
                <select
                  value={orderSource}
                  onChange={(event) =>
                    setOrderSource(event.target.value as OrderSource)
                  }
                  className="w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-900 outline-none transition focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
                >
                  <option value="POS">POS</option>
                  <option value="Talabat">طلبات</option>
                  <option value="Marsool">مرسول</option>
                  <option value="Jahez">جاهز</option>
                  <option value="Other">أخرى</option>
                </select>
                <input
                  type="text"
                  value={externalOrderNumber}
                  onChange={(e) => setExternalOrderNumber(e.target.value)}
                  placeholder="رقم الطلب الخارجي (اختياري)"
                  disabled={orderSource === "POS"}
                  className="w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-900 outline-none transition focus:border-primary-500 focus:ring-2 focus:ring-primary-100 disabled:bg-slate-50"
                />
              </div>
              <input
                type="text"
                value={deliveryAddress}
                onChange={(e) => setDeliveryAddress(e.target.value)}
                placeholder="عنوان الدليفري"
                className="w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-900 outline-none transition focus:border-primary-500 focus:ring-2 focus:ring-primary-100 disabled:bg-slate-50"
              />
              <input
                type="number"
                min="0"
                value={deliveryFee}
                onChange={(e) => setDeliveryFee(e.target.value)}
                placeholder="رسوم الدليفري"
                className="w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-900 outline-none transition focus:border-primary-500 focus:ring-2 focus:ring-primary-100 disabled:bg-slate-50"
              />
              <textarea
                placeholder="ملاحظات الدليفري (اختياري)"
                value={deliveryNotes}
                onChange={(e) => setDeliveryNotes(e.target.value)}
                rows={2}
                className="w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-900 outline-none transition focus:border-primary-500 focus:ring-2 focus:ring-primary-100 disabled:bg-slate-50"
              />
            </div>
          )}

          <div className="space-y-2 rounded-2xl border border-slate-200 bg-slate-50 p-3">
            <div className="flex items-center justify-between gap-3">
              <span className="flex items-center gap-2 text-sm font-bold text-slate-700">
                <MessageSquare className="h-4 w-4 text-amber-500" />
                ملاحظات الطلب
              </span>
              <button
                type="button"
                onClick={() => setShowSavedNotesModal(true)}
                className="rounded-full bg-white px-3 py-1.5 text-xs font-bold text-primary-700 ring-1 ring-primary-100 transition hover:bg-primary-50"
              >
                ملاحظات سريعة
              </button>
            </div>
            <textarea
              value={orderNotes}
              onChange={(event) => setOrderNotes(event.target.value)}
              rows={2}
              placeholder="مثال: بدون بصل، حار، بدون ملح"
              className="w-full resize-none rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-900 outline-none transition focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
            />
          </div>
        </SurfaceCard>
      </div>
    );
  };

  const renderCustomerTab = () => {
    if (selectedCustomer) {
      return (
        <div className="space-y-4">
          <div>
            <h3 className="text-lg font-black text-slate-900">
              العميل المرتبط
            </h3>
            <p className="mt-1 text-sm text-slate-500">
              البيانات هنا تؤثر على البيع الآجل والائتمان.
            </p>
          </div>

          <div className="rounded-[1.75rem] border border-primary-200 bg-[linear-gradient(180deg,#eff6ff_0%,#ffffff_100%)] p-4 shadow-sm">
            <div className="mb-4 flex items-start justify-between gap-3">
              <div className="flex items-center gap-3">
                <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-primary-600 text-white">
                  <User className="h-6 w-6" />
                </div>
                <div>
                  <p className="text-lg font-black text-slate-900">
                    {selectedCustomer.name || "عميل"}
                  </p>
                  <p className="mt-1 flex items-center gap-1 text-sm text-slate-600">
                    <Phone className="h-3.5 w-3.5" />
                    {selectedCustomer.phone}
                  </p>
                </div>
              </div>

              <button
                type="button"
                onClick={handleClearCustomer}
                className="inline-flex h-10 w-10 items-center justify-center rounded-2xl text-slate-400 transition-colors hover:bg-danger-50 hover:text-danger-500"
                aria-label="إزالة العميل"
              >
                <XIcon className="h-5 w-5" />
              </button>
            </div>

            <div className="space-y-3">
              {(selectedCustomer.loyaltyPoints ?? 0) > 0 && (
                <SurfaceCard className="border-amber-200 bg-amber-50 p-3">
                  <SummaryLine
                    label="نقاط الولاء"
                    value={selectedCustomer.loyaltyPoints.toString()}
                    valueClassName="text-amber-600"
                    icon={
                      <Star className="h-4 w-4 fill-current text-amber-500" />
                    }
                  />
                </SurfaceCard>
              )}

              {selectedCustomer.totalDue > 0 && (
                <SurfaceCard className="border-orange-200 bg-orange-50 p-3">
                  <SummaryLine
                    label="رصيد مستحق"
                    value={formatCurrency(selectedCustomer.totalDue)}
                    valueClassName="text-orange-600"
                    icon={<AlertCircle className="h-4 w-4 text-orange-500" />}
                  />
                </SurfaceCard>
              )}

              {selectedCustomer.creditLimit > 0 && (
                <SurfaceCard className="border-blue-200 bg-blue-50 p-3">
                  <SummaryLine
                    label="حد الائتمان"
                    value={formatCurrency(selectedCustomer.creditLimit)}
                    valueClassName="text-blue-600"
                    icon={<Wallet className="h-4 w-4 text-blue-500" />}
                  />
                </SurfaceCard>
              )}

              {!selectedCustomer.isActive && (
                <SurfaceCard className="border-danger-200 bg-danger-50 p-3">
                  <p className="text-sm font-semibold text-danger-600">
                    هذا العميل غير نشط ولا يمكن استخدامه في البيع الآجل.
                  </p>
                </SurfaceCard>
              )}
            </div>
          </div>
        </div>
      );
    }

    return (
      <div className="space-y-4">
        <div>
          <h3 className="text-lg font-black text-slate-900">ربط عميل</h3>
          <p className="mt-1 text-sm text-slate-500">
            ابحث برقم الهاتف أو اتركه للبيع النقدي.
          </p>
        </div>

        <div className="relative">
          <input
            ref={customerPhoneRef}
            type="text"
            value={customerPhone}
            onChange={(event) =>
              setCustomerPhone(event.target.value.replace(/[^0-9]/g, ""))
            }
            placeholder="ابحث برقم الهاتف..."
            className="w-full rounded-[1.4rem] border border-slate-200 bg-slate-50 px-4 py-3 text-sm font-medium text-slate-900 outline-none transition focus:border-primary-500 focus:bg-white focus:ring-2 focus:ring-primary-100"
            dir="ltr"
          />
          {isSearchingCustomer && (
            <div className="absolute left-4 top-1/2 -translate-y-1/2">
              <div className="h-5 w-5 animate-spin rounded-full border-2 border-primary-500 border-t-transparent" />
            </div>
          )}
        </div>

        {customerPhone.length >= 8 &&
          !isSearchingCustomer &&
          searchResult?.data && (
            <button
              type="button"
              onClick={() => handleSelectCustomer(searchResult.data!)}
              className="w-full rounded-[1.5rem] border border-success-200 bg-success-50 p-4 text-start transition-colors hover:bg-success-100"
            >
              <div className="flex items-center gap-3">
                <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-success-600 text-white">
                  <User className="h-5 w-5" />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="truncate font-bold text-slate-900">
                    {searchResult.data.name || "عميل"}
                  </p>
                  <p className="mt-1 text-sm text-slate-600">
                    {searchResult.data.phone}
                  </p>
                </div>
                <span className="text-xs font-semibold text-success-600">
                  اضغط للاختيار
                </span>
              </div>
            </button>
          )}

        {customerPhone.length >= 8 &&
          !isSearchingCustomer &&
          !searchResult?.data && (
            <SurfaceCard className="space-y-3">
              <p className="text-sm text-slate-500">لم يتم العثور على عميل</p>
              <button
                type="button"
                onClick={() => setShowCustomerCreateModal(true)}
                className="inline-flex min-h-[48px] w-full items-center justify-center gap-2 rounded-2xl bg-primary-600 px-4 py-3 text-sm font-semibold text-white transition-colors hover:bg-primary-700"
              >
                <Plus className="h-4 w-4" />
                إضافة عميل جديد
              </button>
            </SurfaceCard>
          )}

        <div className="rounded-[1.5rem] border border-dashed border-slate-200 bg-white px-4 py-8 text-center">
          <User className="mx-auto mb-3 h-12 w-12 text-slate-300" />
          <p className="text-sm font-semibold text-slate-700">عميل نقدي</p>
          <p className="mt-1 text-sm leading-7 text-slate-500">
            إذا لم يتم اختيار عميل، فسيتم إنشاء الطلب كبيع نقدي بدون حساب آجل.
          </p>
        </div>
      </div>
    );
  };
  const renderPaymentTab = () => {
    return (
      <div className="space-y-4">
        <div>
          <h3 className="text-lg font-black text-slate-900">الدفع</h3>
          <p className="mt-1 text-sm text-slate-500">
            اختر الطريقة، أدخل المبلغ، ثم أنهِ الفاتورة من الزر السفلي.
          </p>
        </div>

        <SurfaceCard className="space-y-4">
          <div className="flex items-center justify-between gap-3">
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-400">
                المبلغ المطلوب
              </p>
              <p className="mt-1 text-2xl font-black text-primary-700">
                {formatCurrency(paymentTotal)}
              </p>
            </div>

            <button
              type="button"
              onClick={() => setAmountPaid(paymentTotal.toFixed(2))}
              className="inline-flex min-h-[38px] items-center justify-center rounded-full bg-primary-50 px-3 py-2 text-xs font-semibold text-primary-700 transition-colors hover:bg-primary-100"
            >
              دفع كامل
            </button>
          </div>

          {orderType === "Delivery" && (
            <div className="rounded-[1.15rem] border border-primary-100 bg-primary-50 px-3 py-3 text-sm">
              <div className="flex items-center justify-between gap-3 text-primary-800">
                <span className="font-semibold">رسوم التوصيل</span>
                <span className="font-black">
                  {formatCurrency(deliveryFeeAmount)}
                </span>
              </div>
              <div className="mt-2 flex items-start justify-between gap-3 border-t border-primary-100 pt-2 text-primary-800">
                <span className="shrink-0 font-semibold">العنوان</span>
                <span className="text-end font-medium">
                  {deliveryAddress || "لم يتم إدخال عنوان"}
                </span>
              </div>
              {deliveryNotes && (
                <div className="mt-2 flex items-start justify-between gap-3 border-t border-primary-100 pt-2 text-primary-800">
                  <span className="shrink-0 font-semibold">ملاحظات</span>
                  <span className="text-end font-medium">{deliveryNotes}</span>
                </div>
              )}
            </div>
          )}

          <div className="grid grid-cols-3 gap-2">
            {paymentMethods.map((method) => (
              <button
                key={method.id}
                type="button"
                onClick={() => {
                  setSelectedPaymentMethod(method.id);
                  if (method.id === "Cash") {
                    setTransactionReference("");
                  }
                }}
                className={clsx(
                  "flex min-h-[68px] flex-col items-center justify-center gap-2 rounded-[1.15rem] border px-3 py-3 text-sm font-semibold transition-all",
                  selectedPaymentMethod === method.id
                    ? "border-primary-600 bg-primary-50 text-primary-700 shadow-sm"
                    : "border-slate-200 bg-white text-slate-600 hover:border-slate-300 hover:bg-slate-50",
                )}
              >
                {method.icon}
                <span>{method.label}</span>
              </button>
            ))}
          </div>

          {requiresTransactionReference && (
            <div className="space-y-2">
              <label className="block text-sm font-bold text-slate-900">
                رقم المعاملة <span className="text-danger-500">*</span>
              </label>
              <input
                type="text"
                value={transactionReference}
                onChange={(event) =>
                  setTransactionReference(event.target.value)
                }
                className="w-full rounded-[1.35rem] border border-slate-200 bg-white px-4 py-3 text-sm font-semibold text-slate-900 outline-none transition focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
                placeholder="اكتب رقم العملية من محفظة أو حساب بنكي"
              />
            </div>
          )}

          <div className="space-y-3">
            <label className="block text-sm font-bold text-slate-900">
              المبلغ المدفوع
            </label>
            <input
              type="number"
              inputMode="decimal"
              min="0"
              step="0.01"
              value={amountPaid}
              onChange={(event) => setAmountPaid(event.target.value)}
              className={clsx(
                "w-full rounded-[1.35rem] border bg-white px-4 py-3.5 text-lg font-black text-slate-900 outline-none transition",
                showPaymentError
                  ? "border-danger-400 ring-2 ring-danger-100"
                  : "border-slate-200 focus:border-primary-500 focus:ring-2 focus:ring-primary-100",
              )}
              placeholder="0.00"
            />

            <div className="flex gap-2 overflow-x-auto pb-1">
              {quickAmounts.map((amount) => (
                <button
                  key={amount}
                  type="button"
                  onClick={() => handleQuickAmount(amount)}
                  className="inline-flex min-h-[40px] shrink-0 items-center justify-center rounded-full border border-slate-200 bg-white px-3 py-2 text-sm font-semibold text-slate-700 transition-colors hover:bg-slate-50"
                >
                  {amount}
                </button>
              ))}
              <button
                type="button"
                onClick={() => setAmountPaid("")}
                className="inline-flex min-h-[40px] shrink-0 items-center justify-center rounded-full border border-slate-200 bg-white px-3 py-2 text-sm font-semibold text-slate-500 transition-colors hover:bg-slate-50"
              >
                مسح
              </button>
            </div>
          </div>
        </SurfaceCard>

        {change > 0 && (
          <SurfaceCard className="border-success-200 bg-success-50">
            <SummaryLine
              label="الباقي"
              value={formatCurrency(change)}
              valueClassName="text-success-600 text-base font-black"
              icon={<Banknote className="h-4 w-4 text-success-500" />}
            />
          </SurfaceCard>
        )}

        {allowPartialPayment && numericAmount < paymentTotal && (
          <SurfaceCard
            className={clsx(
              creditLimitExceeded
                ? "border-danger-200 bg-danger-50"
                : "border-orange-200 bg-orange-50",
            )}
          >
            <SummaryLine
              label="المبلغ المستحق"
              value={formatCurrency(amountDue)}
              valueClassName={clsx(
                "text-base font-black",
                creditLimitExceeded ? "text-danger-600" : "text-orange-600",
              )}
              icon={<Wallet className="h-4 w-4 text-orange-500" />}
            />
            {creditLimitExceeded && (
              <p className="mt-2 text-xs font-semibold text-danger-600">
                تجاوز حد الائتمان. المتاح: {formatCurrency(availableCredit)}
              </p>
            )}
            {selectedCustomer && !selectedCustomer.isActive && (
              <p className="mt-2 text-xs font-semibold text-danger-600">
                العميل غير نشط
              </p>
            )}
          </SurfaceCard>
        )}

        {selectedCustomer &&
          canTakeCredit &&
          canSellOnCredit &&
          selectedPaymentMethod === "Cash" && (
            <div className="flex items-start gap-3 rounded-[1.35rem] border border-blue-200 bg-blue-50 p-4">
              <input
                type="checkbox"
                id="partialPayment"
                checked={allowPartialPayment}
                onChange={(event) => {
                  const isChecked = event.target.checked;
                  setAllowPartialPayment(isChecked);

                  if (isChecked && numericAmount >= paymentTotal) {
                    setAmountPaid("");
                  }
                }}
                className="mt-1 h-5 w-5 rounded text-primary-600 focus:ring-2 focus:ring-primary-500"
              />
              <label htmlFor="partialPayment" className="flex-1 cursor-pointer">
                <p className="text-sm font-bold text-slate-900">
                  السماح بالدفع الجزئي
                </p>
                <p className="mt-1 text-xs leading-6 text-slate-600">
                  يخصم المدفوع الآن ويُسجل الباقي على العميل ضمن الائتمان
                  المتاح.
                </p>
              </label>
            </div>
          )}

        <label className="flex items-start gap-3 rounded-[1.35rem] border border-amber-200 bg-amber-50 p-4">
          <input
            type="checkbox"
            checked={printKitchenTicket}
            onChange={(event) => setPrintKitchenTicket(event.target.checked)}
            className="mt-1 h-5 w-5 rounded text-amber-600 focus:ring-2 focus:ring-amber-500"
          />
          <span className="flex-1">
            <span className="block text-sm font-bold text-slate-900">
              طباعة فاتورة المطبخ مع فاتورة البيع
            </span>
            <span className="mt-1 block text-xs leading-6 text-slate-600">
              سيتم طباعة أمر مطبخ بدون أسعار ثم فاتورة العميل كفاتورتين منفصلتين.
            </span>
          </span>
        </label>
      </div>
    );
  };

  const renderSummaryTab = () => (
    <SurfaceCard className="space-y-3">
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-400">
            ملخص الفاتورة
          </p>
          <p className="mt-1 text-base font-black text-slate-900">
            {itemsCount} عنصر
          </p>
        </div>

        <div className="text-end">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-400">
            الإجمالي
          </p>
          <p className="mt-1 text-lg font-black text-primary-700">
            {formatCurrency(displayWorkspaceTotal)}
          </p>
        </div>
      </div>

      <div className="rounded-[1.15rem] bg-slate-50 px-3 py-2.5 text-sm text-slate-600">
        {selectedCustomer ? (
          <div className="flex items-center gap-2">
            <User className="h-4 w-4 text-primary-500" />
            <span className="font-semibold text-slate-800">
              {selectedCustomer.name || selectedCustomer.phone}
            </span>
          </div>
        ) : (
          <div className="flex items-center gap-2">
            <User className="h-4 w-4 text-slate-400" />
            <span>عميل نقدي</span>
          </div>
        )}
      </div>

      {orderType === "DineIn" && (
        <div className="rounded-[1.15rem] bg-emerald-50 px-3 py-2.5 text-sm text-emerald-800">
          <div className="flex items-center justify-between gap-3">
            <span className="flex items-center gap-2 font-semibold">
              <Armchair className="h-4 w-4" />
              الصالة
            </span>
            <span className="font-black">
              {selectedTable ? `طاولة ${selectedTable.number}` : "لم يتم اختيار طاولة"}
            </span>
          </div>
        </div>
      )}

      {orderType !== "Delivery" && (
        <div className="rounded-[1.15rem] bg-slate-50 px-3 py-2.5 text-sm text-slate-700">
          <div className="flex items-center justify-between gap-3">
            <span className="font-semibold">نوع الطلب</span>
            <span className="font-black">
              {orderType === "Takeaway" ? "تيك أواي" : "صالة"}
            </span>
          </div>
        </div>
      )}

      <SummaryLine label="المجموع الفرعي" value={formatCurrency(subtotal)} />
      {discountAmount > 0 && (
        <SummaryLine
          label={
            discountType === "Percentage" && discountValue
              ? `الخصم (${discountValue}%)`
              : "الخصم"
          }
          value={`- ${formatCurrency(discountAmount)}`}
          valueClassName="text-success-600"
          icon={<Tag className="h-4 w-4 text-success-500" />}
        />
      )}
      {isTaxEnabled && (
        <SummaryLine
          label={`الضريبة (${taxRate}%)`}
          value={formatCurrency(taxAmount)}
        />
      )}
      {serviceChargeAmount > 0 && (
        <SummaryLine
          label={`رسوم الخدمة (${serviceChargeRate}%)`}
          value={formatCurrency(serviceChargeAmount)}
        />
      )}
      {orderType === "Delivery" && (
        <>
          <SummaryLine
            label="رسوم التوصيل"
            value={formatCurrency(deliveryFeeAmount)}
            icon={<Store className="h-4 w-4 text-primary-500" />}
          />
          <div className="rounded-[1.15rem] bg-primary-50 px-3 py-2.5 text-sm text-primary-800">
            <div className="flex items-start justify-between gap-3">
              <span className="shrink-0 font-semibold">عنوان التوصيل</span>
              <span className="text-end font-medium">
                {deliveryAddress || "لم يتم إدخال عنوان"}
              </span>
            </div>
            {deliveryNotes && (
              <div className="mt-2 flex items-start justify-between gap-3 border-t border-primary-100 pt-2">
                <span className="shrink-0 font-semibold">ملاحظات</span>
                <span className="text-end font-medium">{deliveryNotes}</span>
              </div>
            )}
            <div className="mt-2 flex items-start justify-between gap-3 border-t border-primary-100 pt-2">
              <span className="shrink-0 font-semibold">مصدر الطلب</span>
              <span className="text-end font-medium">
                {orderSource}
                {externalOrderNumber.trim()
                  ? ` - ${externalOrderNumber.trim()}`
                  : ""}
              </span>
            </div>
          </div>
        </>
      )}
      {orderNotes.trim() && (
        <div className="rounded-[1.15rem] bg-amber-50 px-3 py-2.5 text-sm text-amber-800">
          <div className="flex items-start justify-between gap-3">
            <span className="shrink-0 font-semibold">ملاحظات الطلب</span>
            <span className="whitespace-pre-line text-end font-medium">
              {orderNotes.trim()}
            </span>
          </div>
        </div>
      )}
      <SummaryLine
        label="طريقة الدفع"
        value={selectedPaymentMethodLabel}
        icon={<CreditCard className="h-4 w-4 text-primary-500" />}
      />

      <div className="border-t border-slate-200 pt-3">
        <SummaryLine
          label="الصافي النهائي"
          value={formatCurrency(checkoutTotal)}
          valueClassName="text-primary-600 text-base font-black"
          icon={<Wallet className="h-4 w-4 text-primary-500" />}
        />
        <p className="mt-2 text-xs text-slate-400">
          * الإجمالي تقديري، ويتم تأكيده نهائيًا عند إنشاء الطلب.
        </p>
      </div>
    </SurfaceCard>
  );

  const renderActiveTab = () => {
    switch (activeTab) {
      case "customer":
        return renderCustomerTab();
      case "payment":
        return renderPaymentTab();
      case "summary":
        return renderSummaryTab();
      case "cart":
      default:
        return renderCartTab();
    }
  };

  const renderWorkspaceFooter = (withSafeArea = false) => (
    <div
      className={clsx(
        "border-t border-slate-200 bg-white px-4 py-4",
        withSafeArea && "pb-[calc(env(safe-area-inset-bottom)+1rem)]",
      )}
    >
      <div className="mb-3 flex items-center justify-between gap-3 rounded-[1.25rem] bg-gray-50 px-4 py-3">
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-400">
            الإجمالي
          </p>
          <p className="mt-1 text-lg font-black text-primary-700">
            {formatCurrency(displayWorkspaceTotal)}
          </p>
        </div>
        <div className="text-end text-xs text-slate-500">
          <p>{itemsCount} عنصر</p>
          {selectedCustomer ? (
            <p className="mt-1 truncate font-semibold text-slate-700">
              {selectedCustomer.name || selectedCustomer.phone}
            </p>
          ) : (
            <p className="mt-1 font-semibold text-slate-700">عميل نقدي</p>
          )}
        </div>
      </div>

      {activeTab === "payment" && paymentTotal > 0 ? (
        <Button
          variant="success"
          size="xl"
          className="w-full rounded-[1.5rem]"
          onClick={handleCompletePayment}
          isLoading={isCreating || isCompleting}
          disabled={
            isCreating ||
            isCompleting ||
            (requiresTransactionReference && !transactionReference.trim()) ||
            (numericAmount < paymentTotal && !canSellOnCredit) ||
            (numericAmount < paymentTotal && !allowPartialPayment) ||
            (numericAmount < paymentTotal && creditLimitExceeded)
          }
          rightIcon={<Check className="h-5 w-5" />}
        >
          {isCreating
              ? "جاري إنشاء الطلب..."
              : isCompleting
                ? "جاري الدفع..."
                : numericAmount < paymentTotal && allowPartialPayment
                  ? `إتمام البيع الآجل (مستحق: ${formatCurrency(amountDue)})`
                  : "إتمام الدفع"}
        </Button>
      ) : (
        <div className="grid gap-2">
          <Button
            variant="success"
            size="xl"
            className="rounded-[1.5rem]"
            onClick={openPaymentWorkspace}
            disabled={paymentTotal <= 0}
            rightIcon={<CreditCard className="h-5 w-5" />}
          >
            الدفع {formatCurrency(checkoutTotal)}
          </Button>
        </div>
      )}
    </div>
  );

  const renderSearchLanding = () => null;

  const renderProductResultsPanel = () => {
    if (showCatalog) {
      return (
        <div className="space-y-3">
          <div className="rounded-[1.25rem] border border-gray-200 bg-slate-50 px-4 py-3">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <p className="text-sm font-bold text-slate-900">
                  {selectedCategoryName
                    ? `فئة ${selectedCategoryName}`
                    : "الكتالوج"}
                </p>
                <p className="mt-1 text-xs text-slate-500">
                  {filteredProducts.length} منتج متاح للعرض
                </p>
              </div>

              <button
                type="button"
                onClick={handleResetDiscovery}
                className="inline-flex min-h-[34px] items-center justify-center rounded-full border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-600 transition-colors hover:bg-slate-50"
              >
                إغلاق الكتالوج
              </button>
            </div>
          </div>

          <CategoryChips
            categories={categories}
            selectedId={selectedCategory}
            onSelect={handleCategorySelect}
          />

          <ProductListView
            products={filteredProducts}
            categories={categories}
            onAddProduct={(product) => handleAddProductToCart(product)}
            stockByProductId={stockByProductId}
            hasInventorySnapshot={hasInventorySnapshot}
            isInventoryLoading={isInventoryLoading}
          />
        </div>
      );
    }

    return (
      <div className="space-y-3">
        <div className="rounded-[1.25rem] border border-primary-100 bg-primary-50 px-4 py-3">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <p className="text-sm font-bold text-slate-900">نتائج البحث</p>
              <p className="mt-1 text-xs text-slate-500">
                {filteredProducts.length} نتيجة لعبارة "
                {deferredSearchInput.trim()}"
              </p>
            </div>

            <button
              type="button"
              onClick={() => setSearchInput("")}
              className="inline-flex min-h-[34px] items-center justify-center rounded-full border border-primary-200 bg-white px-3 py-1.5 text-xs font-semibold text-primary-700 transition-colors hover:bg-primary-100"
            >
              مسح البحث
            </button>
          </div>
        </div>

        {visibleSearchResults.length > 0 ? (
          <div className="space-y-2">
            {visibleSearchResults.map((product) => {
              const cartItem = items.find(
                (item) => item.product.id === product.id,
              );
              const quantityInCart = cartItem?.quantity ?? 0;
              const totalStock = getProductCurrentStock(
                product,
                stockByProductId,
              );
              const availableStock = hasInventorySnapshot
                ? getProductAvailableStock(
                    product,
                    quantityInCart,
                    stockByProductId,
                  )
                : Number.POSITIVE_INFINITY;
              const requiresDirectStock =
                product.type !== ProductType.Manufactured &&
                product.trackInventory;
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
              const categoryName =
                categories.find(
                  (category) => category.id === product.categoryId,
                )?.name ?? "غير مصنف";

              return (
                <button
                  key={product.id}
                  type="button"
                  onClick={() => {
                    const added = handleAddProductToCart(product);
                    if (added) {
                      setSearchInput("");
                      searchInputRef.current?.focus();
                      setActiveTab("cart");
                    }
                  }}
                  disabled={!product.isActive || isOutOfStock || !canAddMore}
                  className={clsx(
                    "flex w-full items-center justify-between gap-3 rounded-[1.2rem] border px-3 py-3 text-start transition-all",
                    quantityInCart > 0
                      ? "border-primary-300 bg-primary-50"
                      : "border-gray-200 bg-white hover:border-primary-200 hover:bg-slate-50",
                    (!product.isActive || isOutOfStock || !canAddMore) &&
                      "cursor-not-allowed opacity-55",
                  )}
                >
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2">
                      <p className="truncate text-sm font-bold text-slate-900">
                        {product.name}
                      </p>
                      {quantityInCart > 0 && (
                        <span className="inline-flex h-5 min-w-[1.4rem] items-center justify-center rounded-full bg-primary-600 px-1.5 text-[10px] font-bold text-white">
                          {quantityInCart}
                        </span>
                      )}
                    </div>
                    <p className="mt-1 text-xs text-slate-500">
                      {categoryName}
                    </p>
                    {requiresDirectStock && hasInventorySnapshot && (
                      <p
                        className={clsx(
                          "mt-1 text-[11px] font-semibold",
                          isOutOfStock ? "text-danger-600" : "text-emerald-600",
                        )}
                      >
                        {isOutOfStock
                          ? "نفد المخزون"
                          : `المتاح ${availableStock}`}
                      </p>
                    )}
                  </div>

                  <div className="shrink-0 text-end">
                    <p className="text-sm font-black text-slate-900">
                      {formatCurrency(product.suggestedPrice)}
                    </p>
                    <span
                      className={clsx(
                        "mt-1 inline-flex rounded-full px-2 py-0.5 text-[10px] font-semibold",
                        !product.isActive || isOutOfStock || !canAddMore
                          ? "bg-slate-100 text-slate-500"
                          : "bg-primary-50 text-primary-700",
                      )}
                    >
                      {!product.isActive || isOutOfStock || !canAddMore
                        ? "غير متاح"
                        : "إضافة"}
                    </span>
                  </div>
                </button>
              );
            })}

            {filteredProducts.length > visibleSearchResults.length && (
              <div className="rounded-[1.15rem] border border-dashed border-slate-200 bg-slate-50 px-4 py-3 text-xs text-slate-500">
                تم عرض أول 12 منتج فقط. افتح الكتالوج إذا كنت تريد استعراض
                المزيد.
              </div>
            )}
          </div>
        ) : (
          <div className="rounded-[1.25rem] border border-dashed border-slate-200 bg-slate-50 px-4 py-8 text-center">
            <p className="text-sm font-semibold text-slate-700">
              لا توجد منتجات مطابقة.
            </p>
            <p className="mt-1 text-xs leading-6 text-slate-500">
              جرّب اسمًا مختلفًا أو افتح الكتالوج، ويمكنك إنشاء منتج جديد إذا
              كانت لديك الصلاحية.
            </p>
            {canQuickCreateProduct && (
              <button
                type="button"
                onClick={handleOpenQuickCreate}
                className="mt-4 inline-flex min-h-[40px] items-center justify-center rounded-full bg-primary-600 px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-primary-700"
              >
                إضافة منتج جديد
              </button>
            )}
          </div>
        )}
      </div>
    );
  };

  const renderOrderSections = () => (
    <div className="space-y-3">
      {renderCartTab()}

      <div className="grid gap-2 sm:grid-cols-2">
        <button
          type="button"
          onClick={() =>
            setActiveTab((currentTab) =>
              currentTab === "customer" ? "cart" : "customer",
            )
          }
          className="rounded-[1.35rem] border border-gray-200 bg-white px-4 py-3 text-start shadow-sm transition-colors hover:border-primary-200 hover:bg-primary-50"
        >
          <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-slate-400">
            العميل
          </p>
          <p className="mt-1 text-sm font-bold text-slate-900">
            {selectedCustomer
              ? selectedCustomer.name || selectedCustomer.phone
              : "إضافة عميل للفاتورة"}
          </p>
          <p className="mt-1 text-xs text-slate-500">
            {selectedCustomer
              ? "تغيير أو مراجعة بيانات العميل"
              : "اتركها نقدي أو اربط عميلًا للبيع الآجل"}
          </p>
        </button>

        <button
          type="button"
          onClick={openPaymentWorkspace}
          className="rounded-[1.35rem] border border-primary-200 bg-primary-50 px-4 py-3 text-start shadow-sm transition-colors hover:bg-primary-100"
        >
          <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-primary-500">
            الدفع
          </p>
          <p className="mt-1 text-sm font-bold text-slate-900">
            {selectedPaymentMethodLabel}
          </p>
          <p className="mt-1 text-xs text-slate-500">
            افتح الدفع لاختيار الطريقة والمبلغ المدفوع.
          </p>
        </button>
      </div>

      {activeTab === "customer" && (
        <div className="rounded-[1.5rem] border border-gray-200 bg-white p-4 shadow-sm">
          {renderCustomerTab()}
        </div>
      )}

      {renderSummaryTab()}

      {activeTab === "payment" && (
        <div className="rounded-[1.5rem] border border-gray-200 bg-white p-4 shadow-sm">
          {renderPaymentTab()}
        </div>
      )}
    </div>
  );

  const renderDesktopWorkspace = () => {
    if (itemsCount === 0) {
      return null;
    }

    return (
      <aside className="hidden min-h-0 lg:flex lg:w-[25rem] lg:flex-col xl:w-[28rem]">
        <div className="min-h-0 flex-1 overflow-y-auto pe-1">
          {renderOrderSections()}
        </div>
        <div className="mt-3 overflow-hidden rounded-[1.5rem] border border-gray-200 bg-white shadow-sm">
          {renderWorkspaceFooter()}
        </div>
      </aside>
    );
  };

  const renderMobileWorkspace = () => {
    if (itemsCount === 0) {
      return null;
    }

    return (
      <div className="pointer-events-none fixed inset-x-0 bottom-0 z-20 bg-gradient-to-t from-gray-100 via-gray-100 to-transparent px-2 pb-[calc(env(safe-area-inset-bottom)+0.35rem)] pt-6 lg:hidden">
        <div className="pointer-events-auto overflow-hidden rounded-[1.4rem] border border-gray-200 bg-white shadow-[0_16px_40px_rgba(15,23,42,0.12)]">
          {renderWorkspaceFooter(true)}
        </div>
      </div>
    );
  };

  const renderSearchWorkspaceLayout = () => (
    <div className="flex min-h-0 flex-1 flex-col gap-3 lg:flex-row">
      <section className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-[1.5rem] border border-gray-200 bg-white shadow-sm">
        <div className="border-b border-gray-200 bg-white px-3 py-3 md:px-4">
          <div className="flex flex-col gap-3">
            <div className="flex items-center justify-between gap-2 overflow-x-auto pb-0.5">
              <div className="flex min-w-0 items-center gap-1.5">
                <span className="inline-flex items-center gap-1 rounded-full bg-gray-100 px-2.5 py-1 text-[11px] font-semibold text-gray-700">
                  <Store className="h-3.5 w-3.5 text-primary-500" />
                  {currentBranch?.name || "الفرع الحالي"}
                </span>
                {shiftDuration && (
                  <span className="inline-flex items-center gap-1 rounded-full bg-gray-100 px-2.5 py-1 text-[11px] font-semibold text-gray-700">
                    <Clock3 className="h-3 w-3 text-gray-500" />
                    {shiftDuration}
                  </span>
                )}
              </div>

              <div className="flex items-center gap-1.5">
                {itemsCount > 0 && (
                  <span className="inline-flex items-center gap-1 rounded-full bg-gray-100 px-2.5 py-1 text-[11px] font-semibold text-gray-700">
                    <ShoppingCart className="h-3 w-3 text-primary-500" />
                    {itemsCount}
                  </span>
                )}
                <span className="inline-flex items-center gap-1 rounded-full bg-success-50 px-2.5 py-1 text-[11px] font-semibold text-success-700">
                  <Wallet className="h-3 w-3" />
                  {itemsCount > 0
                    ? formatCurrency(displayWorkspaceTotal)
                    : "جاهز للبيع"}
                </span>
              </div>
            </div>

            <div className="grid gap-2 sm:grid-cols-[minmax(0,1fr)_auto]">
              <div className="relative">
                <ScanBarcode className="pointer-events-none absolute left-3.5 top-1/2 h-4.5 w-4.5 -translate-y-1/2 text-slate-400" />
                <input
                  ref={searchInputRef}
                  type="text"
                  value={searchInput}
                  onChange={(event) => setSearchInput(event.target.value)}
                  onKeyDown={handleSearchKeyDown}
                  placeholder="ابحث بالاسم أو الباركود أو SKU"
                  className="w-full rounded-[1.2rem] border border-slate-200 bg-slate-50 px-4 py-3 pe-11 ps-4 text-sm font-medium text-slate-900 outline-none transition focus:border-primary-500 focus:bg-white focus:ring-2 focus:ring-primary-100"
                  autoComplete="off"
                />
              </div>

              <button
                type="button"
                onClick={() => {
                  setShowCatalog((currentValue) => !currentValue);
                  setSelectedCategory(null);
                }}
                className={clsx(
                  "inline-flex min-h-[44px] items-center justify-center gap-2 rounded-[1rem] px-4 py-2.5 text-sm font-semibold transition-colors",
                  showCatalog
                    ? "border border-primary-200 bg-primary-50 text-primary-700"
                    : "bg-primary-600 text-white hover:bg-primary-700",
                )}
              >
                <Package className="h-4 w-4" />
                {showCatalog ? "إخفاء الكتالوج" : "الكتالوج"}
              </button>
            </div>

            {(showProductResults || itemsCount > 0) && (
              <div className="flex gap-2 overflow-x-auto pb-1">
                <button
                  type="button"
                  onClick={() => setShowAvailableOnly(!showAvailableOnly)}
                  className={clsx(
                    "inline-flex min-h-[36px] shrink-0 items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-semibold transition-all",
                    showAvailableOnly
                      ? "bg-success-600 text-white shadow-sm"
                      : "border border-gray-200 bg-white text-gray-700 hover:bg-gray-50",
                  )}
                >
                  <PackageCheck className="h-3.5 w-3.5" />
                  المتاح فقط
                </button>

                {showProductResults && (
                  <button
                    type="button"
                    onClick={handleResetDiscovery}
                    className="inline-flex min-h-[36px] shrink-0 items-center gap-1.5 rounded-full border border-gray-200 bg-white px-3 py-1.5 text-xs font-semibold text-gray-700 transition-colors hover:bg-gray-50"
                  >
                    <XIcon className="h-3.5 w-3.5" />
                    مسح النتائج
                  </button>
                )}

                {canQuickCreateProduct && (
                  <button
                    type="button"
                    onClick={handleOpenQuickCreate}
                    className="inline-flex min-h-[36px] shrink-0 items-center gap-1.5 rounded-full border border-gray-200 bg-white px-3 py-1.5 text-xs font-semibold text-gray-700 transition-colors hover:bg-gray-50"
                  >
                    <PlusCircle className="h-3.5 w-3.5" />
                    منتج جديد
                  </button>
                )}

                {itemsCount > 0 && (
                  <button
                    type="button"
                    onClick={() => setShowCustomItem(true)}
                    className="inline-flex min-h-[36px] shrink-0 items-center gap-1.5 rounded-full border border-gray-200 bg-white px-3 py-1.5 text-xs font-semibold text-gray-700 transition-colors hover:bg-gray-50"
                    title="إضافة منتج مخصص"
                  >
                    <FileText className="h-3.5 w-3.5" />
                    منتج مخصص
                  </button>
                )}
              </div>
            )}
          </div>
        </div>

        <div
          className={clsx(
            "min-h-0 flex-1 overflow-y-auto px-3 pt-3 md:px-4",
            itemsCount > 0 ? "pb-28 lg:pb-4" : "pb-4",
          )}
        >
          {showProductResults
            ? renderProductResultsPanel()
            : renderSearchLanding()}

          {itemsCount > 0 && (
            <div className="mt-4 lg:hidden">{renderOrderSections()}</div>
          )}
        </div>
      </section>

      {renderDesktopWorkspace()}
    </div>
  );

  return (
    <div className="h-full overflow-hidden bg-gray-100">
      <div className="flex h-full flex-col overflow-hidden p-1.5 md:p-2.5">
        {shiftWarning && shiftWarning.shouldWarn && !dismissedWarning && (
          <div className="mb-1.5">
            <ShiftWarningBanner
              warning={shiftWarning}
              onClose={() => setDismissedWarning(true)}
            />
          </div>
        )}

        <LowStockAlert />
        <BatchExpiryAlertBanner />

        {renderSearchWorkspaceLayout()}
        {renderMobileWorkspace()}

        {false && (
          <div className="flex min-h-0 flex-1 flex-col gap-3 lg:flex-row">
            <section className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-sm">
              <div className="border-b border-gray-200 bg-white px-3 pb-3 pt-3 md:px-4">
                <div className="flex flex-col gap-3">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <div className="flex min-w-0 flex-wrap items-center gap-2">
                      <span className="inline-flex items-center gap-1 rounded-lg bg-gray-100 px-3 py-1.5 text-xs font-semibold text-gray-700">
                        <Store className="h-3.5 w-3.5 text-primary-500" />
                        {currentBranch?.name || "الفرع الحالي"}
                      </span>
                      {shiftDuration && (
                        <span className="inline-flex items-center gap-1 rounded-lg bg-gray-100 px-3 py-1.5 text-xs font-semibold text-gray-700">
                          <Clock3 className="h-3.5 w-3.5 text-gray-500" />
                          {shiftDuration}
                        </span>
                      )}
                      {shiftOpenedAt && (
                        <span className="inline-flex items-center gap-1 rounded-lg bg-gray-100 px-3 py-1.5 text-xs font-semibold text-gray-700">
                          فتحت {shiftOpenedAt}
                        </span>
                      )}
                    </div>

                    <div className="flex items-center gap-2">
                      <span className="inline-flex items-center gap-1 rounded-lg bg-primary-50 px-3 py-1.5 text-xs font-semibold text-primary-700">
                        <ShoppingCart className="h-3.5 w-3.5" />
                        {itemsCount}
                      </span>
                      <span className="inline-flex items-center gap-1 rounded-lg bg-success-50 px-3 py-1.5 text-xs font-semibold text-success-700">
                        <Wallet className="h-3.5 w-3.5" />
                        {formatCurrency(displayWorkspaceTotal)}
                      </span>
                    </div>
                  </div>

                  <div className="grid gap-2 sm:grid-cols-[minmax(0,1fr)_auto]">
                    <div className="relative">
                      <ScanBarcode className="pointer-events-none absolute right-4 top-1/2 h-5 w-5 -translate-y-1/2 text-slate-400" />
                      <input
                        ref={searchInputRef}
                        type="text"
                        value={searchInput}
                        onChange={(event) => setSearchInput(event.target.value)}
                        onKeyDown={handleSearchKeyDown}
                        placeholder="بحث بالاسم أو الباركود أو SKU... واضغط Enter للإضافة"
                        className="w-full rounded-[1.45rem] border border-slate-200 bg-white px-4 py-3.5 pe-12 text-sm font-medium text-slate-900 outline-none transition focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
                        autoComplete="off"
                      />
                    </div>

                    <button
                      type="button"
                      onClick={() => setShowCatalog(true)}
                      className="inline-flex min-h-[50px] items-center justify-center gap-2 rounded-xl bg-primary-600 px-4 py-3 text-sm font-semibold text-white transition-all hover:bg-primary-700"
                    >
                      <Package className="h-4.5 w-4.5" />
                      عرض الكتالوج
                    </button>
                  </div>

                  <div className="flex flex-wrap gap-2">
                    <button
                      type="button"
                      onClick={() => setShowAvailableOnly(!showAvailableOnly)}
                      className={clsx(
                        "inline-flex min-h-[42px] shrink-0 items-center gap-2 rounded-xl px-3 py-2 text-sm font-semibold transition-all",
                        showAvailableOnly
                          ? "bg-success-600 text-white shadow-sm"
                          : "bg-gray-100 text-gray-700 hover:bg-gray-200",
                      )}
                    >
                      <PackageCheck className="h-4 w-4" />
                      المتاح فقط
                    </button>

                    <button
                      type="button"
                      onClick={handleOpenQuickCreate}
                      disabled={!canQuickCreateProduct}
                      className={clsx(
                        "inline-flex min-h-[42px] shrink-0 items-center gap-2 rounded-xl px-3 py-2 text-sm font-semibold transition-all",
                        canQuickCreateProduct
                          ? "bg-gray-100 text-gray-700 hover:bg-gray-200"
                          : "cursor-not-allowed bg-gray-100 text-gray-400",
                      )}
                    >
                      <PlusCircle className="h-4 w-4" />
                      منتج جديد
                    </button>

                    <button
                      type="button"
                      onClick={() => setShowCustomItem(true)}
                      disabled={itemsCount === 0}
                      className={clsx(
                        "inline-flex min-h-[42px] shrink-0 items-center gap-2 rounded-xl px-3 py-2 text-sm font-semibold transition-all",
                        itemsCount > 0
                          ? "bg-gray-100 text-gray-700 hover:bg-gray-200"
                          : "cursor-not-allowed bg-gray-100 text-gray-400",
                      )}
                      title={
                        itemsCount > 0 ? "إضافة منتج مخصص" : "ابدأ طلب أولاً"
                      }
                    >
                      <FileText className="h-4 w-4" />
                      منتج مخصص
                    </button>
                  </div>

                  <div className="rounded-xl bg-gray-50 p-2">
                    <CategoryChips
                      categories={categories}
                      selectedId={selectedCategory}
                      onSelect={setSelectedCategory}
                    />
                  </div>
                </div>
              </div>

              <div className="min-h-0 flex-1 overflow-y-auto px-3 pb-4 pt-3 md:px-4">
                {deferredSearchInput.trim() && (
                  <div className="mb-3 flex items-center justify-between gap-3 rounded-xl border border-gray-200 bg-gray-50 px-4 py-3">
                    <div>
                      <p className="text-sm font-semibold text-slate-900">
                        نتائج البحث
                      </p>
                      <p className="mt-1 text-xs text-slate-500">
                        {filteredProducts.length} نتيجة لعبارة "
                        {deferredSearchInput.trim()}"
                      </p>
                    </div>
                    <button
                      type="button"
                      onClick={() => setSearchInput("")}
                      className="inline-flex min-h-[40px] items-center justify-center rounded-2xl border border-slate-200 bg-white px-3 py-2 text-xs font-semibold text-slate-600 transition-colors hover:bg-slate-50"
                    >
                      مسح
                    </button>
                  </div>
                )}

                <ProductListView
                  products={filteredProducts}
                  categories={categories}
                  onAddProduct={(product) => handleAddProductToCart(product)}
                  stockByProductId={stockByProductId}
                  hasInventorySnapshot={hasInventorySnapshot}
                  isInventoryLoading={isInventoryLoading}
                />
              </div>
            </section>

            <aside className="flex min-h-[22rem] max-h-[56vh] flex-col overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-sm lg:min-h-0 lg:max-h-none lg:w-[26rem] xl:w-[29rem]">
              <div className="border-b border-gray-200 bg-gray-50 px-4 py-3">
                <div className="mx-auto mb-2 h-1.5 w-16 rounded-full bg-gray-300 lg:hidden" />
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.18em] text-gray-400">
                      الطلب الحالي
                    </p>
                    <h2 className="mt-1 text-lg font-black text-gray-900">
                      السلة والدفع
                    </h2>
                  </div>

                  <div className="rounded-xl bg-white px-3 py-2 text-end ring-1 ring-gray-200">
                    <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-gray-400">
                      الإجمالي
                    </p>
                    <p className="mt-1 text-lg font-black text-primary-700">
                      {formatCurrency(displayWorkspaceTotal)}
                    </p>
                  </div>
                </div>
              </div>

              <div className="grid grid-cols-4 gap-2 border-b border-slate-200 bg-white p-2">
                {workspaceTabs.map((tab) => (
                  <WorkspaceTabButton
                    key={tab.id}
                    icon={tab.icon}
                    label={tab.label}
                    active={activeTab === tab.id}
                    disabled={tab.disabled}
                    indicator={tab.indicator}
                    onClick={() => handleWorkspaceTabChange(tab.id)}
                  />
                ))}
              </div>

              <div className="min-h-0 flex-1 overflow-y-auto bg-white px-3 py-4 md:px-4">
                {renderActiveTab()}
              </div>

              <div className="border-t border-slate-200 bg-white px-4 py-4">
                <div className="mb-3 flex items-center justify-between gap-3 rounded-xl bg-gray-50 px-4 py-3">
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-400">
                      الإجمالي
                    </p>
                    <p className="mt-1 text-lg font-black text-primary-700">
                      {formatCurrency(displayWorkspaceTotal)}
                    </p>
                  </div>
                  <div className="text-end text-xs text-slate-500">
                    <p>{itemsCount} عنصر</p>
                    {selectedCustomer ? (
                      <p className="mt-1 truncate font-semibold text-slate-700">
                        {selectedCustomer.name || selectedCustomer.phone}
                      </p>
                    ) : (
                      <p className="mt-1 font-semibold text-slate-700">
                        عميل نقدي
                      </p>
                    )}
                  </div>
                </div>

                {activeTab === "payment" && items.length > 0 ? (
                  <Button
                    variant="success"
                    size="xl"
                    className="w-full rounded-[1.5rem]"
                    onClick={handleCompletePayment}
                    isLoading={isCreating || isCompleting}
                    disabled={
                      isCreating ||
                      isCompleting ||
                      (requiresTransactionReference &&
                        !transactionReference.trim()) ||
                      (numericAmount < paymentTotal && !canSellOnCredit) ||
                      (numericAmount < paymentTotal && !allowPartialPayment) ||
                      (numericAmount < paymentTotal && creditLimitExceeded)
                    }
                    rightIcon={<Check className="h-5 w-5" />}
                  >
                    {isCreating
                        ? "جاري إنشاء الطلب..."
                        : isCompleting
                          ? "جاري الدفع..."
                          : numericAmount < paymentTotal && allowPartialPayment
                            ? `إتمام البيع الآجل (مستحق: ${formatCurrency(amountDue)})`
                            : "إتمام الدفع"}
                  </Button>
                ) : (
                  <Button
                    variant="success"
                    size="xl"
                    className="w-full rounded-[1.5rem]"
                    onClick={openPaymentWorkspace}
                    disabled={items.length === 0}
                    rightIcon={<CreditCard className="h-5 w-5" />}
                  >
                    الدفع {formatCurrency(checkoutTotal)}
                  </Button>
                )}
              </div>
            </aside>
          </div>
        )}
      </div>

      {showQuickCreate && canQuickCreateProduct && (
        <ProductQuickCreateModal
          onClose={() => setShowQuickCreate(false)}
          onSuccess={() => {
            toast.success("تم إضافة المنتج بنجاح");
            setShowQuickCreate(false);
          }}
        />
      )}

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
            toast.success(`تم إضافة: ${item.name}`);
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

            toast.success(`تمت الإضافة: ${selectedProductForBatch.name} من ${batch.batchNumber || "بدون رقم دفعة"}`);
            setShowBatchModal(false);
            setSelectedProductForBatch(null);
          }}
        />
      )}

      {showCustomerCreateModal && (
        <CustomerQuickCreateModal
          initialPhone={customerPhone}
          onClose={() => setShowCustomerCreateModal(false)}
          onSuccess={(customer) => {
            handleSelectCustomer(customer);
            setShowCustomerCreateModal(false);
          }}
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
          notes={savedOrderNotes}
          onApply={appendOrderNote}
          onClose={() => setShowSavedNotesModal(false)}
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
    toast.error(`لا توجد دفعات متاحة للبيع للمنتج: ${product.name}`);
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

export default POSWorkspacePage;
