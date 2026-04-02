import { useState, FormEvent } from "react";
import {
  UserPlus,
  Search,
  Edit,
  Trash2,
  ChevronLeft,
  ChevronRight,
  Users,
  Star,
  Eye,
} from "lucide-react";
import {
  useGetCustomersQuery,
  useDeleteCustomerMutation,
} from "@/api/customersApi";
import type { Customer } from "@/types/customer.types";
import { CustomerFormModal } from "@/components/customers/CustomerFormModal";
import { CustomerDetailsModal } from "@/components/customers/CustomerDetailsModal";
import { Button } from "@/components/common/Button";
import { Card } from "@/components/common/Card";
import { Loading } from "@/components/common/Loading";
import { formatDateTime, formatCurrency } from "@/utils/formatters";
import { toast } from "sonner";
import clsx from "clsx";
import { Portal } from "@/components/common/Portal";
import { handleApiError } from "@/utils/errorHandler";

export const CustomersPage = () => {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [searchInput, setSearchInput] = useState("");
  const [showFormModal, setShowFormModal] = useState(false);
  const [editingCustomer, setEditingCustomer] = useState<Customer | null>(null);
  const [deletingCustomer, setDeletingCustomer] = useState<Customer | null>(
    null,
  );
  const [viewingCustomer, setViewingCustomer] = useState<Customer | null>(null);

  const pageSize = 10;

  const { data, isLoading, isFetching } = useGetCustomersQuery({
    page,
    pageSize,
    search: search || undefined,
  });

  const [deleteCustomer, { isLoading: isDeleting }] =
    useDeleteCustomerMutation();

  const customers = data?.data?.items || [];
  const totalPages = data?.data?.totalPages || 1;
  const totalCount = data?.data?.totalCount || 0;
  const hasNextPage = data?.data?.hasNextPage || false;
  const hasPreviousPage = data?.data?.hasPreviousPage || false;

  const handleSearch = (e: FormEvent) => {
    e.preventDefault();
    setSearch(searchInput);
    setPage(1);
  };

  const handleEdit = (customer: Customer) => {
    setEditingCustomer(customer);
    setShowFormModal(true);
  };

  const handleDelete = async () => {
    if (!deletingCustomer) return;

    try {
      await deleteCustomer(deletingCustomer.id).unwrap();
      toast.success("تم حذف العميل بنجاح");
      setDeletingCustomer(null);
    } catch (error) {
      toast.error(handleApiError(error));
    }
  };

  const handleCloseFormModal = () => {
    setShowFormModal(false);
    setEditingCustomer(null);
  };

  if (isLoading) {
    return <Loading />;
  }

  const totalDue = customers.reduce((sum, c) => sum + c.totalDue, 0);
  const totalSpent = customers.reduce((sum, c) => sum + c.totalSpent, 0);

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-6">
        <div className="mb-8">
          <div className="flex items-center gap-3 mb-2">
            <div className="w-8 h-8 rounded-full bg-cyan-100 flex items-center justify-center">
              <Users className="w-5 h-5 text-cyan-600" />
            </div>
            <h1 className="text-3xl font-bold text-gray-900">إدارة العملاء</h1>
          </div>
          <p className="text-gray-600">
            إدارة قاعدة بيانات العملاء والمبيعات والديون
          </p>
        </div>

        <div className="flex justify-end">
          <Button variant="primary" onClick={() => setShowFormModal(true)}>
            <UserPlus className="w-5 h-5" />
            إضافة عميل جديد
          </Button>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <Card className="border-cyan-100">
            <p className="text-sm text-gray-600">إجمالي العملاء</p>
            <p className="text-2xl font-bold text-gray-900 mt-1">
              {totalCount}
            </p>
          </Card>
          <Card className="border-green-100">
            <p className="text-sm text-gray-600">إجمالي المبيعات</p>
            <p className="text-2xl font-bold text-green-700 mt-1">
              {formatCurrency(totalSpent)}
            </p>
          </Card>
          <Card className="border-amber-100">
            <p className="text-sm text-gray-600">إجمالي المستحق</p>
            <p className="text-2xl font-bold text-amber-700 mt-1">
              {formatCurrency(totalDue)}
            </p>
          </Card>
        </div>

        <form onSubmit={handleSearch}>
          <Card>
            <div className="flex gap-2">
              <div className="relative flex-1 max-w-md">
                <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
                <input
                  type="text"
                  value={searchInput}
                  onChange={(e) => setSearchInput(e.target.value)}
                  placeholder="بحث بالاسم أو رقم الهاتف..."
                  className="w-full pr-10 pl-4 py-2 border border-gray-200 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
                />
              </div>
              <Button type="submit" variant="secondary">
                بحث
              </Button>
              {search && (
                <Button
                  type="button"
                  variant="ghost"
                  onClick={() => {
                    setSearch("");
                    setSearchInput("");
                    setPage(1);
                  }}
                >
                  مسح
                </Button>
              )}
            </div>
          </Card>
        </form>

        <Card padding="none" className="flex-1 flex flex-col">
          <div className="overflow-x-auto flex-1">
            <table className="w-full">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  <th className="text-right px-6 py-4 text-sm font-semibold text-gray-600">
                    الاسم
                  </th>
                  <th className="text-right px-6 py-4 text-sm font-semibold text-gray-600">
                    رقم الهاتف
                  </th>
                  <th className="text-right px-6 py-4 text-sm font-semibold text-gray-600 hidden md:table-cell">
                    العنوان
                  </th>
                  <th className="text-right px-6 py-4 text-sm font-semibold text-gray-600">
                    نقاط الولاء
                  </th>
                  <th className="text-right px-6 py-4 text-sm font-semibold text-gray-600 hidden lg:table-cell">
                    إجمالي الطلبات
                  </th>
                  <th className="text-right px-6 py-4 text-sm font-semibold text-gray-600 hidden lg:table-cell">
                    إجمالي المشتريات
                  </th>
                  <th className="text-right px-6 py-4 text-sm font-semibold text-gray-600 hidden xl:table-cell">
                    المبلغ المستحق
                  </th>
                  <th className="text-center px-6 py-4 text-sm font-semibold text-gray-600">
                    الإجراءات
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {customers.length === 0 ? (
                  <tr>
                    <td
                      colSpan={8}
                      className="px-6 py-12 text-center text-gray-500"
                    >
                      <Users className="w-12 h-12 mx-auto mb-4 text-gray-300" />
                      <p className="text-lg font-medium">لا يوجد عملاء</p>
                      <p className="text-sm">ابدأ بإضافة عميل جديد</p>
                    </td>
                  </tr>
                ) : (
                  customers.map((customer) => (
                    <tr
                      key={customer.id}
                      className={clsx(
                        "hover:bg-gray-50 transition-colors",
                        isFetching && "opacity-50",
                      )}
                    >
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-3">
                          <div className="w-10 h-10 bg-primary-100 rounded-full flex items-center justify-center shrink-0">
                            <span className="text-primary-600 font-semibold">
                              {(customer.name ||
                                customer.phone)[0].toUpperCase()}
                            </span>
                          </div>
                          <div>
                            <p className="font-medium text-gray-800">
                              {customer.name || "—"}
                            </p>
                            {customer.lastOrderAt && (
                              <p className="text-xs text-gray-400">
                                آخر طلب: {formatDateTime(customer.lastOrderAt)}
                              </p>
                            )}
                          </div>
                        </div>
                      </td>
                      <td
                        className="px-6 py-4 font-mono text-gray-600"
                        dir="ltr"
                      >
                        {customer.phone}
                      </td>
                      <td className="px-6 py-4 text-gray-600 hidden md:table-cell">
                        {customer.address || "—"}
                      </td>
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-1">
                          <Star className="w-4 h-4 text-amber-500" />
                          <span className="font-medium text-gray-800">
                            {customer.loyaltyPoints}
                          </span>
                        </div>
                      </td>
                      <td className="px-6 py-4 text-gray-600 hidden lg:table-cell">
                        {customer.totalOrders} طلب
                      </td>
                      <td className="px-6 py-4 text-gray-600 hidden lg:table-cell">
                        {formatCurrency(customer.totalSpent)}
                      </td>
                      <td className="px-6 py-4 hidden xl:table-cell">
                        {customer.totalDue > 0 ? (
                          <div className="flex flex-col gap-1">
                            <span className="font-semibold text-orange-600">
                              {formatCurrency(customer.totalDue)}
                            </span>
                            {customer.creditLimit > 0 && (
                              <span className="text-xs text-gray-500">
                                من {formatCurrency(customer.creditLimit)}
                              </span>
                            )}
                          </div>
                        ) : (
                          <span className="text-gray-400">—</span>
                        )}
                      </td>
                      <td className="px-6 py-4">
                        <div className="flex items-center justify-center gap-2">
                          <button
                            onClick={() => setViewingCustomer(customer)}
                            className="p-2 text-gray-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors"
                            title="عرض التفاصيل"
                          >
                            <Eye className="w-4 h-4" />
                          </button>
                          <button
                            onClick={() => handleEdit(customer)}
                            className="p-2 text-gray-400 hover:text-primary-600 hover:bg-primary-50 rounded-lg transition-colors"
                            title="تعديل"
                          >
                            <Edit className="w-4 h-4" />
                          </button>
                          <button
                            onClick={() => setDeletingCustomer(customer)}
                            className="p-2 text-gray-400 hover:text-danger-600 hover:bg-danger-50 rounded-lg transition-colors"
                            title="حذف"
                          >
                            <Trash2 className="w-4 h-4" />
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {totalPages > 1 && (
            <div className="flex items-center justify-between px-6 py-4 border-t border-gray-200 bg-gray-50">
              <p className="text-sm text-gray-600">
                صفحة {page} من {totalPages}
              </p>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPage((p) => p - 1)}
                  disabled={!hasPreviousPage || isFetching}
                >
                  <ChevronRight className="w-4 h-4" />
                  السابق
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPage((p) => p + 1)}
                  disabled={!hasNextPage || isFetching}
                >
                  التالي
                  <ChevronLeft className="w-4 h-4" />
                </Button>
              </div>
            </div>
          )}
        </Card>

        {showFormModal && (
          <CustomerFormModal
            customer={editingCustomer}
            onClose={handleCloseFormModal}
          />
        )}

        {viewingCustomer && (
          <CustomerDetailsModal
            customer={viewingCustomer}
            onClose={() => setViewingCustomer(null)}
          />
        )}

        {deletingCustomer && (
          <Portal>
            <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-[100] p-4">
              <div className="bg-white rounded-2xl shadow-2xl w-full max-w-sm animate-scale-in p-6">
                <div className="text-center mb-6">
                  <div className="w-16 h-16 bg-danger-100 rounded-full flex items-center justify-center mx-auto mb-4">
                    <Trash2 className="w-8 h-8 text-danger-600" />
                  </div>
                  <h3 className="text-lg font-bold text-gray-800 mb-2">
                    حذف العميل
                  </h3>
                  <p className="text-gray-500">
                    هل أنت متأكد من حذف العميل "
                    {deletingCustomer.name || deletingCustomer.phone}"؟
                  </p>
                  <p className="text-sm text-danger-500 mt-2">
                    لا يمكن التراجع عن هذا الإجراء
                  </p>
                </div>
                <div className="flex gap-3">
                  <Button
                    variant="secondary"
                    onClick={() => setDeletingCustomer(null)}
                    className="flex-1"
                    disabled={isDeleting}
                  >
                    إلغاء
                  </Button>
                  <Button
                    variant="danger"
                    onClick={handleDelete}
                    isLoading={isDeleting}
                    className="flex-1"
                  >
                    حذف
                  </Button>
                </div>
              </div>
            </div>
          </Portal>
        )}

        {/* Help Section */}
        <div className="bg-blue-50 border border-blue-200 rounded-lg p-6">
          <h3 className="text-lg font-semibold text-blue-900 mb-3">
            💡 نصائح إدارة العملاء
          </h3>
          <ul className="space-y-2 text-sm text-blue-800">
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>بيانات العميل:</strong> أضف كل بيانات العميل بشكل صحيح
                للرجوع إليها لاحقاً
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>نقاط الولاء:</strong> تراكم نقاط مع كل عملية شراء يمكن
                استبدالها بخصومات
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>الديون:</strong> تتبع ما على كل عميل من مبالغ مستحقة
                الدفع
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>البحث:</strong> ابحث عن العميل باسمه أو رقم هاتفه للسرعة
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>التفاصيل:</strong> اضغط على عينك لعرض كل معاملات العميل
              </span>
            </li>
          </ul>
        </div>
      </div>
    </div>
  );
};

export default CustomersPage;
