import { useState, useEffect } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { ChevronDown } from "lucide-react";
import {
  useCreatePurchaseInvoiceMutation,
  useUpdatePurchaseInvoiceMutation,
  useGetPurchaseInvoiceByIdQuery,
} from "../../api/purchaseInvoiceApi";
import { useGetSuppliersQuery } from "../../api/suppliersApi";
import { useGetProductsQuery } from "../../api/productsApi";
import { Button } from "../../components/common/Button";
import { Card } from "../../components/common/Card";
import { Loading } from "../../components/common/Loading";
import { QuickAddProductModal } from "../../components/purchase-invoices/QuickAddProductModal";
import { formatCurrency } from "../../utils/formatters";
import { toast } from "sonner";
import type { CreatePurchaseInvoiceItemRequest } from "../../types/purchaseInvoice.types";
import { getApiErrorCode, handleApiError } from "../../utils/errorHandler";
import { extractApiData } from "@/utils/apiResponse";

interface InvoiceItem extends CreatePurchaseInvoiceItemRequest {
  tempId: string;
  productName?: string;
  productType?: number; // ProductType enum
}

export function PurchaseInvoiceFormPage() {
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const isEditMode = !!id;

  const [supplierId, setSupplierId] = useState<number>(0);
  const [invoiceDate, setInvoiceDate] = useState<string>(
    new Date().toISOString().split("T")[0],
  );
  const [notes, setNotes] = useState<string>("");
  const [items, setItems] = useState<InvoiceItem[]>([]);
  const [selectedProductId, setSelectedProductId] = useState<number>(0);
  const [quantity, setQuantity] = useState<string>("");
  const [purchasePrice, setPurchasePrice] = useState<string>("");
  const [sellingPrice, setSellingPrice] = useState<string>("");
  const [itemNotes, setItemNotes] = useState<string>("");
  const [showQuickAddProduct, setShowQuickAddProduct] = useState(false);

  const { data: suppliersResponse } = useGetSuppliersQuery();
  const { data: productsResponse } = useGetProductsQuery();
  const { data: invoiceResponse, isLoading: isLoadingInvoice } =
    useGetPurchaseInvoiceByIdQuery(Number(id), { skip: !isEditMode });

  const [createInvoice, { isLoading: isCreating }] =
    useCreatePurchaseInvoiceMutation();
  const [updateInvoice, { isLoading: isUpdating }] =
    useUpdatePurchaseInvoiceMutation();

  const suppliers = suppliersResponse?.data || [];
  const products = productsResponse?.data || [];
  const invoice = invoiceResponse?.data;

  // Load invoice data in edit mode
  useEffect(() => {
    if (invoice && isEditMode) {
      setSupplierId(invoice.supplierId);
      setInvoiceDate(invoice.invoiceDate.split("T")[0]);
      setNotes(invoice.notes || "");
      setItems(
        invoice.items.map((item) => ({
          tempId: `item-${item.id}`,
          productId: item.productId,
          productName: item.productName,
          quantity: item.quantity,
          purchasePrice: item.purchasePrice,
          sellingPrice: item.sellingPrice,
          notes: item.notes,
        })),
      );
    }
  }, [invoice, isEditMode]);

  const handleAddItem = () => {
    const numQuantity = Number(quantity) || 0;
    const numPurchasePrice = Number(purchasePrice) || 0;
    const numSellingPrice = Number(sellingPrice) || 0;

    if (!selectedProductId || numQuantity <= 0 || numPurchasePrice <= 0) {
      toast.error("يرجى ملء جميع بيانات المنتج");
      return;
    }

    if (numSellingPrice <= 0) {
      toast.error("يرجى إدخال سعر البيع");
      return;
    }

    const product = products.find((p) => p.id === selectedProductId);
    if (!product) return;

    const newItem: InvoiceItem = {
      tempId: `temp-${Date.now()}`,
      productId: selectedProductId,
      productName: product.name,
      productType: product.type,
      quantity: numQuantity,
      purchasePrice: numPurchasePrice,
      sellingPrice: numSellingPrice,
      notes: itemNotes,
    };

    setItems([...items, newItem]);
    setSelectedProductId(0);
    setQuantity("");
    setPurchasePrice("");
    setSellingPrice("");
    setItemNotes("");
  };

  const handleProductCreated = (productId: number) => {
    setSelectedProductId(productId);
    // Auto-fill selling price from newly created product
    const product = products.find((p) => p.id === productId);
    if (product) {
      setSellingPrice(String(product.price));
    }
    toast.success("تم إضافة المنتج. يمكنك الآن إضافته للفاتورة");
  };

  const handleRemoveItem = (tempId: string) => {
    setItems(items.filter((item) => item.tempId !== tempId));
  };

  const calculateSubtotal = () => {
    return items.reduce(
      (sum, item) => sum + item.quantity * item.purchasePrice,
      0,
    );
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!supplierId) {
      toast.error("يرجى اختيار المورد");
      return;
    }

    if (items.length === 0) {
      toast.error("يرجى إضافة منتج واحد على الأقل");
      return;
    }

    const requestData = {
      supplierId,
      invoiceDate: new Date(invoiceDate).toISOString(),
      items: items.map((item) => ({
        productId: item.productId,
        quantity: item.quantity,
        purchasePrice: item.purchasePrice,
        sellingPrice: item.sellingPrice,
        notes: item.notes,
      })),
      notes,
    };

    try {
      if (isEditMode) {
        await updateInvoice({
          id: Number(id),
          data: requestData,
        }).unwrap();

        toast.success("تم تحديث الفاتورة بنجاح");
        navigate(`/purchase-invoices/${id}`);
      } else {
        const response = await createInvoice(requestData).unwrap();
        const createdInvoice = extractApiData(
          response,
          "PURCHASE_INVOICE_CREATE_EMPTY_RESPONSE",
          "فشل إنشاء الفاتورة",
        );

        toast.success("تم إنشاء الفاتورة بنجاح");
        navigate(`/purchase-invoices/${createdInvoice.id}`);
      }
    } catch (error) {
      console.error("Error saving invoice:", error);
      const errorCode = getApiErrorCode(error);
      if (errorCode) {
        toast.error(handleApiError({ data: { errorCode } }));
        return;
      }
      toast.error(handleApiError(error));
    }
  };

  if (isEditMode && isLoadingInvoice) return <Loading />;

  return (
    <div className="p-6 pb-20">
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-2xl font-bold">
          {isEditMode ? "تعديل فاتورة الشراء" : "إنشاء فاتورة شراء جديدة"}
        </h1>
        <Button
          variant="outline"
          onClick={() => navigate("/purchase-invoices")}
        >
          رجوع
        </Button>
      </div>

      <form onSubmit={handleSubmit}>
        {/* Invoice Header */}
        <Card className="mb-6">
          <h2 className="text-lg font-semibold mb-4">بيانات الفاتورة</h2>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div>
              <label className="block text-sm font-medium mb-1">
                المورد <span className="text-red-500">*</span>
              </label>
              <div className="relative">
                <select
                  value={supplierId}
                  onChange={(e) => setSupplierId(Number(e.target.value))}
                  className="w-full appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 hover:border-gray-400 transition-all duration-200 shadow-sm"
                  required
                >
                  <option value={0}>اختر المورد</option>
                  {suppliers.map((supplier) => (
                    <option key={supplier.id} value={supplier.id}>
                      {supplier.name}
                    </option>
                  ))}
                </select>
                <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium mb-1">
                تاريخ الفاتورة <span className="text-red-500">*</span>
              </label>
              <input
                type="date"
                value={invoiceDate}
                onChange={(e) => setInvoiceDate(e.target.value)}
                className="w-full px-3 py-2 border rounded-lg"
                required
              />
            </div>

            <div>
              <label className="block text-sm font-medium mb-1">ملاحظات</label>
              <input
                type="text"
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                className="w-full px-3 py-2 border rounded-lg"
                placeholder="ملاحظات اختيارية"
              />
            </div>
          </div>
        </Card>

        {/* Add Item Section */}
        <Card className="mb-6">
          <div className="flex justify-between items-center mb-4">
            <h2 className="text-lg font-semibold">إضافة منتج</h2>
            <Button
              type="button"
              variant="outline"
              onClick={() => setShowQuickAddProduct(true)}
            >
              + منتج جديد
            </Button>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-6 gap-4">
            <div className="md:col-span-2">
              <label className="block text-sm font-medium mb-1">المنتج</label>
              <div className="relative">
                <select
                  value={selectedProductId}
                  onChange={(e) => {
                    const productId = Number(e.target.value);
                    setSelectedProductId(productId);
                    // Auto-fill selling price from product
                    const product = products.find((p) => p.id === productId);
                    if (product) {
                      setSellingPrice(String(product.price));
                    }
                  }}
                  className="w-full appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 hover:border-gray-400 transition-all duration-200 shadow-sm"
                >
                  <option value={0}>اختر المنتج</option>
                  {products.map((product) => (
                    <option key={product.id} value={product.id}>
                      {product.name} {product.sku && `(${product.sku})`}
                    </option>
                  ))}
                </select>
                <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium mb-1">الكمية</label>
              <input
                type="number"
                value={quantity}
                onChange={(e) => setQuantity(e.target.value)}
                className="w-full px-3 py-2 border rounded-lg"
                min="1"
                placeholder="1"
              />
            </div>

            <div>
              <label className="block text-sm font-medium mb-1">
                سعر الشراء
              </label>
              <input
                type="number"
                value={purchasePrice}
                onChange={(e) => setPurchasePrice(e.target.value)}
                className="w-full px-3 py-2 border rounded-lg"
                min="0"
                step="0.01"
                placeholder="0.00"
              />
            </div>

            <div>
              <label className="block text-sm font-medium mb-1">
                سعر البيع
              </label>
              <input
                type="number"
                value={sellingPrice}
                onChange={(e) => setSellingPrice(e.target.value)}
                className="w-full px-3 py-2 border rounded-lg"
                min="0"
                step="0.01"
                placeholder="0.00"
              />
            </div>

            <div className="flex items-end">
              <Button type="button" onClick={handleAddItem} className="w-full">
                إضافة
              </Button>
            </div>
          </div>

          <div className="mt-2">
            <input
              type="text"
              value={itemNotes}
              onChange={(e) => setItemNotes(e.target.value)}
              className="w-full px-3 py-2 border rounded-lg"
              placeholder="ملاحظات المنتج (اختياري)"
            />
          </div>
        </Card>

        {/* Items Table */}
        <Card padding="none" className="mb-6">
          <div className="p-4 border-b">
            <h2 className="text-lg font-semibold">المنتجات</h2>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                    المنتج
                  </th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                    الكمية
                  </th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                    سعر الشراء
                  </th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                    سعر البيع
                  </th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                    الإجمالي
                  </th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                    ملاحظات
                  </th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                    إجراءات
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200">
                {items.length === 0 ? (
                  <tr>
                    <td
                      colSpan={7}
                      className="px-4 py-8 text-center text-gray-500"
                    >
                      لم يتم إضافة منتجات بعد
                    </td>
                  </tr>
                ) : (
                  items.map((item) => (
                    <tr key={item.tempId}>
                      <td className="px-4 py-3 text-sm">{item.productName}</td>
                      <td className="px-4 py-3 text-sm">{item.quantity}</td>
                      <td className="px-4 py-3 text-sm">
                        {formatCurrency(item.purchasePrice)}
                      </td>
                      <td className="px-4 py-3 text-sm font-medium text-green-600">
                        {formatCurrency(item.sellingPrice)}
                      </td>
                      <td className="px-4 py-3 text-sm font-medium">
                        {formatCurrency(item.quantity * item.purchasePrice)}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-500">
                        {item.notes || "-"}
                      </td>
                      <td className="px-4 py-3 text-sm">
                        <button
                          type="button"
                          onClick={() => handleRemoveItem(item.tempId)}
                          className="text-red-600 hover:text-red-800"
                        >
                          حذف
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* Totals */}
          {items.length > 0 && (
            <div className="p-4 border-t bg-gray-50">
              <div className="flex justify-end">
                <div className="w-64">
                  <div className="flex justify-between mb-2">
                    <span className="text-sm">المجموع الفرعي:</span>
                    <span className="text-sm font-medium">
                      {formatCurrency(calculateSubtotal())}
                    </span>
                  </div>
                </div>
              </div>
            </div>
          )}
        </Card>

        {/* Actions */}
        <div className="flex justify-end gap-4 mb-6">
          <Button
            type="button"
            variant="outline"
            onClick={() => navigate("/purchase-invoices")}
          >
            إلغاء
          </Button>
          <Button type="submit" disabled={isCreating || isUpdating}>
            {isCreating || isUpdating
              ? "جاري الحفظ..."
              : isEditMode
                ? "تحديث"
                : "حفظ"}
          </Button>
        </div>
      </form>

      {/* Quick Add Product Modal */}
      <QuickAddProductModal
        isOpen={showQuickAddProduct}
        onClose={() => setShowQuickAddProduct(false)}
        onProductCreated={handleProductCreated}
      />
    </div>
  );
}
