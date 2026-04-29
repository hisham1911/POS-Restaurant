import { useState } from "react";
import {
  useGetDeliveryPersonsQuery,
  useCreateDeliveryPersonMutation,
  useUpdateDeliveryPersonMutation,
  useDeleteDeliveryPersonMutation,
} from "@/api/deliveryApi";
import { Button } from "@/components/common/Button";
import { Loading } from "@/components/common/Loading";
import { Modal } from "@/components/common/Modal";
import { Input } from "@/components/common/Input";
import { toast } from "sonner";
import { Truck, Plus, Pencil, Trash2, Search } from "lucide-react";

export default function DeliveryPersonsPage() {
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [form, setForm] = useState({ name: "", phone: "", vehicleInfo: "", isActive: true });

  const { data, isLoading } = useGetDeliveryPersonsQuery({ page, pageSize: 20, search });
  const [createPerson, { isLoading: isCreating }] = useCreateDeliveryPersonMutation();
  const [updatePerson, { isLoading: isUpdating }] = useUpdateDeliveryPersonMutation();
  const [deletePerson] = useDeleteDeliveryPersonMutation();

  const persons = data?.data?.items || [];
  const totalPages = data?.data?.totalPages || 1;

  const resetForm = () => {
    setForm({ name: "", phone: "", vehicleInfo: "", isActive: true });
    setEditingId(null);
  };

  const openCreate = () => {
    resetForm();
    setIsModalOpen(true);
  };

  const openEdit = (person: typeof persons[0]) => {
    setForm({ name: person.name, phone: person.phone, vehicleInfo: person.vehicleInfo || "", isActive: person.isActive });
    setEditingId(person.id);
    setIsModalOpen(true);
  };

  const handleSubmit = async () => {
    if (!form.name.trim() || !form.phone.trim()) {
      toast.error("الاسم ورقم الهاتف مطلوبان");
      return;
    }
    try {
      if (editingId) {
        await updatePerson({ id: editingId, body: form }).unwrap();
        toast.success("تم تحديث المندوب");
      } else {
        await createPerson(form).unwrap();
        toast.success("تم إضافة المندوب");
      }
      setIsModalOpen(false);
      resetForm();
    } catch {
      toast.error("حدث خطأ");
    }
  };

  const handleDelete = async (id: number) => {
    if (!confirm("هل أنت متأكد من الحذف؟")) return;
    try {
      await deletePerson(id).unwrap();
      toast.success("تم الحذف");
    } catch {
      toast.error("حدث خطأ أثناء الحذف");
    }
  };

  if (isLoading) return <Loading />;

  return (
    <div className="p-6 max-w-6xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-2">
          <Truck className="w-6 h-6 text-blue-600" />
          <h1 className="text-xl font-bold">المناديب والتوصيل</h1>
        </div>
        <Button onClick={openCreate} className="flex items-center gap-2">
          <Plus className="w-4 h-4" />
          إضافة مندوب
        </Button>
      </div>

      <div className="bg-white rounded-lg border border-gray-200 p-4 mb-4">
        <div className="relative">
          <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
          <input
            type="text"
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            placeholder="بحث باسم أو هاتف..."
            className="w-full pr-10 pl-3 py-2 border rounded-lg"
          />
        </div>
      </div>

      <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-gray-50">
            <tr>
              <th className="px-4 py-3 text-right font-medium text-gray-600">الاسم</th>
              <th className="px-4 py-3 text-right font-medium text-gray-600">الهاتف</th>
              <th className="px-4 py-3 text-right font-medium text-gray-600">المركبة</th>
              <th className="px-4 py-3 text-right font-medium text-gray-600">الحالة</th>
              <th className="px-4 py-3 text-right font-medium text-gray-600">إجراءات</th>
            </tr>
          </thead>
          <tbody>
            {persons.length === 0 ? (
              <tr>
                <td colSpan={5} className="px-4 py-8 text-center text-gray-500">
                  لا يوجد مناديب
                </td>
              </tr>
            ) : (
              persons.map((p) => (
                <tr key={p.id} className="border-t hover:bg-gray-50">
                  <td className="px-4 py-3 font-medium">{p.name}</td>
                  <td className="px-4 py-3">{p.phone}</td>
                  <td className="px-4 py-3 text-gray-500">{p.vehicleInfo || "-"}</td>
                  <td className="px-4 py-3">
                    <span className={`inline-block px-2 py-0.5 rounded text-xs ${p.isActive ? "bg-green-100 text-green-700" : "bg-gray-100 text-gray-600"}`}>
                      {p.isActive ? "نشط" : "غير نشط"}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex gap-2">
                      <button onClick={() => openEdit(p)} className="p-1 text-blue-600 hover:bg-blue-50 rounded">
                        <Pencil className="w-4 h-4" />
                      </button>
                      <button onClick={() => handleDelete(p.id)} className="p-1 text-red-600 hover:bg-red-50 rounded">
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
        <div className="flex justify-center gap-2 mt-4">
          {Array.from({ length: totalPages }, (_, i) => (
            <button
              key={i + 1}
              onClick={() => setPage(i + 1)}
              className={`px-3 py-1 rounded border ${page === i + 1 ? "bg-blue-600 text-white border-blue-600" : "bg-white text-gray-700"}`}
            >
              {i + 1}
            </button>
          ))}
        </div>
      )}

      <Modal isOpen={isModalOpen} onClose={() => setIsModalOpen(false)} title={editingId ? "تعديل مندوب" : "إضافة مندوب جديد"}>
        <div className="space-y-4 p-4">
          <Input label="الاسم *" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
          <Input label="رقم الهاتف *" value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} />
          <Input label="بيانات المركبة" value={form.vehicleInfo} onChange={(e) => setForm({ ...form, vehicleInfo: e.target.value })} />
          {editingId && (
            <label className="flex items-center gap-2">
              <input
                type="checkbox"
                checked={form.isActive}
                onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
              />
              <span className="text-sm">نشط</span>
            </label>
          )}
          <div className="flex justify-end gap-2 mt-4">
            <Button variant="outline" onClick={() => setIsModalOpen(false)}>إلغاء</Button>
            <Button onClick={handleSubmit} disabled={isCreating || isUpdating}>
              {editingId ? "حفظ التعديلات" : "إضافة"}
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
