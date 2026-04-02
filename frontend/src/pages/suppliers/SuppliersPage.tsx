import { useState } from "react";
import { Trash2, Edit, Plus, Search } from "lucide-react";
import { toast } from "sonner";
import {
  useGetSuppliersQuery,
  useDeleteSupplierMutation,
} from "../../api/suppliersApi";
import { Supplier } from "../../types/supplier.types";
import { Button } from "../../components/common/Button";
import { Card } from "../../components/common/Card";
import { Loading } from "../../components/common/Loading";
import { Input } from "../../components/common/Input";
import SupplierFormModal from "../../components/suppliers/SupplierFormModal";
import { handleApiError } from "../../utils/errorHandler";

export default function SuppliersPage() {
  const [searchTerm, setSearchTerm] = useState("");
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [selectedSupplier, setSelectedSupplier] = useState<Supplier | null>(
    null,
  );

  const { data: response, isLoading } = useGetSuppliersQuery();
  const [deleteSupplier] = useDeleteSupplierMutation();

  const suppliers = response?.data || [];

  // Filter suppliers by search term
  const filteredSuppliers = suppliers.filter(
    (supplier) =>
      supplier.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
      supplier.nameEn?.toLowerCase().includes(searchTerm.toLowerCase()) ||
      supplier.phone?.includes(searchTerm) ||
      supplier.email?.toLowerCase().includes(searchTerm.toLowerCase()),
  );

  const handleAddSupplier = () => {
    setSelectedSupplier(null);
    setIsModalOpen(true);
  };

  const handleEditSupplier = (supplier: Supplier) => {
    setSelectedSupplier(supplier);
    setIsModalOpen(true);
  };

  const handleDeleteSupplier = async (id: number, name: string) => {
    if (!confirm(`هل أنت متأكد من حذف المورد "${name}"؟`)) {
      return;
    }

    try {
      await deleteSupplier(id).unwrap();
      toast.success("تم حذف المورد بنجاح");
    } catch (error) {
      toast.error(handleApiError(error));
    }
  };

  if (isLoading) {
    return <Loading />;
  }

  const activeSuppliers = filteredSuppliers.filter((s) => s.isActive).length;

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-6">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <div className="flex items-center gap-3 mb-2">
              <div className="w-10 h-10 rounded-full bg-indigo-100 flex items-center justify-center">
                <Plus className="w-5 h-5 text-indigo-600" />
              </div>
              <h1 className="text-3xl font-bold text-gray-900">الموردين</h1>
            </div>
            <p className="text-gray-600">إدارة الموردين والشركات الموردة</p>
          </div>
          <Button
            onClick={handleAddSupplier}
            className="flex items-center gap-2"
          >
            <Plus className="w-4 h-4" />
            إضافة مورد
          </Button>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <Card className="border-indigo-100">
            <p className="text-sm text-gray-600">إجمالي الموردين</p>
            <p className="text-2xl font-bold text-gray-900 mt-1">
              {filteredSuppliers.length}
            </p>
          </Card>
          <Card className="border-green-100">
            <p className="text-sm text-gray-600">الموردين النشطين</p>
            <p className="text-2xl font-bold text-green-700 mt-1">
              {activeSuppliers}
            </p>
          </Card>
        </div>

        <Card className="mb-6">
          <div className="relative">
            <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
            <Input
              type="text"
              placeholder="ابحث عن مورد (الاسم، الهاتف، البريد الإلكتروني...)"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="pr-10"
            />
          </div>
        </Card>

        <Card padding="none">
          {filteredSuppliers.length === 0 ? (
            <div className="text-center py-12">
              <p className="text-gray-500">
                {searchTerm ? "لا توجد نتائج للبحث" : "لا يوجد موردين"}
              </p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead className="bg-gray-50 border-b">
                  <tr>
                    <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                      الاسم
                    </th>
                    <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                      الهاتف
                    </th>
                    <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                      البريد الإلكتروني
                    </th>
                    <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                      جهة الاتصال
                    </th>
                    <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                      الحالة
                    </th>
                    <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                      الإجراءات
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200">
                  {filteredSuppliers.map((supplier) => (
                    <tr key={supplier.id} className="hover:bg-gray-50">
                      <td className="px-6 py-4">
                        <div>
                          <div className="font-medium text-gray-900">
                            {supplier.name}
                          </div>
                          {supplier.nameEn && (
                            <div className="text-sm text-gray-500">
                              {supplier.nameEn}
                            </div>
                          )}
                        </div>
                      </td>
                      <td className="px-6 py-4 text-sm text-gray-900">
                        {supplier.phone || "-"}
                      </td>
                      <td className="px-6 py-4 text-sm text-gray-900">
                        {supplier.email || "-"}
                      </td>
                      <td className="px-6 py-4 text-sm text-gray-900">
                        {supplier.contactPerson || "-"}
                      </td>
                      <td className="px-6 py-4">
                        <span
                          className={`inline-flex px-2 py-1 text-xs font-semibold rounded-full ${
                            supplier.isActive
                              ? "bg-green-100 text-green-800"
                              : "bg-red-100 text-red-800"
                          }`}
                        >
                          {supplier.isActive ? "نشط" : "غير نشط"}
                        </span>
                      </td>
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-2">
                          <button
                            onClick={() => handleEditSupplier(supplier)}
                            className="p-2 text-blue-600 hover:bg-blue-50 rounded-lg transition-colors"
                            title="تعديل"
                          >
                            <Edit className="w-4 h-4" />
                          </button>
                          <button
                            onClick={() =>
                              handleDeleteSupplier(supplier.id, supplier.name)
                            }
                            className="p-2 text-red-600 hover:bg-red-50 rounded-lg transition-colors"
                            title="حذف"
                          >
                            <Trash2 className="w-4 h-4" />
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>

        {isModalOpen && (
          <SupplierFormModal
            supplier={selectedSupplier}
            onClose={() => {
              setIsModalOpen(false);
              setSelectedSupplier(null);
            }}
          />
        )}
      </div>
    </div>
  );
}
