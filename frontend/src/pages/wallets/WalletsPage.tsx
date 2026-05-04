import { useState } from "react";
import {
  Plus,
  Wallet as WalletIcon,
  Pencil,
  Trash2,
  ArrowDownLeft,
  ArrowUpRight,
  Eye,
  X,
  Check,
  Building2,
  CreditCard,
} from "lucide-react";
import { Link } from "react-router-dom";
import { toast } from "sonner";
import { usePermission } from "@/hooks/usePermission";
import {
  useGetWalletsQuery,
  useCreateWalletMutation,
  useUpdateWalletMutation,
  useDeleteWalletMutation,
  useDepositWalletMutation,
  useWithdrawWalletMutation,
} from "@/api/walletApi";
import { Button } from "@/components/common/Button";
import { formatCurrency } from "@/utils/formatters";
import type { Wallet as WalletType, CreateWalletRequest } from "@/types/wallet.types";

export default function WalletsPage() {
  const { data, isLoading, refetch } = useGetWalletsQuery();
  const wallets = data?.data ?? [];
  const { hasPermission } = usePermission();
  const canManage = hasPermission("WalletManage");

  const [showCreate, setShowCreate] = useState(false);
  const [editingWallet, setEditingWallet] = useState<WalletType | null>(null);
  const [depositWallet, setDepositWallet] = useState<WalletType | null>(null);
  const [withdrawWallet, setWithdrawWallet] = useState<WalletType | null>(null);

  const [createForm, setCreateForm] = useState<CreateWalletRequest>({
    name: "",
    type: "BankAccount",
    initialBalance: 0,
    accountNumber: "",
    notes: "",
  });

  const [editName, setEditName] = useState("");
  const [editActive, setEditActive] = useState(true);
  const [editNotes, setEditNotes] = useState("");
  const [editAccountNumber, setEditAccountNumber] = useState("");

  const [txAmount, setTxAmount] = useState("");
  const [txDescription, setTxDescription] = useState("");

  const [createWallet, { isLoading: isCreating }] = useCreateWalletMutation();
  const [updateWallet, { isLoading: isUpdating }] = useUpdateWalletMutation();
  const [deleteWallet, { isLoading: isDeleting }] = useDeleteWalletMutation();
  const [deposit, { isLoading: isDepositing }] = useDepositWalletMutation();
  const [withdraw, { isLoading: isWithdrawing }] = useWithdrawWalletMutation();

  const handleCreate = async () => {
    if (!createForm.name.trim()) {
      toast.error("اسم المحفظة مطلوب");
      return;
    }
    try {
      await createWallet(createForm).unwrap();
      toast.success("تم إنشاء المحفظة بنجاح");
      setShowCreate(false);
      setCreateForm({ name: "", type: "BankAccount", initialBalance: 0, accountNumber: "", notes: "" });
      refetch();
    } catch {
      // error handled by baseApi
    }
  };

  const handleUpdate = async () => {
    if (!editingWallet) return;
    if (!editName.trim()) {
      toast.error("اسم المحفظة مطلوب");
      return;
    }
    try {
      await updateWallet({
        id: editingWallet.id,
        body: { name: editName, isActive: editActive, notes: editNotes, accountNumber: editAccountNumber },
      }).unwrap();
      toast.success("تم تحديث المحفظة بنجاح");
      setEditingWallet(null);
      refetch();
    } catch {
      // error handled by baseApi
    }
  };

  const handleDelete = async (wallet: WalletType) => {
    if (!confirm(`هل أنت متأكد من حذف المحفظة "${wallet.name}"؟`)) return;
    try {
      await deleteWallet(wallet.id).unwrap();
      toast.success("تم حذف المحفظة بنجاح");
      refetch();
    } catch {
      // error handled by baseApi
    }
  };

  const handleDeposit = async () => {
    if (!depositWallet) return;
    const amount = parseFloat(txAmount);
    if (!amount || amount <= 0) {
      toast.error("المبلغ غير صحيح");
      return;
    }
    try {
      await deposit({ id: depositWallet.id, body: { amount, description: txDescription } }).unwrap();
      toast.success("تم الإيداع بنجاح");
      setDepositWallet(null);
      setTxAmount("");
      setTxDescription("");
      refetch();
    } catch {
      // error handled by baseApi
    }
  };

  const handleWithdraw = async () => {
    if (!withdrawWallet) return;
    const amount = parseFloat(txAmount);
    if (!amount || amount <= 0) {
      toast.error("المبلغ غير صحيح");
      return;
    }
    try {
      await withdraw({ id: withdrawWallet.id, body: { amount, description: txDescription } }).unwrap();
      toast.success("تم السحب بنجاح");
      setWithdrawWallet(null);
      setTxAmount("");
      setTxDescription("");
      refetch();
    } catch {
      // error handled by baseApi
    }
  };

  const typeLabel = (type: string) => {
    switch (type) {
      case "BankAccount": return "حساب بنكي";
      case "Wallet": return "محفظة";
      default: return type;
    }
  };

  const typeIcon = (type: string) => {
    switch (type) {
      case "BankAccount": return <CreditCard className="h-4 w-4" />;
      case "Wallet": return <WalletIcon className="h-4 w-4" />;
      default: return <WalletIcon className="h-4 w-4" />;
    }
  };

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">المحافظ</h1>
          <p className="text-sm text-slate-500">إدارة محافظ الدفع الإلكتروني وتتبع أرصدتها</p>
        </div>
        {canManage && (
          <Button onClick={() => setShowCreate(true)}>
            <Plus className="h-4 w-4" />
            إضافة محفظة
          </Button>
        )}
      </div>

      {isLoading ? (
        <div className="py-12 text-center text-slate-400">جاري التحميل...</div>
      ) : wallets.length === 0 ? (
        <div className="rounded-2xl border border-slate-200 bg-white p-12 text-center">
          <WalletIcon className="mx-auto h-12 w-12 text-slate-300" />
          <p className="mt-3 text-slate-500">لا توجد محافظ مسجلة</p>
          {canManage && (
            <Button className="mt-4" variant="outline" onClick={() => setShowCreate(true)}>
              <Plus className="h-4 w-4" />
              إضافة محفظة جديدة
            </Button>
          )}
        </div>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {wallets.map((wallet) => (
            <div
              key={wallet.id}
              className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm transition hover:shadow-md"
            >
              <div className="flex items-start justify-between">
                <div className="flex items-center gap-3">
                  <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary-50 text-primary-700">
                    {typeIcon(wallet.type)}
                  </div>
                  <div>
                    <h3 className="font-bold text-slate-900">{wallet.name}</h3>
                    <p className="text-xs text-slate-500">{typeLabel(wallet.type)}</p>
                  </div>
                </div>
                <span
                  className={`rounded-full px-2 py-0.5 text-xs font-semibold ${
                    wallet.isActive
                      ? "bg-emerald-50 text-emerald-700"
                      : "bg-slate-100 text-slate-500"
                  }`}
                >
                  {wallet.isActive ? "نشطة" : "معطلة"}
                </span>
              </div>

              <div className="mt-4">
                <p className="text-sm text-slate-500">الرصيد الحالي</p>
                <p className="text-2xl font-black text-slate-900">
                  {formatCurrency(wallet.currentBalance)}
                </p>
              </div>

              {wallet.accountNumber && (
                <p className="mt-1 text-xs text-slate-400">{wallet.accountNumber}</p>
              )}

              <div className="mt-4 flex flex-wrap gap-2">
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => {
                    setDepositWallet(wallet);
                    setTxAmount("");
                    setTxDescription("");
                  }}
                >
                  <ArrowDownLeft className="h-3.5 w-3.5" />
                  إيداع
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => {
                    setWithdrawWallet(wallet);
                    setTxAmount("");
                    setTxDescription("");
                  }}
                >
                  <ArrowUpRight className="h-3.5 w-3.5" />
                  سحب
                </Button>
                <Link to={`/wallets/${wallet.id}`}>
                  <Button size="sm" variant="ghost">
                    <Eye className="h-3.5 w-3.5" />
                    التفاصيل
                  </Button>
                </Link>
                {canManage && (
                  <>
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={() => {
                        setEditingWallet(wallet);
                        setEditName(wallet.name);
                        setEditActive(wallet.isActive);
                        setEditNotes(wallet.notes ?? "");
                        setEditAccountNumber(wallet.accountNumber ?? "");
                      }}
                    >
                      <Pencil className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      size="sm"
                      variant="ghost"
                      className="text-danger-600 hover:text-danger-700"
                      onClick={() => handleDelete(wallet)}
                      isLoading={isDeleting}
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </Button>
                  </>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Create Modal */}
      {showCreate && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4">
          <div className="w-full max-w-md rounded-2xl border border-slate-200 bg-white p-6 shadow-xl">
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-bold text-slate-900">إضافة محفظة جديدة</h2>
              <button onClick={() => setShowCreate(false)} className="rounded-lg p-1 hover:bg-slate-100">
                <X className="h-5 w-5 text-slate-500" />
              </button>
            </div>
            <div className="mt-4 space-y-3">
              <div>
                <label className="block text-sm font-semibold text-slate-900">الاسم</label>
                <input
                  className="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2 text-sm outline-none focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
                  value={createForm.name}
                  onChange={(e) => setCreateForm({ ...createForm, name: e.target.value })}
                  placeholder="مثلاً: فيزا CIB"
                />
              </div>
              <div>
                <label className="block text-sm font-semibold text-slate-900">النوع</label>
                <select
                  className="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2 text-sm outline-none focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
                  value={createForm.type}
                  onChange={(e) => setCreateForm({ ...createForm, type: e.target.value })}
                >
                  <option value="BankAccount">حساب بنكي</option>
                  <option value="Wallet">محفظة</option>
                </select>
              </div>
              <div>
                <label className="block text-sm font-semibold text-slate-900">رقم الحساب (اختياري)</label>
                <input
                  className="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2 text-sm outline-none focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
                  value={createForm.accountNumber ?? ""}
                  onChange={(e) => setCreateForm({ ...createForm, accountNumber: e.target.value })}
                  placeholder="رقم البطاقة أو الحساب"
                />
              </div>
              <div>
                <label className="block text-sm font-semibold text-slate-900">الرصيد الافتتاحي</label>
                <input
                  type="number"
                  min="0"
                  step="0.01"
                  className="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2 text-sm outline-none focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
                  value={createForm.initialBalance}
                  onChange={(e) => setCreateForm({ ...createForm, initialBalance: parseFloat(e.target.value) || 0 })}
                />
              </div>
              <div>
                <label className="block text-sm font-semibold text-slate-900">ملاحظات (اختياري)</label>
                <input
                  className="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2 text-sm outline-none focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
                  value={createForm.notes ?? ""}
                  onChange={(e) => setCreateForm({ ...createForm, notes: e.target.value })}
                />
              </div>
            </div>
            <div className="mt-6 flex gap-2">
              <Button className="flex-1" onClick={handleCreate} isLoading={isCreating}>
                <Check className="h-4 w-4" />
                حفظ
              </Button>
              <Button variant="outline" onClick={() => setShowCreate(false)}>
                إلغاء
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* Edit Modal */}
      {editingWallet && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4">
          <div className="w-full max-w-md rounded-2xl border border-slate-200 bg-white p-6 shadow-xl">
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-bold text-slate-900">تعديل محفظة</h2>
              <button onClick={() => setEditingWallet(null)} className="rounded-lg p-1 hover:bg-slate-100">
                <X className="h-5 w-5 text-slate-500" />
              </button>
            </div>
            <div className="mt-4 space-y-3">
              <div>
                <label className="block text-sm font-semibold text-slate-900">الاسم</label>
                <input
                  className="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2 text-sm outline-none focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
                  value={editName}
                  onChange={(e) => setEditName(e.target.value)}
                />
              </div>
              <div>
                <label className="block text-sm font-semibold text-slate-900">رقم الحساب</label>
                <input
                  className="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2 text-sm outline-none focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
                  value={editAccountNumber}
                  onChange={(e) => setEditAccountNumber(e.target.value)}
                />
              </div>
              <div className="flex items-center gap-2">
                <input
                  id="editActive"
                  type="checkbox"
                  checked={editActive}
                  onChange={(e) => setEditActive(e.target.checked)}
                  className="h-4 w-4 rounded border-slate-300 text-primary-600 focus:ring-primary-500"
                />
                <label htmlFor="editActive" className="text-sm text-slate-700">نشطة</label>
              </div>
              <div>
                <label className="block text-sm font-semibold text-slate-900">ملاحظات</label>
                <input
                  className="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2 text-sm outline-none focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
                  value={editNotes}
                  onChange={(e) => setEditNotes(e.target.value)}
                />
              </div>
            </div>
            <div className="mt-6 flex gap-2">
              <Button className="flex-1" onClick={handleUpdate} isLoading={isUpdating}>
                <Check className="h-4 w-4" />
                تحديث
              </Button>
              <Button variant="outline" onClick={() => setEditingWallet(null)}>
                إلغاء
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* Deposit Modal */}
      {depositWallet && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4">
          <div className="w-full max-w-sm rounded-2xl border border-slate-200 bg-white p-6 shadow-xl">
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-bold text-slate-900">إيداع — {depositWallet.name}</h2>
              <button onClick={() => setDepositWallet(null)} className="rounded-lg p-1 hover:bg-slate-100">
                <X className="h-5 w-5 text-slate-500" />
              </button>
            </div>
            <div className="mt-4 space-y-3">
              <div>
                <label className="block text-sm font-semibold text-slate-900">المبلغ</label>
                <input
                  type="number"
                  min="0.01"
                  step="0.01"
                  className="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2 text-sm outline-none focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
                  value={txAmount}
                  onChange={(e) => setTxAmount(e.target.value)}
                  autoFocus
                />
              </div>
              <div>
                <label className="block text-sm font-semibold text-slate-900">الوصف (اختياري)</label>
                <input
                  className="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2 text-sm outline-none focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
                  value={txDescription}
                  onChange={(e) => setTxDescription(e.target.value)}
                  placeholder="سبب الإيداع"
                />
              </div>
            </div>
            <div className="mt-6 flex gap-2">
              <Button className="flex-1" onClick={handleDeposit} isLoading={isDepositing}>
                <ArrowDownLeft className="h-4 w-4" />
                إيداع
              </Button>
              <Button variant="outline" onClick={() => setDepositWallet(null)}>
                إلغاء
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* Withdraw Modal */}
      {withdrawWallet && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4">
          <div className="w-full max-w-sm rounded-2xl border border-slate-200 bg-white p-6 shadow-xl">
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-bold text-slate-900">سحب — {withdrawWallet.name}</h2>
              <button onClick={() => setWithdrawWallet(null)} className="rounded-lg p-1 hover:bg-slate-100">
                <X className="h-5 w-5 text-slate-500" />
              </button>
            </div>
            <div className="mt-4 space-y-3">
              <div>
                <label className="block text-sm font-semibold text-slate-900">المبلغ</label>
                <input
                  type="number"
                  min="0.01"
                  step="0.01"
                  className="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2 text-sm outline-none focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
                  value={txAmount}
                  onChange={(e) => setTxAmount(e.target.value)}
                  autoFocus
                />
              </div>
              <div>
                <label className="block text-sm font-semibold text-slate-900">الوصف (اختياري)</label>
                <input
                  className="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2 text-sm outline-none focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
                  value={txDescription}
                  onChange={(e) => setTxDescription(e.target.value)}
                  placeholder="سبب السحب"
                />
              </div>
            </div>
            <div className="mt-6 flex gap-2">
              <Button className="flex-1" variant="danger" onClick={handleWithdraw} isLoading={isWithdrawing}>
                <ArrowUpRight className="h-4 w-4" />
                سحب
              </Button>
              <Button variant="outline" onClick={() => setWithdrawWallet(null)}>
                إلغاء
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
