import { useState, useRef, useEffect, useCallback } from "react";
import { useProducts, useCategories } from "@/hooks/useProducts";
import { useCart } from "@/hooks/useCart";
import { useShift } from "@/hooks/useShift";
import { useOrders } from "@/hooks/useOrders";
import { usePOSShortcuts } from "@/hooks/usePOSShortcuts";
import { useGetShiftWarningsQuery } from "@/api/shiftsApi";
import { usePOSMode } from "@/hooks/usePOSMode";
import { Customer } from "@/types/customer.types";
import { PaymentMethod } from "@/types/order.types";
import { toast } from "sonner";
import { Link, Navigate } from "react-router-dom";
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
  CheckCircle2,
  Minus,
  Package,
} from "lucide-react";
import clsx from "clsx";
import { getProductCurrentStock } from "@/utils/productStock";

// Import components
import { ProductListView } from "@/components/pos/ProductListView";
import { CategoryChips } from "@/components/pos/CategoryChips";
import { CartItemComponent } from "@/components/pos/CartItem";
import { CustomerQuickCreateModal } from "@/components/pos/CustomerQuickCreateModal";
import { ProductQuickCreateModal } from "@/components/pos/ProductQuickCreateModal";
import { CustomItemModal } from "@/components/pos/CustomItemModal";
import { Loading } from "@/components/common/Loading";
import { Button } from "@/components/common/Button";
import { formatCurrency } from "@/utils/formatters";
import { useLazyGetCustomerByPhoneQuery } from "@/api/customersApi";

type WorkspaceTab = "cart" | "customer" | "payment" | "summary";

export const POSWorkspacePage = () => {
  const { mode } = usePOSMode();

  // Redirect to cashier mode if mode is cashier
  if (mode === "cashier") {
    return <Navigate to="/pos" replace />;
  }

  // State
  const [selectedCategory, setSelectedCategory] = useState<number | null>(null);
  const [showAvailableOnly, setShowAvailableOnly] = useState(false);
  const [showQuickCreate, setShowQuickCreate] = useState(false);
  const [showCustomItem, setShowCustomItem] = useState(false);
  const [selectedCustomer, setSelectedCustomer] = useState<Customer | null>(null);
  const [searchInput, setSearchInput] = useState("");
  const [activeTab, setActiveTab] = useState<WorkspaceTab>("cart");
  const [showCustomerCreateModal, setShowCustomerCreateModal] = useState(false);
  const [customerPhone, setCustomerPhone] = useState("");
  const [selectedPaymentMethod, setSelectedPaymentMethod] = useState<PaymentMethod>("Cash");
  const [amountPaid, setAmountPaid] = useState<string>("");
  const [allowPartialPayment, setAllowPartialPayment] = useState(false);
  const [showPaymentError, setShowPaymentError] = useState(false);
  const [showDiscountInput, setShowDiscountInput] = useState(false);
  const [discountInputValue, setDiscountInputValue] = useState("");
  const [discountInputType, setDiscountInputType] = useState<"Percentage" | "Fixed">("Percentage");
  
  const searchInputRef = useRef<HTMLInputElement>(null);
  const customerPhoneRef = useRef<HTMLInputElement>(null);

  // Hooks
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
    addItem,
    clearCart,
    applyDiscount,
    removeDiscount,
  } = useCart();
  const { hasActiveShift, isLoading: isLoadingShift, currentShift } = useShift();
  const { createOrder, completeOrder, isCreating, isCompleting } = useOrders();

  // Customer search
  const [searchCustomer, { data: searchResult, isFetching: isSearchingCustomer }] =
    useLazyGetCustomerByPhoneQuery();

  // Fetch shift warnings
  const { data: warningsData } = useGetShiftWarningsQuery(undefined, {
    pollingInterval: 10 * 60 * 1000,
    skip: !hasActiveShift,
  });

  const shiftWarning = warningsData?.data;

  // Shortcuts
  usePOSShortcuts({
    onCheckout: () => {
      if (items.length > 0) {
        setActiveTab("payment");
        setAmountPaid(total.toFixed(2));
      }
    },
    onSearch: () => searchInputRef.current?.focus(),
  });

  // Auto-focus search on mount
  useEffect(() => {
    searchInputRef.current?.focus();
  }, []);

  // Auto-update payment amount when total changes
  useEffect(() => {
    if (activeTab === "payment") {
      setAmountPaid(total.toFixed(2));
    }
  }, [total, activeTab]);

  // Handle search/barcode scan
  const handleSearchSubmit = useCallback(
    (value: string) => {
      const trimmedValue = value.trim();
      if (!trimmedValue) return;

      const foundProduct = products.find(
        (p) =>
          (p.barcode && p.barcode.toLowerCase() === trimmedValue.toLowerCase()) ||
          (p.sku && p.sku.toLowerCase() === trimmedValue.toLowerCase()) ||
          p.name.toLowerCase() === trimmedValue.toLowerCase()
      );

      if (foundProduct) {
        addItem(foundProduct, 1);
        toast.success(`تمت الإضافة: ${foundProduct.name}`);
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

  // Customer search with debounce
  useEffect(() => {
    const timer = setTimeout(() => {
      if (customerPhone.length >= 8) {
        searchCustomer(customerPhone);
      }
    }, 300);
    return () => clearTimeout(timer);
  }, [customerPhone, searchCustomer]);

  // Handle customer selection
  const handleSelectCustomer = (customer: Customer) => {
    setSelectedCustomer(customer);
    setCustomerPhone("");
    toast.success(`تم اختيار العميل: ${customer.name || customer.phone}`);
  };

  const handleClearCustomer = () => {
    setSelectedCustomer(null);
    setCustomerPhone("");
  };

  // Payment handlers
  const handleNumpadClick = (value: string) => {
    if (value === "C") {
      setAmountPaid("");
    } else if (value === "←") {
      setAmountPaid((prev) => prev.slice(0, -1));
    } else if (value === ".") {
      if (!amountPaid.includes(".")) {
        setAmountPaid((prev) => prev + ".");
      }
    } else {
      setAmountPaid((prev) => prev + value);
    }
  };

  const handleQuickAmount = (amount: number) => {
    setAmountPaid(amount.toFixed(2));
  };

  // Complete payment
  const handleCompletePayment = async () => {
    const numericAmount = parseFloat(amountPaid) || 0;
    const amountDue = total - numericAmount;

    // Validate payment amount
    if (numericAmount < total && !allowPartialPayment) {
      setShowPaymentError(true);
      setTimeout(() => setShowPaymentError(false), 500);
      toast.error("المبلغ المدفوع أقل من الإجمالي");
      return;
    }

    // Validate partial payment requires customer
    if (numericAmount < total && !selectedCustomer) {
      toast.error("البيع الآجل يتطلب ربط عميل بالطلب");
      return;
    }

    // Validate customer is active
    if (numericAmount < total && selectedCustomer && !selectedCustomer.isActive) {
      toast.error("العميل غير نشط - لا يمكن البيع الآجل");
      return;
    }

    // Validate credit limit
    if (selectedCustomer && selectedCustomer.creditLimit > 0) {
      const availableCredit = selectedCustomer.creditLimit - selectedCustomer.totalDue;
      const creditLimitExceeded = amountDue > availableCredit;
      if (numericAmount < total && creditLimitExceeded) {
        toast.error(
          `تجاوز حد الائتمان. المتاح: ${formatCurrency(availableCredit)} ج.م، المطلوب: ${formatCurrency(amountDue)} ج.م`,
          { duration: 5000 }
        );
        return;
      }
    }

    try {
      // Create order
      const order = await createOrder(selectedCustomer?.id);
      if (!order) return;

      // Complete order with payment
      const completedOrder = await completeOrder(order.id, {
        payments: [{ method: selectedPaymentMethod, amount: numericAmount }],
      });

      if (completedOrder) {
        const change = numericAmount - total;
        if (change > 0) {
          toast.success(`تم إتمام الدفع! الباقي: ${formatCurrency(change)}`);
        } else if (amountDue > 0) {
          toast.success(
            `تم إتمام البيع الآجل! المبلغ المستحق: ${formatCurrency(amountDue)}`
          );
        } else {
          toast.success("تم إتمام الدفع بنجاح!");
        }

        // Reset state
        setSelectedCustomer(null);
        setCustomerPhone("");
        setAmountPaid("");
        setAllowPartialPayment(false);
        setActiveTab("cart");
      }
    } catch {
      toast.error("حدث خطأ غير متوقع");
    }
  };

  // Filter products
  let filteredProducts = products;

  if (searchInput.trim()) {
    const searchLower = searchInput.toLowerCase().trim();
    filteredProducts = filteredProducts.filter(
      (p) =>
        p.name.toLowerCase().includes(searchLower) ||
        (p.barcode && p.barcode.toLowerCase().includes(searchLower)) ||
        (p.sku && p.sku.toLowerCase().includes(searchLower))
    );
  }

  if (selectedCategory) {
    filteredProducts = filteredProducts.filter(
      (p) => p.categoryId === selectedCategory
    );
  }

  if (showAvailableOnly) {
    filteredProducts = filteredProducts.filter((p) => {
      if (!p.trackInventory) return true;
      return getProductCurrentStock(p) > 0;
    });
  }

  // Loading state
  if (isLoading || isLoadingShift) {
    return (
      <div className="h-full flex items-center justify-center bg-gray-50">
        <Loading />
      </div>
    );
  }

  // No active shift warning
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

  // Payment calculations
  const numericAmount = parseFloat(amountPaid) || 0;
  const change = numericAmount - total;
  const amountDue = total - numericAmount;

  // Calculate available credit for customer
  const availableCredit = selectedCustomer
    ? selectedCustomer.creditLimit - selectedCustomer.totalDue
    : 0;

  const canTakeCredit =
    selectedCustomer &&
    selectedCustomer.isActive &&
    (selectedCustomer.creditLimit === 0 ||
      amountDue <= availableCredit);

  const creditLimitExceeded =
    selectedCustomer &&
    selectedCustomer.creditLimit > 0 &&
    amountDue > availableCredit;

  const paymentMethods: {
    id: PaymentMethod;
    label: string;
    icon: React.ReactNode;
  }[] = [
    { id: "Cash", label: "نقدي", icon: <Banknote className="w-6 h-6" /> },
    { id: "Card", label: "بطاقة", icon: <CreditCard className="w-6 h-6" /> },
    { id: "Fawry", label: "فوري", icon: <Building2 className="w-6 h-6" /> },
  ];

  const quickAmounts = [50, 100, 200, 500];

  return (
    <div className="h-full flex flex-col bg-gray-50">
      {/* Shift Warning Banner */}
      {shiftWarning && shiftWarning.shouldWarn && (
        <div className="bg-warning-50 border-b border-warning-200 px-6 py-3">
          <div className="flex items-center gap-3">
            <AlertCircle className="w-5 h-5 text-warning-600 shrink-0" />
            <div className="flex-1">
              <p className="text-sm font-medium text-warning-800">
                {shiftWarning.message}
              </p>
              {shiftWarning.hoursOpen && (
                <p className="text-xs text-warning-600">
                  الوردية مفتوحة منذ {shiftWarning.hoursOpen.toFixed(1)} ساعة
                </p>
              )}
            </div>
          </div>
        </div>
      )}

      {/* Main Area */}
      <div className="flex-1 flex overflow-hidden">
        {/* Left: Product Explorer (60%) */}
        <div className="flex-1 flex flex-col p-4 min-w-0">
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
                className="w-full pl-4 pr-10 py-3 border border-gray-200 rounded-xl bg-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 text-sm shadow-sm"
                autoComplete="off"
              />
            </div>
          </div>

          {/* Categories */}
          <div className="mb-4">
            <CategoryChips
              categories={categories}
              selectedId={selectedCategory}
              onSelect={setSelectedCategory}
            />
          </div>

          {/* Filters Row */}
          <div className="flex items-center gap-2 mb-4">
            <button
              onClick={() => setShowAvailableOnly(!showAvailableOnly)}
              className={clsx(
                "flex items-center gap-1.5 px-4 py-2 rounded-lg text-sm font-medium transition-all",
                showAvailableOnly
                  ? "bg-success-600 text-white shadow-md"
                  : "bg-white text-gray-600 border-2 border-gray-200 hover:border-success-300"
              )}
            >
              <PackageCheck className="w-4 h-4" />
              <span>المتاح فقط</span>
            </button>

            <button
              onClick={() => setShowQuickCreate(true)}
              className="flex items-center gap-1.5 px-4 py-2 rounded-lg text-sm font-medium bg-white text-gray-700 border-2 border-gray-200 hover:border-primary-300 hover:bg-primary-50 transition-all"
            >
              <PlusCircle className="w-4 h-4" />
              <span>منتج جديد</span>
            </button>

            <button
              onClick={() => setShowCustomItem(true)}
              disabled={itemsCount === 0}
              className={clsx(
                "flex items-center gap-1.5 px-4 py-2 rounded-lg text-sm font-medium transition-all",
                itemsCount > 0
                  ? "bg-white text-gray-700 border-2 border-gray-200 hover:border-orange-300 hover:bg-orange-50"
                  : "bg-gray-200 text-gray-400 cursor-not-allowed border-2 border-gray-200"
              )}
              title={itemsCount > 0 ? "إضافة منتج مخصص للطلب الحالي" : "ابدأ طلب أولاً"}
            >
              <FileText className="w-4 h-4" />
              <span>منتج مخصص</span>
            </button>
          </div>

          {/* Products List */}
          <div className="flex-1 overflow-y-auto scrollbar-thin">
            <ProductListView products={filteredProducts} categories={categories} />
          </div>
        </div>

        {/* Right: Transaction Workspace (40%) */}
        <div className="w-[40%] bg-white border-l border-gray-200 flex flex-col">
          {/* Tabs */}
          <div className="flex border-b border-gray-200">
            <button
              onClick={() => setActiveTab("cart")}
              className={clsx(
                "flex-1 flex items-center justify-center gap-2 py-3 text-sm font-medium transition-colors relative",
                activeTab === "cart"
                  ? "text-primary-600 bg-primary-50"
                  : "text-gray-600 hover:bg-gray-50"
              )}
            >
              <ShoppingCart className="w-4 h-4" />
              <span>السلة</span>
              {itemsCount > 0 && (
                <span className="absolute top-1 right-1 bg-primary-600 text-white text-xs w-5 h-5 rounded-full flex items-center justify-center">
                  {itemsCount}
                </span>
              )}
              {activeTab === "cart" && (
                <div className="absolute bottom-0 left-0 right-0 h-0.5 bg-primary-600" />
              )}
            </button>

            <button
              onClick={() => setActiveTab("customer")}
              className={clsx(
                "flex-1 flex items-center justify-center gap-2 py-3 text-sm font-medium transition-colors relative",
                activeTab === "customer"
                  ? "text-primary-600 bg-primary-50"
                  : "text-gray-600 hover:bg-gray-50"
              )}
            >
              <User className="w-4 h-4" />
              <span>العميل</span>
              {selectedCustomer && (
                <div className="w-2 h-2 bg-success-500 rounded-full" />
              )}
              {activeTab === "customer" && (
                <div className="absolute bottom-0 left-0 right-0 h-0.5 bg-primary-600" />
              )}
            </button>

            <button
              onClick={() => {
                setActiveTab("payment");
                setAmountPaid(total.toFixed(2));
              }}
              disabled={items.length === 0}
              className={clsx(
                "flex-1 flex items-center justify-center gap-2 py-3 text-sm font-medium transition-colors relative",
                activeTab === "payment"
                  ? "text-primary-600 bg-primary-50"
                  : "text-gray-600 hover:bg-gray-50",
                items.length === 0 && "opacity-50 cursor-not-allowed"
              )}
            >
              <CreditCard className="w-4 h-4" />
              <span>الدفع</span>
              {activeTab === "payment" && (
                <div className="absolute bottom-0 left-0 right-0 h-0.5 bg-primary-600" />
              )}
            </button>

            <button
              onClick={() => setActiveTab("summary")}
              disabled={items.length === 0}
              className={clsx(
                "flex-1 flex items-center justify-center gap-2 py-3 text-sm font-medium transition-colors relative",
                activeTab === "summary"
                  ? "text-primary-600 bg-primary-50"
                  : "text-gray-600 hover:bg-gray-50",
                items.length === 0 && "opacity-50 cursor-not-allowed"
              )}
            >
              <Receipt className="w-4 h-4" />
              <span>الملخص</span>
              {activeTab === "summary" && (
                <div className="absolute bottom-0 left-0 right-0 h-0.5 bg-primary-600" />
              )}
            </button>
          </div>

          {/* Tab Content */}
          <div className="flex-1 overflow-y-auto p-4">
            {/* Cart Tab */}
            {activeTab === "cart" && (
              <div className="space-y-4">
                {items.length === 0 ? (
                  <div className="flex flex-col items-center justify-center h-full text-gray-400 py-12">
                    <div className="w-20 h-20 rounded-full bg-gray-100 flex items-center justify-center mb-4">
                      <ShoppingCart className="w-10 h-10" />
                    </div>
                    <p className="text-lg font-medium">السلة فارغة</p>
                    <p className="text-sm">اضغط على المنتجات لإضافتها</p>
                  </div>
                ) : (
                  <>
                    <div className="flex items-center justify-between mb-4">
                      <h3 className="text-lg font-bold text-gray-800">
                        العناصر ({itemsCount})
                      </h3>
                      <button
                        onClick={clearCart}
                        className="flex items-center gap-1 text-danger-500 text-sm hover:underline"
                      >
                        <Trash2 className="w-4 h-4" />
                        إفراغ
                      </button>
                    </div>

                    <div className="space-y-3">
                      {items.map((item) => (
                        <CartItemComponent key={item.product.id} item={item} />
                      ))}
                    </div>

                    {/* Discount Section */}
                    <div className="pt-4 border-t space-y-3">
                      <p className="text-sm font-medium text-gray-700">الخصم</p>
                      
                      {discountAmount === 0 ? (
                        showDiscountInput ? (
                          <div className="space-y-3">
                            {/* Discount Type Selector */}
                            <div className="grid grid-cols-2 gap-2">
                              <button
                                onClick={() => setDiscountInputType("Percentage")}
                                className={clsx(
                                  "py-2 px-3 rounded-lg text-sm font-medium transition-all",
                                  discountInputType === "Percentage"
                                    ? "bg-primary-600 text-white"
                                    : "bg-gray-100 text-gray-700 hover:bg-gray-200"
                                )}
                              >
                                نسبة %
                              </button>
                              <button
                                onClick={() => setDiscountInputType("Fixed")}
                                className={clsx(
                                  "py-2 px-3 rounded-lg text-sm font-medium transition-all",
                                  discountInputType === "Fixed"
                                    ? "bg-success-600 text-white"
                                    : "bg-gray-100 text-gray-700 hover:bg-gray-200"
                                )}
                              >
                                مبلغ ثابت
                              </button>
                            </div>

                            {/* Input */}
                            <input
                              type="number"
                              value={discountInputValue === "0" ? "" : discountInputValue}
                              onChange={(e) => setDiscountInputValue(e.target.value)}
                              placeholder={discountInputType === "Percentage" ? "0-100" : "0.00"}
                              className="w-full px-4 py-2 border-2 border-gray-300 rounded-lg focus:border-primary-500 focus:outline-none"
                              autoFocus
                            />

                            {/* Actions */}
                            <div className="flex gap-2">
                              <button
                                onClick={() => {
                                  const value = parseFloat(discountInputValue);
                                  if (!isNaN(value) && value > 0) {
                                    if (discountInputType === "Percentage" && value <= 100) {
                                      applyDiscount("Percentage", value);
                                      toast.success(`تم تطبيق خصم ${value}%`);
                                      setShowDiscountInput(false);
                                      setDiscountInputValue("");
                                    } else if (discountInputType === "Fixed") {
                                      applyDiscount("Fixed", value);
                                      toast.success(`تم تطبيق خصم ${formatCurrency(value)}`);
                                      setShowDiscountInput(false);
                                      setDiscountInputValue("");
                                    } else {
                                      toast.error("النسبة يجب أن تكون بين 0 و 100");
                                    }
                                  }
                                }}
                                className="flex-1 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 font-medium"
                              >
                                تطبيق
                              </button>
                              <button
                                onClick={() => {
                                  setShowDiscountInput(false);
                                  setDiscountInputValue("");
                                }}
                                className="px-4 py-2 bg-gray-200 text-gray-700 rounded-lg hover:bg-gray-300"
                              >
                                إلغاء
                              </button>
                            </div>
                          </div>
                        ) : (
                          <button
                            onClick={() => setShowDiscountInput(true)}
                            className="w-full flex items-center justify-center gap-2 py-3 border-2 border-dashed border-gray-300 rounded-lg text-gray-600 hover:border-primary-500 hover:bg-primary-50 transition-colors"
                          >
                            <Tag className="w-5 h-5" />
                            <span className="font-medium">إضافة خصم</span>
                          </button>
                        )
                      ) : (
                        <div className="p-3 bg-success-50 border border-success-200 rounded-lg">
                          <div className="flex items-center justify-between mb-2">
                            <span className="text-sm font-medium text-success-700">
                              {discountType === "Percentage" ? `خصم ${discountValue}%` : "خصم ثابت"}
                            </span>
                            <button
                              onClick={() => {
                                removeDiscount();
                                toast.success("تم إلغاء الخصم");
                              }}
                              className="p-1 text-danger-500 hover:bg-danger-100 rounded transition-colors"
                            >
                              <XIcon className="w-4 h-4" />
                            </button>
                          </div>
                          <div className="text-lg font-bold text-success-600">
                            - {formatCurrency(discountAmount)}
                          </div>
                        </div>
                      )}
                    </div>
                  </>
                )}
              </div>
            )}

            {/* Customer Tab */}
            {activeTab === "customer" && (
              <div className="space-y-4">
                <h3 className="text-lg font-bold text-gray-800 mb-4">
                  معلومات العميل
                </h3>

                {selectedCustomer ? (
                  <div className="bg-primary-50 border border-primary-200 rounded-xl p-4">
                    <div className="flex items-start justify-between mb-4">
                      <div className="flex items-center gap-3">
                        <div className="w-12 h-12 bg-primary-600 rounded-full flex items-center justify-center">
                          <User className="w-6 h-6 text-white" />
                        </div>
                        <div>
                          <p className="font-bold text-gray-800 text-lg">
                            {selectedCustomer.name || "عميل"}
                          </p>
                          <p className="text-sm text-gray-600 flex items-center gap-1">
                            <Phone className="w-3 h-3" />
                            {selectedCustomer.phone}
                          </p>
                        </div>
                      </div>
                      <button
                        onClick={handleClearCustomer}
                        className="p-2 text-gray-400 hover:text-danger-500 hover:bg-danger-50 rounded-lg transition-colors"
                      >
                        <XIcon className="w-5 h-5" />
                      </button>
                    </div>

                    <div className="space-y-3">
                      {(selectedCustomer.loyaltyPoints ?? 0) > 0 && (
                        <div className="flex items-center justify-between p-3 bg-amber-50 rounded-lg">
                          <span className="text-sm text-gray-700 flex items-center gap-2">
                            <Star className="w-4 h-4 text-amber-500 fill-current" />
                            نقاط الولاء
                          </span>
                          <span className="font-bold text-amber-600">
                            {selectedCustomer.loyaltyPoints}
                          </span>
                        </div>
                      )}

                      {selectedCustomer.totalDue > 0 && (
                        <div className="flex items-center justify-between p-3 bg-orange-50 rounded-lg">
                          <span className="text-sm text-gray-700 flex items-center gap-2">
                            <AlertCircle className="w-4 h-4 text-orange-500" />
                            رصيد مستحق
                          </span>
                          <span className="font-bold text-orange-600">
                            {formatCurrency(selectedCustomer.totalDue)}
                          </span>
                        </div>
                      )}

                      {selectedCustomer.creditLimit > 0 && (
                        <div className="flex items-center justify-between p-3 bg-blue-50 rounded-lg">
                          <span className="text-sm text-gray-700">حد الائتمان</span>
                          <span className="font-bold text-blue-600">
                            {formatCurrency(selectedCustomer.creditLimit)}
                          </span>
                        </div>
                      )}
                    </div>
                  </div>
                ) : (
                  <div className="space-y-4">
                    <div className="relative">
                      <input
                        ref={customerPhoneRef}
                        type="text"
                        value={customerPhone}
                        onChange={(e) =>
                          setCustomerPhone(e.target.value.replace(/[^0-9]/g, ""))
                        }
                        placeholder="🔍 ابحث برقم الهاتف..."
                        className="w-full px-4 py-3 border border-gray-200 rounded-xl focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
                        dir="ltr"
                      />
                      {isSearchingCustomer && (
                        <div className="absolute left-3 top-1/2 -translate-y-1/2">
                          <div className="w-5 h-5 border-2 border-primary-500 border-t-transparent rounded-full animate-spin" />
                        </div>
                      )}
                    </div>

                    {/* Search Result */}
                    {customerPhone.length >= 8 &&
                      !isSearchingCustomer &&
                      searchResult?.data && (
                        <div
                          onClick={() => handleSelectCustomer(searchResult.data!)}
                          className="bg-success-50 border border-success-200 rounded-xl p-4 cursor-pointer hover:bg-success-100 transition-colors"
                        >
                          <div className="flex items-center gap-3">
                            <div className="w-10 h-10 bg-success-600 rounded-full flex items-center justify-center">
                              <User className="w-5 h-5 text-white" />
                            </div>
                            <div className="flex-1">
                              <p className="font-semibold text-gray-800">
                                {searchResult.data.name || "عميل"}
                              </p>
                              <p className="text-sm text-gray-600">
                                {searchResult.data.phone}
                              </p>
                            </div>
                            <span className="text-xs text-success-600 font-medium">
                              اضغط للاختيار
                            </span>
                          </div>
                        </div>
                      )}

                    {/* Not Found */}
                    {customerPhone.length >= 8 &&
                      !isSearchingCustomer &&
                      !searchResult?.data && (
                        <div className="bg-gray-50 border border-gray-200 rounded-xl p-4">
                          <p className="text-sm text-gray-500 mb-3">
                            لم يتم العثور على عميل
                          </p>
                          <button
                            onClick={() => setShowCustomerCreateModal(true)}
                            className="w-full flex items-center justify-center gap-2 py-2.5 bg-primary-600 text-white rounded-lg hover:bg-primary-700 transition-colors"
                          >
                            <Plus className="w-5 h-5" />
                            <span className="font-medium">إضافة عميل جديد</span>
                          </button>
                        </div>
                      )}

                    <div className="text-center text-sm text-gray-500 py-8">
                      <User className="w-12 h-12 mx-auto mb-2 text-gray-300" />
                      <p>ابحث عن عميل أو اترك الحقل فارغاً للبيع النقدي</p>
                    </div>
                  </div>
                )}
              </div>
            )}

            {/* Payment Tab */}
            {activeTab === "payment" && (
              <div className="space-y-4">
                <h3 className="text-lg font-bold text-gray-800 mb-4">الدفع</h3>

                {/* Total Amount */}
                <div className="text-center p-6 bg-gradient-to-br from-primary-50 to-primary-100 rounded-xl border border-primary-200">
                  <p className="text-sm text-gray-600 mb-1">المبلغ المطلوب</p>
                  <p className="text-4xl font-bold text-primary-600">
                    {formatCurrency(total)}
                  </p>
                </div>

                {/* Payment Methods */}
                <div>
                  <p className="text-sm font-medium text-gray-700 mb-3">
                    طريقة الدفع
                  </p>
                  <div className="grid grid-cols-3 gap-3">
                    {paymentMethods.map((method) => (
                      <button
                        key={method.id}
                        onClick={() => setSelectedPaymentMethod(method.id)}
                        className={clsx(
                          "flex flex-col items-center gap-2 p-4 rounded-xl border-2 transition-all",
                          selectedPaymentMethod === method.id
                            ? "border-primary-600 bg-primary-50 text-primary-600"
                            : "border-gray-200 hover:border-gray-300"
                        )}
                      >
                        {method.icon}
                        <span className="font-medium text-sm">{method.label}</span>
                      </button>
                    ))}
                  </div>
                </div>

                {/* Amount Input (for Cash) */}
                {selectedPaymentMethod === "Cash" && (
                  <div className="space-y-4">
                    <div>
                      <p className="text-sm font-medium text-gray-700 mb-3">
                        المبلغ المدفوع
                      </p>
                      <div
                        className={clsx(
                          "text-center p-4 bg-gray-50 rounded-xl transition-all",
                          showPaymentError && "animate-shake border-2 border-danger-500"
                        )}
                      >
                        <p className="text-3xl font-bold">
                          {amountPaid || "0"}{" "}
                          <span className="text-lg text-gray-400">ج.م</span>
                        </p>
                      </div>
                    </div>

                    {/* Quick Amounts */}
                    <div className="flex gap-2">
                      {quickAmounts.map((amount) => (
                        <button
                          key={amount}
                          onClick={() => handleQuickAmount(amount)}
                          className="flex-1 py-2 rounded-lg bg-gray-100 font-medium hover:bg-primary-100 hover:text-primary-600 transition-colors text-sm"
                        >
                          {amount}
                        </button>
                      ))}
                      <button
                        onClick={() => handleQuickAmount(total)}
                        className="flex-1 py-2 rounded-lg bg-primary-600 text-white font-medium hover:bg-primary-700 transition-colors text-sm"
                      >
                        تمام
                      </button>
                    </div>

                    {/* Numpad */}
                    <div className="grid grid-cols-4 gap-2">
                      {[
                        "7",
                        "8",
                        "9",
                        "←",
                        "4",
                        "5",
                        "6",
                        "C",
                        "1",
                        "2",
                        "3",
                        ".",
                        "0",
                        "00",
                      ].map((key) => (
                        <button
                          key={key}
                          onClick={() => handleNumpadClick(key)}
                          className={clsx(
                            "h-12 rounded-lg bg-gray-100 font-semibold text-lg hover:bg-gray-200 active:bg-gray-300 transition-colors",
                            key === "0" && "col-span-2"
                          )}
                        >
                          {key}
                        </button>
                      ))}
                    </div>

                    {/* Change */}
                    {change > 0 && (
                      <div className="text-center p-4 bg-success-50 rounded-xl border border-success-200">
                        <p className="text-sm text-gray-600">الباقي</p>
                        <p className="text-2xl font-bold text-success-600">
                          {formatCurrency(change)}
                        </p>
                      </div>
                    )}

                    {/* Amount Due */}
                    {numericAmount < total && numericAmount > 0 && (
                      <div
                        className={clsx(
                          "text-center p-4 rounded-xl border",
                          creditLimitExceeded
                            ? "bg-danger-50 border-danger-200"
                            : "bg-orange-50 border-orange-200"
                        )}
                      >
                        <p className="text-sm text-gray-600">المبلغ المستحق</p>
                        <p
                          className={clsx(
                            "text-2xl font-bold",
                            creditLimitExceeded
                              ? "text-danger-600"
                              : "text-orange-600"
                          )}
                        >
                          {formatCurrency(amountDue)}
                        </p>
                        {creditLimitExceeded && (
                          <p className="text-xs text-danger-600 mt-1">
                            تجاوز حد الائتمان - المتاح: {formatCurrency(availableCredit)}
                          </p>
                        )}
                        {selectedCustomer && !selectedCustomer.isActive && (
                          <p className="text-xs text-danger-600 mt-1">
                            العميل غير نشط
                          </p>
                        )}
                      </div>
                    )}
                  </div>
                )}

                {/* Partial Payment Option */}
                {selectedCustomer && canTakeCredit && selectedPaymentMethod === "Cash" && (
                  <div className="flex items-start gap-3 p-4 bg-blue-50 rounded-xl border border-blue-200">
                    <input
                      type="checkbox"
                      id="partialPayment"
                      checked={allowPartialPayment}
                      onChange={(e) => setAllowPartialPayment(e.target.checked)}
                      className="w-5 h-5 text-primary-600 rounded focus:ring-2 focus:ring-primary-500 mt-0.5"
                    />
                    <label htmlFor="partialPayment" className="flex-1 cursor-pointer">
                      <p className="font-medium text-gray-800 text-sm">
                        السماح بالدفع الجزئي (بيع آجل)
                      </p>
                      <p className="text-xs text-gray-600 mt-1">
                        يمكن للعميل دفع جزء من المبلغ والباقي يُسجل كدين
                      </p>
                    </label>
                  </div>
                )}
              </div>
            )}

            {/* Summary Tab */}
            {activeTab === "summary" && (
              <div className="space-y-4">
                <h3 className="text-lg font-bold text-gray-800 mb-4">
                  ملخص الطلب
                </h3>

                {/* Customer Info */}
                <div className="p-4 bg-gray-50 rounded-xl border border-gray-200">
                  <p className="text-xs font-medium text-gray-500 mb-2">العميل</p>
                  {selectedCustomer ? (
                    <div className="flex items-center gap-2">
                      <User className="w-4 h-4 text-primary-500" />
                      <span className="font-medium text-gray-800">
                        {selectedCustomer.name || selectedCustomer.phone}
                      </span>
                    </div>
                  ) : (
                    <div className="flex items-center gap-2 text-gray-500">
                      <User className="w-4 h-4" />
                      <span>عميل نقدي</span>
                    </div>
                  )}
                </div>

                {/* Items Summary */}
                <div className="p-4 bg-gray-50 rounded-xl border border-gray-200">
                  <p className="text-xs font-medium text-gray-500 mb-3">
                    العناصر ({itemsCount})
                  </p>
                  <div className="space-y-2 max-h-48 overflow-y-auto scrollbar-thin">
                    {items.map((item) => (
                      <div
                        key={item.product.id}
                        className="flex items-center justify-between text-sm"
                      >
                        <div className="flex items-center gap-2">
                          <span className="font-medium text-gray-700">
                            {item.quantity}x
                          </span>
                          <span className="text-gray-600">{item.product.name}</span>
                        </div>
                        <span className="font-semibold text-gray-800">
                          {formatCurrency(item.product.price * item.quantity)}
                        </span>
                      </div>
                    ))}
                  </div>
                </div>

                {/* Financial Summary */}
                <div className="p-4 bg-gray-50 rounded-xl border border-gray-200 space-y-3">
                  <div className="flex justify-between text-sm">
                    <span className="text-gray-600">المجموع الفرعي</span>
                    <span className="font-semibold text-gray-800">
                      {formatCurrency(subtotal)}
                    </span>
                  </div>

                  {discountAmount > 0 && (
                    <div className="flex justify-between text-sm">
                      <span className="text-success-600 flex items-center gap-1">
                        <Tag className="w-4 h-4" />
                        الخصم
                        {discountType === "Percentage" &&
                          discountValue &&
                          ` (${discountValue}%)`}
                      </span>
                      <span className="font-semibold text-success-600">
                        - {formatCurrency(discountAmount)}
                      </span>
                    </div>
                  )}

                  {isTaxEnabled && (
                    <div className="flex justify-between text-sm">
                      <span className="text-gray-600">الضريبة ({taxRate}%)</span>
                      <span className="font-semibold text-gray-800">
                        {formatCurrency(taxAmount)}
                      </span>
                    </div>
                  )}

                  <div className="pt-3 border-t border-gray-300 flex justify-between">
                    <span className="font-bold text-gray-800">الإجمالي</span>
                    <span className="text-2xl font-bold text-primary-600">
                      {formatCurrency(total)}
                    </span>
                  </div>
                </div>

                {/* Payment Method */}
                <div className="p-4 bg-gray-50 rounded-xl border border-gray-200">
                  <p className="text-xs font-medium text-gray-500 mb-2">
                    طريقة الدفع
                  </p>
                  <div className="flex items-center gap-2">
                    {selectedPaymentMethod === "Cash" && (
                      <Banknote className="w-5 h-5 text-success-600" />
                    )}
                    {selectedPaymentMethod === "Card" && (
                      <CreditCard className="w-5 h-5 text-primary-600" />
                    )}
                    {selectedPaymentMethod === "Fawry" && (
                      <Building2 className="w-5 h-5 text-secondary-600" />
                    )}
                    <span className="font-medium text-gray-800">
                      {paymentMethods.find((m) => m.id === selectedPaymentMethod)
                        ?.label}
                    </span>
                  </div>
                </div>
              </div>
            )}
          </div>

          {/* Sticky Total Bar */}
          <div className="border-t border-gray-200 p-4 bg-white">
            <div className="flex items-center justify-between mb-3">
              <span className="text-sm text-gray-600">الإجمالي</span>
              <span className="text-2xl font-bold text-primary-600">
                {formatCurrency(total)}
              </span>
            </div>

            {/* Complete Payment Button */}
            {activeTab === "payment" && items.length > 0 && (
              <Button
                variant="success"
                size="xl"
                className="w-full"
                onClick={handleCompletePayment}
                isLoading={isCreating || isCompleting}
                disabled={
                  isCreating ||
                  isCompleting ||
                  (numericAmount < total && !allowPartialPayment) ||
                  (numericAmount < total && creditLimitExceeded)
                }
                rightIcon={<Check className="w-5 h-5" />}
              >
                {isCreating
                  ? "جاري إنشاء الطلب..."
                  : isCompleting
                  ? "جاري الدفع..."
                  : numericAmount < total && allowPartialPayment
                  ? `إتمام البيع الآجل (مستحق: ${formatCurrency(amountDue)})`
                  : "إتمام الدفع"}
              </Button>
            )}

            {/* Checkout Button (for other tabs) */}
            {activeTab !== "payment" && items.length > 0 && (
              <Button
                variant="success"
                size="xl"
                className="w-full"
                onClick={() => {
                  setActiveTab("payment");
                  setAmountPaid(total.toFixed(2));
                }}
                rightIcon={<CreditCard className="w-5 h-5" />}
              >
                💳 الدفع {formatCurrency(total)}
              </Button>
            )}
          </div>
        </div>
      </div>

      {/* Modals */}
      {showQuickCreate && (
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
            // Add custom item to cart directly
            const customProduct = {
              id: -Date.now(), // Temporary negative ID for custom items
              name: item.name,
              price: item.unitPrice,
              taxRate: item.taxRate ?? taxRate,
              categoryId: 0,
              isActive: true,
              trackInventory: false,
              type: 2, // Service type
              currentBranchStock: null,
            };
            addItem(customProduct as any, item.quantity);
            toast.success(`تم إضافة: ${item.name}`);
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
    </div>
  );
};

export default POSWorkspacePage;
