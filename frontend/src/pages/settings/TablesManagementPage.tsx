import { useMemo, useState } from "react";
import { Armchair, Pencil, Plus, Trash2 } from "lucide-react";
import clsx from "clsx";
import { toast } from "sonner";
import {
  useCreateRestaurantTableMutation,
  useDeleteRestaurantTableMutation,
  useGetRestaurantTablesQuery,
  useUpdateRestaurantTableMutation,
} from "@/api/restaurantTablesApi";
import { Button, ConfirmDialog } from "@/components/common";
import { Card } from "@/components/common/Card";
import { Input } from "@/components/common/Input";
import { Loading } from "@/components/common/Loading";
import { useAppSelector } from "@/store/hooks";
import { selectCurrentBranch } from "@/store/slices/branchSlice";
import type { ApiResponse } from "@/types/api.types";
import type { RestaurantTable } from "@/types/restaurant.types";

interface TableFormState {
  number: string;
  sortOrder: string;
  isActive: boolean;
}

const emptyForm: TableFormState = {
  number: "",
  sortOrder: "0",
  isActive: true,
};

const isOccupied = (table: RestaurantTable) =>
  table.status === "Occupied" || table.status === 1;

const apiErrorMessage = (error: unknown, fallback: string) => {
  const apiError = error as { data?: ApiResponse<unknown> };
  return apiError.data?.message ?? fallback;
};

const TablesManagementPage = () => {
  const currentBranch = useAppSelector(selectCurrentBranch);
  const branchId = currentBranch?.id ?? 0;

  const { data, isLoading } = useGetRestaurantTablesQuery(branchId, {
    skip: !branchId,
  });
  const [createTable, { isLoading: isCreating }] =
    useCreateRestaurantTableMutation();
  const [updateTable, { isLoading: isUpdating }] =
    useUpdateRestaurantTableMutation();
  const [deleteTable, { isLoading: isDeleting }] =
    useDeleteRestaurantTableMutation();

  const [form, setForm] = useState<TableFormState>(emptyForm);
  const [editingTable, setEditingTable] = useState<RestaurantTable | null>(
    null,
  );
  const [deletingTable, setDeletingTable] = useState<RestaurantTable | null>(
    null,
  );

  const tables = useMemo(
    () =>
      [...(data?.data ?? [])].sort(
        (a, b) => a.sortOrder - b.sortOrder || a.number.localeCompare(b.number),
      ),
    [data?.data],
  );

  const occupiedCount = tables.filter(isOccupied).length;
  const activeCount = tables.filter((table) => table.isActive).length;
  const isSaving = isCreating || isUpdating;

  const resetForm = () => {
    setForm(emptyForm);
    setEditingTable(null);
  };

  const handleEdit = (table: RestaurantTable) => {
    setEditingTable(table);
    setForm({
      number: table.number,
      sortOrder: String(table.sortOrder),
      isActive: table.isActive,
    });
  };

  const handleSubmit = async () => {
    const number = form.number.trim();
    const sortOrder = Number(form.sortOrder || 0);

    if (!branchId) {
      toast.error("اختر فرعا أولا");
      return;
    }

    if (!number) {
      toast.error("رقم الطاولة مطلوب");
      return;
    }

    if (!Number.isFinite(sortOrder) || sortOrder < 0) {
      toast.error("ترتيب الطاولة يجب أن يكون صفرا أو أكثر");
      return;
    }

    try {
      if (editingTable) {
        await updateTable({
          id: editingTable.id,
          body: {
            number,
            sortOrder,
            isActive: form.isActive,
          },
        }).unwrap();
        toast.success("تم تحديث الطاولة");
      } else {
        await createTable({
          branchId,
          number,
          sortOrder,
        }).unwrap();
        toast.success("تمت إضافة الطاولة");
      }

      resetForm();
    } catch (error) {
      toast.error(apiErrorMessage(error, "تعذر حفظ الطاولة"));
    }
  };

  const handleConfirmDelete = async () => {
    if (!deletingTable) return;

    try {
      await deleteTable(deletingTable.id).unwrap();
      toast.success("تم حذف الطاولة");
      setDeletingTable(null);
      if (editingTable?.id === deletingTable.id) {
        resetForm();
      }
    } catch (error) {
      toast.error(apiErrorMessage(error, "تعذر حذف الطاولة"));
    }
  };

  if (!branchId) {
    return (
      <div className="p-6">
        <Card className="text-center text-sm text-gray-600">
          اختر فرعا أولا لإدارة طاولات الصالة.
        </Card>
      </div>
    );
  }

  if (isLoading) {
    return <Loading />;
  }

  return (
    <div className="min-h-full bg-gray-50 p-4 sm:p-6">
      <div className="mx-auto max-w-6xl space-y-5">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-3">
            <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-primary-100 text-primary-700">
              <Armchair className="h-6 w-6" />
            </div>
            <div>
              <h1 className="text-2xl font-bold text-gray-900">
                إدارة الطاولات
              </h1>
              <p className="text-sm text-gray-500">
                طاولات الصالة لهذا الفرع. الطاولة المشغولة تفتح الطلب المرتبط
                بها من شاشة الطاولات.
              </p>
            </div>
          </div>
        </div>

        <div className="grid gap-3 sm:grid-cols-3">
          <Card>
            <p className="text-sm text-gray-500">كل الطاولات</p>
            <p className="mt-1 text-2xl font-bold text-gray-900">
              {tables.length}
            </p>
          </Card>
          <Card className="border-emerald-100">
            <p className="text-sm text-gray-500">النشطة</p>
            <p className="mt-1 text-2xl font-bold text-emerald-600">
              {activeCount}
            </p>
          </Card>
          <Card className="border-red-100">
            <p className="text-sm text-gray-500">المشغولة</p>
            <p className="mt-1 text-2xl font-bold text-red-600">
              {occupiedCount}
            </p>
          </Card>
        </div>

        <Card className="space-y-4">
          <div className="flex items-center justify-between gap-3">
            <h2 className="font-bold text-gray-900">
              {editingTable ? "تعديل طاولة" : "إضافة طاولة"}
            </h2>
            {editingTable && (
              <Button variant="ghost" size="sm" onClick={resetForm}>
                إلغاء التعديل
              </Button>
            )}
          </div>

          <div className="grid gap-3 sm:grid-cols-[1fr_160px_auto] sm:items-end">
            <Input
              label="رقم الطاولة"
              value={form.number}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  number: event.target.value,
                }))
              }
              placeholder="مثال: 1 أو A1"
            />
            <Input
              label="الترتيب"
              type="number"
              min="0"
              value={form.sortOrder}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  sortOrder: event.target.value,
                }))
              }
            />
            <Button
              variant="primary"
              onClick={handleSubmit}
              isLoading={isSaving}
              leftIcon={<Plus className="h-4 w-4" />}
              className="min-h-[44px]"
            >
              {editingTable ? "حفظ التعديل" : "إضافة"}
            </Button>
          </div>

          {editingTable && (
            <label className="inline-flex items-center gap-2 text-sm font-medium text-gray-700">
              <input
                type="checkbox"
                checked={form.isActive}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    isActive: event.target.checked,
                  }))
                }
                className="h-4 w-4 rounded border-gray-300 text-primary-600 focus:ring-primary-500"
              />
              الطاولة نشطة وتظهر للكاشير
            </label>
          )}
        </Card>

        <Card padding="none" className="overflow-hidden">
          {tables.length === 0 ? (
            <div className="p-10 text-center text-gray-500">
              لا توجد طاولات بعد. أضف أول طاولة من النموذج بالأعلى.
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[720px]">
                <thead className="bg-gray-50 text-sm text-gray-600">
                  <tr>
                    <th className="px-4 py-3 text-start font-semibold">
                      الطاولة
                    </th>
                    <th className="px-4 py-3 text-start font-semibold">
                      الحالة
                    </th>
                    <th className="px-4 py-3 text-start font-semibold">
                      الترتيب
                    </th>
                    <th className="px-4 py-3 text-start font-semibold">
                      التفعيل
                    </th>
                    <th className="px-4 py-3 text-center font-semibold">
                      الإجراءات
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {tables.map((table) => {
                    const occupied = isOccupied(table);

                    return (
                      <tr key={table.id} className="hover:bg-gray-50">
                        <td className="px-4 py-3">
                          <div className="flex items-center gap-2 font-bold text-gray-900">
                            <Armchair className="h-4 w-4 text-primary-600" />
                            طاولة {table.number}
                          </div>
                        </td>
                        <td className="px-4 py-3">
                          <span
                            className={clsx(
                              "rounded-full px-2.5 py-1 text-xs font-bold",
                              occupied
                                ? "bg-red-50 text-red-700"
                                : "bg-emerald-50 text-emerald-700",
                            )}
                          >
                            {occupied ? "مشغولة" : "متاحة"}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-sm text-gray-600">
                          {table.sortOrder}
                        </td>
                        <td className="px-4 py-3">
                          <span
                            className={clsx(
                              "rounded-full px-2.5 py-1 text-xs font-bold",
                              table.isActive
                                ? "bg-primary-50 text-primary-700"
                                : "bg-gray-100 text-gray-500",
                            )}
                          >
                            {table.isActive ? "نشطة" : "غير نشطة"}
                          </span>
                        </td>
                        <td className="px-4 py-3">
                          <div className="flex items-center justify-center gap-1">
                            <button
                              type="button"
                              onClick={() => handleEdit(table)}
                              className="rounded-lg p-2 text-blue-600 transition hover:bg-blue-50"
                              title="تعديل"
                            >
                              <Pencil className="h-4 w-4" />
                            </button>
                            <button
                              type="button"
                              onClick={() => setDeletingTable(table)}
                              className="rounded-lg p-2 text-danger-600 transition hover:bg-danger-50"
                              title="حذف"
                            >
                              <Trash2 className="h-4 w-4" />
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      </div>

      <ConfirmDialog
        open={deletingTable !== null}
        onOpenChange={(open) => !open && setDeletingTable(null)}
        onConfirm={handleConfirmDelete}
        title="حذف الطاولة"
        description={
          deletingTable
            ? `هل تريد حذف طاولة ${deletingTable.number}؟`
            : "هل تريد حذف هذه الطاولة؟"
        }
        confirmText="حذف"
        cancelText="تراجع"
        isLoading={isDeleting}
      />
    </div>
  );
};

export default TablesManagementPage;
