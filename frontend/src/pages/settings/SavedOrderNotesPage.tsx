import { useMemo, useState } from "react";
import { MessageSquare, Pencil, Plus, Trash2 } from "lucide-react";
import clsx from "clsx";
import { toast } from "sonner";
import {
  useCreateSavedOrderNoteMutation,
  useDeleteSavedOrderNoteMutation,
  useGetSavedOrderNotesQuery,
  useUpdateSavedOrderNoteMutation,
} from "@/api/savedOrderNotesApi";
import { Button, ConfirmDialog } from "@/components/common";
import { Card } from "@/components/common/Card";
import { Input } from "@/components/common/Input";
import { Loading } from "@/components/common/Loading";
import { useAppSelector } from "@/store/hooks";
import { selectCurrentBranch } from "@/store/slices/branchSlice";
import type { ApiResponse } from "@/types/api.types";
import type { SavedOrderNote } from "@/types/restaurant.types";

interface NoteFormState {
  text: string;
  sortOrder: string;
  isActive: boolean;
}

const emptyForm: NoteFormState = {
  text: "",
  sortOrder: "0",
  isActive: true,
};

const apiErrorMessage = (error: unknown, fallback: string) => {
  const apiError = error as { data?: ApiResponse<unknown> };
  return apiError.data?.message ?? fallback;
};

const SavedOrderNotesPage = () => {
  const currentBranch = useAppSelector(selectCurrentBranch);
  const branchId = currentBranch?.id ?? 0;

  const { data, isLoading } = useGetSavedOrderNotesQuery(branchId, {
    skip: !branchId,
  });
  const [createNote, { isLoading: isCreating }] =
    useCreateSavedOrderNoteMutation();
  const [updateNote, { isLoading: isUpdating }] =
    useUpdateSavedOrderNoteMutation();
  const [deleteNote, { isLoading: isDeleting }] =
    useDeleteSavedOrderNoteMutation();

  const [form, setForm] = useState<NoteFormState>(emptyForm);
  const [editingNote, setEditingNote] = useState<SavedOrderNote | null>(null);
  const [deletingNote, setDeletingNote] = useState<SavedOrderNote | null>(null);

  const notes = useMemo(
    () =>
      [...(data?.data ?? [])].sort(
        (a, b) => a.sortOrder - b.sortOrder || a.text.localeCompare(b.text),
      ),
    [data?.data],
  );

  const activeCount = notes.filter((note) => note.isActive).length;
  const isSaving = isCreating || isUpdating;

  const resetForm = () => {
    setForm(emptyForm);
    setEditingNote(null);
  };

  const handleEdit = (note: SavedOrderNote) => {
    setEditingNote(note);
    setForm({
      text: note.text,
      sortOrder: String(note.sortOrder),
      isActive: note.isActive,
    });
  };

  const handleSubmit = async () => {
    const text = form.text.trim();
    const sortOrder = Number(form.sortOrder || 0);

    if (!branchId) {
      toast.error("اختر فرعا أولا");
      return;
    }

    if (!text) {
      toast.error("نص الملاحظة مطلوب");
      return;
    }

    if (!Number.isFinite(sortOrder) || sortOrder < 0) {
      toast.error("ترتيب الملاحظة يجب أن يكون صفرا أو أكثر");
      return;
    }

    try {
      if (editingNote) {
        await updateNote({
          id: editingNote.id,
          body: {
            text,
            sortOrder,
            isActive: form.isActive,
          },
        }).unwrap();
        toast.success("تم تحديث الملاحظة");
      } else {
        await createNote({
          branchId,
          text,
          sortOrder,
        }).unwrap();
        toast.success("تمت إضافة الملاحظة");
      }

      resetForm();
    } catch (error) {
      toast.error(apiErrorMessage(error, "تعذر حفظ الملاحظة"));
    }
  };

  const handleConfirmDelete = async () => {
    if (!deletingNote) return;

    try {
      await deleteNote(deletingNote.id).unwrap();
      toast.success("تم حذف الملاحظة");
      setDeletingNote(null);
      if (editingNote?.id === deletingNote.id) {
        resetForm();
      }
    } catch (error) {
      toast.error(apiErrorMessage(error, "تعذر حذف الملاحظة"));
    }
  };

  if (!branchId) {
    return (
      <div className="p-6">
        <Card className="text-center text-sm text-gray-600">
          اختر فرعا أولا لإدارة الملاحظات السريعة.
        </Card>
      </div>
    );
  }

  if (isLoading) {
    return <Loading />;
  }

  return (
    <div className="min-h-full bg-gray-50 p-4 sm:p-6">
      <div className="mx-auto max-w-5xl space-y-5">
        <div className="flex items-center gap-3">
          <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-amber-100 text-amber-700">
            <MessageSquare className="h-6 w-6" />
          </div>
          <div>
            <h1 className="text-2xl font-bold text-gray-900">
              الملاحظات السريعة
            </h1>
            <p className="text-sm text-gray-500">
              ملاحظات عامة يختارها الكاشير وتظهر في تذكرة المطبخ وفاتورة
              العميل.
            </p>
          </div>
        </div>

        <div className="grid gap-3 sm:grid-cols-2">
          <Card>
            <p className="text-sm text-gray-500">كل الملاحظات</p>
            <p className="mt-1 text-2xl font-bold text-gray-900">
              {notes.length}
            </p>
          </Card>
          <Card className="border-amber-100">
            <p className="text-sm text-gray-500">النشطة</p>
            <p className="mt-1 text-2xl font-bold text-amber-600">
              {activeCount}
            </p>
          </Card>
        </div>

        <Card className="space-y-4">
          <div className="flex items-center justify-between gap-3">
            <h2 className="font-bold text-gray-900">
              {editingNote ? "تعديل ملاحظة" : "إضافة ملاحظة"}
            </h2>
            {editingNote && (
              <Button variant="ghost" size="sm" onClick={resetForm}>
                إلغاء التعديل
              </Button>
            )}
          </div>

          <div className="grid gap-3 sm:grid-cols-[1fr_160px_auto] sm:items-end">
            <Input
              label="نص الملاحظة"
              value={form.text}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  text: event.target.value,
                }))
              }
              placeholder="مثال: بدون بصل"
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
              {editingNote ? "حفظ التعديل" : "إضافة"}
            </Button>
          </div>

          {editingNote && (
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
              الملاحظة نشطة وتظهر للكاشير
            </label>
          )}
        </Card>

        <Card padding="none" className="overflow-hidden">
          {notes.length === 0 ? (
            <div className="p-10 text-center text-gray-500">
              لا توجد ملاحظات محفوظة بعد. أضف أول ملاحظة من النموذج بالأعلى.
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[640px]">
                <thead className="bg-gray-50 text-sm text-gray-600">
                  <tr>
                    <th className="px-4 py-3 text-start font-semibold">
                      الملاحظة
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
                  {notes.map((note) => (
                    <tr key={note.id} className="hover:bg-gray-50">
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-2 font-bold text-gray-900">
                          <MessageSquare className="h-4 w-4 text-amber-600" />
                          {note.text}
                        </div>
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-600">
                        {note.sortOrder}
                      </td>
                      <td className="px-4 py-3">
                        <span
                          className={clsx(
                            "rounded-full px-2.5 py-1 text-xs font-bold",
                            note.isActive
                              ? "bg-primary-50 text-primary-700"
                              : "bg-gray-100 text-gray-500",
                          )}
                        >
                          {note.isActive ? "نشطة" : "غير نشطة"}
                        </span>
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex items-center justify-center gap-1">
                          <button
                            type="button"
                            onClick={() => handleEdit(note)}
                            className="rounded-lg p-2 text-blue-600 transition hover:bg-blue-50"
                            title="تعديل"
                          >
                            <Pencil className="h-4 w-4" />
                          </button>
                          <button
                            type="button"
                            onClick={() => setDeletingNote(note)}
                            className="rounded-lg p-2 text-danger-600 transition hover:bg-danger-50"
                            title="حذف"
                          >
                            <Trash2 className="h-4 w-4" />
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
      </div>

      <ConfirmDialog
        open={deletingNote !== null}
        onOpenChange={(open) => !open && setDeletingNote(null)}
        onConfirm={handleConfirmDelete}
        title="حذف الملاحظة"
        description={
          deletingNote
            ? `هل تريد حذف ملاحظة "${deletingNote.text}"؟`
            : "هل تريد حذف هذه الملاحظة؟"
        }
        confirmText="حذف"
        cancelText="تراجع"
        isLoading={isDeleting}
      />
    </div>
  );
};

export default SavedOrderNotesPage;
