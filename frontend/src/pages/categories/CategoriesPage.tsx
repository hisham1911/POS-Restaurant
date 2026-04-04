import { useState, FormEvent } from "react";
import { Plus, Edit2, Trash2, FolderOpen, Search } from "lucide-react";
import {
  useGetCategoriesQuery,
  useCreateCategoryMutation,
  useUpdateCategoryMutation,
  useDeleteCategoryMutation,
} from "@/api/categoriesApi";
import { Button } from "@/components/common/Button";
import { Input } from "@/components/common/Input";
import { Card } from "@/components/common/Card";
import { Modal } from "@/components/common/Modal";
import { Loading } from "@/components/common/Loading";
import type { Category } from "@/types/category.types";
import { toast } from "sonner";
import clsx from "clsx";

export const CategoriesPage = () => {
  const [showForm, setShowForm] = useState(false);
  const [editingCategory, setEditingCategory] = useState<Category | null>(null);
  const [formData, setFormData] = useState({
    name: "",
    nameEn: "",
    description: "",
  });
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);

  const { data: response, isLoading } = useGetCategoriesQuery({
    search: search || undefined,
    page,
    pageSize: 20,
  });
  const categories = response?.data || [];

  const [createCategory, { isLoading: isCreating }] =
    useCreateCategoryMutation();
  const [updateCategory, { isLoading: isUpdating }] =
    useUpdateCategoryMutation();
  const [deleteCategory, { isLoading: isDeleting }] =
    useDeleteCategoryMutation();

  const handleEdit = (category: Category) => {
    setEditingCategory(category);
    setFormData({
      name: category.name,
      nameEn: category.nameEn || "",
      description: category.description || "",
    });
    setShowForm(true);
  };

  const handleDelete = async (id: number) => {
    if (window.confirm("هل أنت متأكد من حذف هذا التصنيف؟")) {
      try {
        await deleteCategory(id).unwrap();
        toast.success("تم حذف التصنيف بنجاح");
      } catch {
        toast.error("فشل في حذف التصنيف");
      }
    }
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    try {
      if (editingCategory) {
        await updateCategory({
          id: editingCategory.id,
          data: formData,
        }).unwrap();
        toast.success("تم تحديث التصنيف بنجاح");
      } else {
        await createCategory(formData).unwrap();
        toast.success("تم إضافة التصنيف بنجاح");
      }
      handleCloseForm();
    } catch {
      toast.error(
        editingCategory ? "فشل في تحديث التصنيف" : "فشل في إضافة التصنيف",
      );
    }
  };

  const handleCloseForm = () => {
    setShowForm(false);
    setEditingCategory(null);
    setFormData({ name: "", nameEn: "", description: "" });
  };

  if (isLoading) return <Loading />;

  const activeCategories = categories.filter((c) => c.isActive).length;
  const totalProducts = categories.reduce(
    (sum, c) => sum + (c.productCount || 0),
    0,
  );

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-6">
        <div className="mb-8">
          <div className="flex items-center gap-3 mb-2">
            <div className="w-8 h-8 rounded-full bg-purple-100 flex items-center justify-center">
              <FolderOpen className="w-5 h-5 text-purple-600" />
            </div>
            <h1 className="text-3xl font-bold text-gray-900">
              إدارة التصنيفات
            </h1>
          </div>
          <p className="text-gray-600">
            تنظيم المنتجات في تصنيفات لتسهيل البحث والعرض
          </p>
        </div>

        <div className="flex justify-end">
          <Button
            variant="primary"
            onClick={() => setShowForm(true)}
            rightIcon={<Plus className="w-5 h-5" />}
          >
            إضافة تصنيف
          </Button>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <Card className="border-purple-100">
            <p className="text-sm text-gray-600">إجمالي التصنيفات</p>
            <p className="text-2xl font-bold text-gray-900 mt-1">
              {categories.length}
            </p>
          </Card>
          <Card className="border-green-100">
            <p className="text-sm text-gray-600">التصنيفات النشطة</p>
            <p className="text-2xl font-bold text-green-700 mt-1">
              {activeCategories}
            </p>
          </Card>
          <Card className="border-blue-100">
            <p className="text-sm text-gray-600">إجمالي المنتجات</p>
            <p className="text-2xl font-bold text-blue-700 mt-1">
              {totalProducts}
            </p>
          </Card>
        </div>

        <Card>
          <div className="relative">
            <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
            <Input
              type="text"
              placeholder="ابحث عن تصنيف..."
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
              className="pr-10"
            />
          </div>
        </Card>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {categories.map((category) => (
            <Card key={category.id} className="relative">
              <div className="flex items-start justify-between">
                <div className="flex items-center gap-3">
                  <div className="w-12 h-12 bg-primary-100 rounded-xl flex items-center justify-center">
                    <FolderOpen className="w-6 h-6 text-primary-600" />
                  </div>
                  <div>
                    <h3 className="font-semibold text-gray-800">
                      {category.name}
                    </h3>
                    {category.nameEn && (
                      <p className="text-sm text-gray-500">{category.nameEn}</p>
                    )}
                  </div>
                </div>
                <div className="flex items-center gap-1">
                  <button
                    onClick={() => handleEdit(category)}
                    className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
                  >
                    <Edit2 className="w-4 h-4 text-gray-500" />
                  </button>
                  <button
                    onClick={() => handleDelete(category.id)}
                    disabled={isDeleting}
                    className="p-2 hover:bg-danger-50 rounded-lg transition-colors"
                  >
                    <Trash2 className="w-4 h-4 text-danger-500" />
                  </button>
                </div>
              </div>
              {category.description && (
                <p className="mt-3 text-sm text-gray-500">
                  {category.description}
                </p>
              )}
              <div className="mt-4 pt-4 border-t flex items-center justify-between">
                <span className="text-sm text-gray-500">
                  {category.productCount || 0} منتج
                </span>
                <span
                  className={clsx(
                    "px-2.5 py-0.5 rounded-full text-xs font-medium",
                    category.isActive
                      ? "bg-success-50 text-success-500"
                      : "bg-gray-100 text-gray-500",
                  )}
                >
                  {category.isActive ? "نشط" : "غير نشط"}
                </span>
              </div>
            </Card>
          ))}

          {categories.length === 0 && (
            <div className="col-span-full text-center py-12 text-gray-400">
              <FolderOpen className="w-12 h-12 mx-auto mb-3" />
              <p>{search ? "لا توجد نتائج للبحث" : "لا توجد تصنيفات"}</p>
            </div>
          )}
        </div>

        {categories.length > 0 && (
          <div className="flex items-center justify-center gap-2">
            <Button
              variant="secondary"
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page === 1}
            >
              السابق
            </Button>
            <span className="px-4 py-2 text-sm text-gray-600">صفحة {page}</span>
            <Button
              variant="secondary"
              onClick={() => setPage((p) => p + 1)}
              disabled={categories.length < 20}
            >
              التالي
            </Button>
          </div>
        )}

        <Modal
          isOpen={showForm}
          onClose={handleCloseForm}
          title={editingCategory ? "تعديل التصنيف" : "إضافة تصنيف جديد"}
        >
          <form onSubmit={handleSubmit} className="space-y-4">
            <Input
              label="اسم التصنيف (عربي)"
              value={formData.name}
              onChange={(e) =>
                setFormData({ ...formData, name: e.target.value })
              }
              placeholder="مثال: المشروبات"
              required
            />
            <Input
              label="اسم التصنيف (إنجليزي)"
              value={formData.nameEn}
              onChange={(e) =>
                setFormData({ ...formData, nameEn: e.target.value })
              }
              placeholder="مثال بالإنجليزية: Beverages"
            />
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">
                الوصف
              </label>
              <textarea
                value={formData.description}
                onChange={(e) =>
                  setFormData({ ...formData, description: e.target.value })
                }
                placeholder="وصف اختياري للتصنيف..."
                rows={3}
                className="w-full px-4 py-2.5 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent"
              />
            </div>
            <div className="flex gap-3 pt-4">
              <Button
                type="button"
                variant="secondary"
                onClick={handleCloseForm}
                className="flex-1"
              >
                إلغاء
              </Button>
              <Button
                type="submit"
                variant="primary"
                isLoading={isCreating || isUpdating}
                className="flex-1"
              >
                {editingCategory ? "تحديث" : "إضافة"}
              </Button>
            </div>
          </form>
        </Modal>

        {/* Help Section */}
        <div className="bg-blue-50 border border-blue-200 rounded-lg p-6">
          <h3 className="text-lg font-semibold text-blue-900 mb-3">
            💡 نصائح إدارة التصنيفات
          </h3>
          <ul className="space-y-2 text-sm text-blue-800">
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>التصنيف:</strong> تصنيف واحد يجمع مجموعة منتجات متشابهة
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>الأسماء:</strong> أضف اسم بالعربية والإنجليزية لسهولة
                البحث
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>عدد المنتجات:</strong> يظهر تلقائياً كم منتج في كل تصنيف
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>النشاط:</strong> يمكن تعطيل تصنيف بدون حذف المنتجات
                بداخله
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>البحث:</strong> استخدم البحث للعثور على تصنيف معين
                سريعاً
              </span>
            </li>
          </ul>
        </div>
      </div>
    </div>
  );
};

export default CategoriesPage;
