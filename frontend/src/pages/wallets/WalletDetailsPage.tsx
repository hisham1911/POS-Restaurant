import { useState } from "react";
import { useParams, Link } from "react-router-dom";
import {
  ArrowLeft,
  Wallet,
  CreditCard,
  Building2,
  ArrowDownLeft,
  ArrowUpRight,
  ChevronLeft,
  ChevronRight,
} from "lucide-react";
import {
  useGetWalletByIdQuery,
  useGetWalletTransactionsQuery,
} from "@/api/walletApi";
import { formatCurrency } from "@/utils/formatters";

export default function WalletDetailsPage() {
  const { id } = useParams<{ id: string }>();
  const walletId = parseInt(id ?? "0", 10);
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const { data: walletData, isLoading: isWalletLoading } = useGetWalletByIdQuery(walletId);
  const { data: txData, isLoading: isTxLoading } = useGetWalletTransactionsQuery({
    id: walletId,
    filters: { page, pageSize },
  });

  const wallet = walletData?.data;
  const txResult = txData?.data;

  const typeLabel = (type: string) => {
    switch (type) {
      case "BankAccount": return "حساب بنكي";
      case "Wallet": return "محفظة";
      default: return type;
    }
  };

  const typeIcon = (type: string) => {
    switch (type) {
      case "BankAccount": return <CreditCard className="h-5 w-5" />;
      case "Wallet": return <Wallet className="h-5 w-5" />;
      default: return <Wallet className="h-5 w-5" />;
    }
  };

  const txTypeLabel = (type: string) => {
    switch (type) {
      case "Deposit": return "إيداع";
      case "Withdrawal": return "سحب";
      case "OrderPayment": return "دفع طلب";
      default: return type;
    }
  };

  const txTypeClass = (type: string) => {
    switch (type) {
      case "Deposit":
      case "OrderPayment":
        return "text-emerald-700 bg-emerald-50";
      case "Withdrawal":
        return "text-danger-700 bg-danger-50";
      default:
        return "text-slate-700 bg-slate-100";
    }
  };

  if (isWalletLoading) {
    return (
      <div className="p-6">
        <div className="py-12 text-center text-slate-400">جاري التحميل...</div>
      </div>
    );
  }

  if (!wallet) {
    return (
      <div className="p-6">
        <div className="rounded-2xl border border-slate-200 bg-white p-12 text-center">
          <Wallet className="mx-auto h-12 w-12 text-slate-300" />
          <p className="mt-3 text-slate-500">المحفظة غير موجودة</p>
          <Link to="/wallets" className="mt-4 inline-block text-sm font-semibold text-primary-600 hover:underline">
            العودة للمحافظ
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center gap-3">
        <Link
          to="/wallets"
          className="flex h-9 w-9 items-center justify-center rounded-xl border border-slate-200 bg-white text-slate-500 transition hover:border-primary-200 hover:bg-primary-50 hover:text-primary-700"
        >
          <ArrowLeft className="h-4 w-4" />
        </Link>
        <div>
          <h1 className="text-2xl font-bold text-slate-900">{wallet.name}</h1>
          <p className="text-sm text-slate-500">
            {typeLabel(wallet.type)} · {wallet.isActive ? "نشطة" : "معطلة"}
          </p>
        </div>
      </div>

      <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <div className="flex items-center gap-4">
          <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-primary-50 text-primary-700">
            {typeIcon(wallet.type)}
          </div>
          <div>
            <p className="text-sm text-slate-500">الرصيد الحالي</p>
            <p className="text-3xl font-black text-slate-900">
              {formatCurrency(wallet.currentBalance)}
            </p>
          </div>
        </div>
        {wallet.accountNumber && (
          <p className="mt-3 text-sm text-slate-400">{wallet.accountNumber}</p>
        )}
        {wallet.notes && (
          <p className="mt-2 text-sm text-slate-500">{wallet.notes}</p>
        )}
      </div>

      <div>
        <h2 className="text-lg font-bold text-slate-900">حركات المحفظة</h2>
        <div className="mt-3 rounded-2xl border border-slate-200 bg-white shadow-sm">
          {isTxLoading ? (
            <div className="py-12 text-center text-slate-400">جاري التحميل...</div>
          ) : !txResult || txResult.items.length === 0 ? (
            <div className="py-12 text-center text-slate-400">لا توجد حركات مسجلة</div>
          ) : (
            <>
              <div className="divide-y divide-slate-100">
                {txResult.items.map((tx) => (
                  <div key={tx.id} className="flex items-center justify-between px-5 py-4">
                    <div className="flex items-center gap-3">
                      <div className={`flex h-9 w-9 items-center justify-center rounded-lg text-xs font-bold ${txTypeClass(tx.type)}`}>
                        {tx.type === "Deposit" || tx.type === "OrderPayment" ? (
                          <ArrowDownLeft className="h-4 w-4" />
                        ) : (
                          <ArrowUpRight className="h-4 w-4" />
                        )}
                      </div>
                      <div>
                        <p className="text-sm font-semibold text-slate-900">{txTypeLabel(tx.type)}</p>
                        <p className="text-xs text-slate-500">
                          {tx.description ?? tx.referenceNumber ?? "—"}
                        </p>
                        {tx.userName && (
                          <p className="text-[11px] text-slate-400">بواسطة: {tx.userName}</p>
                        )}
                      </div>
                    </div>
                    <div className="text-end">
                      <p className={`text-sm font-bold ${tx.type === "Deposit" || tx.type === "OrderPayment" ? "text-emerald-700" : "text-danger-700"}`}>
                        {tx.type === "Deposit" || tx.type === "OrderPayment" ? "+" : "-"}
                        {formatCurrency(tx.amount)}
                      </p>
                      <p className="text-xs text-slate-400">
                        الرصيد: {formatCurrency(tx.balanceAfter)}
                      </p>
                      <p className="text-[11px] text-slate-300">
                        {new Date(tx.createdAt).toLocaleString("ar-EG")}
                      </p>
                    </div>
                  </div>
                ))}
              </div>

              {txResult.totalPages > 1 && (
                <div className="flex items-center justify-between border-t border-slate-100 px-5 py-3">
                  <button
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    disabled={page <= 1}
                    className="flex items-center gap-1 rounded-lg px-3 py-1.5 text-sm font-medium text-slate-600 transition hover:bg-slate-50 disabled:opacity-40"
                  >
                    <ChevronRight className="h-4 w-4" />
                    السابق
                  </button>
                  <span className="text-sm text-slate-500">
                    صفحة {page} من {txResult.totalPages}
                  </span>
                  <button
                    onClick={() => setPage((p) => Math.min(txResult.totalPages, p + 1))}
                    disabled={page >= txResult.totalPages}
                    className="flex items-center gap-1 rounded-lg px-3 py-1.5 text-sm font-medium text-slate-600 transition hover:bg-slate-50 disabled:opacity-40"
                  >
                    التالي
                    <ChevronLeft className="h-4 w-4" />
                  </button>
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}
