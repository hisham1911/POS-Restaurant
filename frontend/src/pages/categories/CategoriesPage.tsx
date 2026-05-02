import { useState } from "react";
import clsx from "clsx";
import { Edit2, FolderOpen, Plus, Search, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { useDeleteCategoryMutation, useGetCategoriesQuery } from "@/api/categoriesApi";
import { CategoryFormModal } from "@/components/categories/CategoryFormModal";
import { Button, ConfirmDialog } from "@/components/common";
import { Card } from "@/components/common/Card";
import { Input } from "@/components/common/Input";
import { Loading } from "@/components/common/Loading";
import type { Category } from "@/types/category.types";

const isImageSource = (value?: string): boolean => {
  if (!value) return false;
  const normalized = value.trim();
  if (!normalized) return false;
  return /^(https?:\/\/|\/|data:image\/|blob:)/i.test(normalized);
};

export const CategoriesPage = () => {
  const [editingCategory, setEditingCategory] = useState<Category | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [deletingCategoryId, setDeletingCategoryId] = useState<number | null>(
    null,
  );
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);

  const { data: response, isLoading } = useGetCategoriesQuery({
    search: search || undefined,
    page,
    pageSize: 20,
  });
  const categories = response?.data || [];

  const [deleteCategory, { isLoading: isDeleting }] =
    useDeleteCategoryMutation();

  const handleEdit = (category: Category) => {
    setEditingCategory(category);
    setShowForm(true);
  };

  const handleCreate = () => {
    setEditingCategory(null);
    setShowForm(true);
  };

  const handleCloseForm = () => {
    setShowForm(false);
    setEditingCategory(null);
  };

  const handleConfirmDelete = async () => {
    if (deletingCategoryId === null) return;

    try {
      await deleteCategory(deletingCategoryId).unwrap();
      toast.success("تم حذف التصنيف بنجاح");
      setDeletingCategoryId(null);
    } catch {
      setDeletingCategoryId(null);
    }
  };

  if (isLoading) return <Loading />;

  const activeCategories = categories.filter((category) => category.isActive)
    .length;
  const totalProducts = categories.reduce(
    (sum, category) => sum + (category.productCount || 0),
    0,
  );

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="mx-auto max-w-7xl space-y-4 px-4 py-5 sm:space-y-6 sm:px-6 sm:py-6 lg:px-8 lg:py-8">
        <div className="mb-8">
          <div className="mb-2 flex items-center gap-3">
            <div className="flex h-8 w-8 items-center justify-center rounded-full bg-purple-100">
              <FolderOpen className="h-5 w-5 text-purple-600" />
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
            onClick={handleCreate}
            rightIcon={<Plus className="h-5 w-5" />}
          >
            إضافة تصنيف
          </Button>
        </div>

        <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
          <Card className="border-purple-100">
            <p className="text-sm text-gray-600">إجمالي التصنيفات</p>
            <p className="mt-1 text-2xl font-bold text-gray-900">
              {categories.length}
            </p>
          </Card>
          <Card className="border-green-100">
            <p className="text-sm text-gray-600">التصنيفات النشطة</p>
            <p className="mt-1 text-2xl font-bold text-green-700">
              {activeCategories}
            </p>
          </Card>
          <Card className="border-blue-100">
            <p className="text-sm text-gray-600">إجمالي المنتجات</p>
            <p className="mt-1 text-2xl font-bold text-blue-700">
              {totalProducts}
            </p>
          </Card>
        </div>

        <Card>
          <div className="relative">
            <Search className="absolute right-3 top-1/2 h-5 w-5 -translate-y-1/2 text-gray-400" />
            <Input
              type="text"
              placeholder="ابحث عن تصنيف..."
              value={search}
              onChange={(event) => {
                setSearch(event.target.value);
                setPage(1);
              }}
              className="pr-10"
            />
          </div>
        </Card>

        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
          {categories.map((category) => (
            <Card key={category.id} className="relative">
              <div className="flex items-start justify-between">
                <div className="flex items-center gap-3">
                  <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-primary-100">
                    {category.imageUrl ? (
                      isImageSource(category.imageUrl) ? (
                        <img
                          src={category.imageUrl}
                          alt={category.name}
                          className="h-8 w-8 rounded object-cover"
                        />
                      ) : (
                        <span className="text-2xl">{category.imageUrl}</span>
                      )
                    ) : (
                      <FolderOpen className="h-6 w-6 text-primary-600" />
                    )}
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
                    className="rounded-lg p-2 transition-colors hover:bg-gray-100"
                  >
                    <Edit2 className="h-4 w-4 text-gray-500" />
                  </button>
                  <button
                    onClick={() => setDeletingCategoryId(category.id)}
                    disabled={isDeleting}
                    className="rounded-lg p-2 transition-colors hover:bg-danger-50"
                  >
                    <Trash2 className="h-4 w-4 text-danger-500" />
                  </button>
                </div>
              </div>

              {category.description && (
                <p className="mt-3 text-sm text-gray-500">
                  {category.description}
                </p>
              )}

              <div className="mt-4 flex items-center justify-between border-t pt-4">
                <div className="space-y-1">
                  <span className="text-sm text-gray-500">
                    {category.productCount || 0} منتج
                  </span>
                  <p className="text-xs text-gray-400">
                    ترتيب {category.sortOrder}
                  </p>
                </div>
                <span
                  className={clsx(
                    "rounded-full px-2.5 py-0.5 text-xs font-medium",
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
            <div className="col-span-full py-12 text-center text-gray-400">
              <FolderOpen className="mx-auto mb-3 h-12 w-12" />
              <p>{search ? "لا توجد نتائج للبحث" : "لا توجد تصنيفات"}</p>
            </div>
          )}
        </div>

        {categories.length > 0 && (
          <div className="flex items-center justify-center gap-2">
            <Button
              variant="secondary"
              onClick={() => setPage((prev) => Math.max(1, prev - 1))}
              disabled={page === 1}
            >
              السابق
            </Button>
            <span className="px-4 py-2 text-sm text-gray-600">صفحة {page}</span>
            <Button
              variant="secondary"
              onClick={() => setPage((prev) => prev + 1)}
              disabled={categories.length < 20}
            >
              التالي
            </Button>
          </div>
        )}

        <CategoryFormModal
          isOpen={showForm}
          onClose={handleCloseForm}
          onSuccess={handleCloseForm}
          category={editingCategory ?? undefined}
        />

        <ConfirmDialog
          open={deletingCategoryId !== null}
          onOpenChange={(open) => !open && setDeletingCategoryId(null)}
          onConfirm={handleConfirmDelete}
          title="حذف التصنيف"
          description="هل أنت متأكد من حذف هذا التصنيف؟"
          isLoading={isDeleting}
        />

        <div className="rounded-lg border border-blue-200 bg-blue-50 p-6">
          <h3 className="mb-3 text-lg font-semibold text-blue-900">
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
                <strong>الأسماء:</strong> أضف اسمًا بالعربية والإنجليزية لسهولة
                البحث
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>عدد المنتجات:</strong> يظهر تلقائيًا كم منتج في كل تصنيف
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
                <strong>البحث:</strong> استخدم البحث للوصول إلى تصنيف معين بسرعة
              </span>
            </li>
          </ul>
        </div>
      </div>
    </div>
  );
};

export default CategoriesPage;
