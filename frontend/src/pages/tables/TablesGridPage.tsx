import { Armchair, ClipboardList, Plus } from "lucide-react";
import { useNavigate } from "react-router-dom";
import clsx from "clsx";
import { toast } from "sonner";
import { useGetRestaurantTablesQuery } from "@/api/restaurantTablesApi";
import { Card } from "@/components/common/Card";
import { Loading } from "@/components/common/Loading";
import { useAppSelector } from "@/store/hooks";
import { selectCurrentBranch } from "@/store/slices/branchSlice";
import type { RestaurantTable } from "@/types/restaurant.types";

const isOccupied = (table: RestaurantTable) =>
  table.status === "Occupied" || table.status === 1;

const TablesGridPage = () => {
  const navigate = useNavigate();
  const currentBranch = useAppSelector(selectCurrentBranch);
  const { data, isLoading } = useGetRestaurantTablesQuery(
    currentBranch?.id ?? 0,
    { skip: !currentBranch?.id },
  );

  const tables = (data?.data ?? []).filter((table) => table.isActive);
  const availableCount = tables.filter((table) => !isOccupied(table)).length;
  const occupiedCount = tables.length - availableCount;

  const handleTableClick = (table: RestaurantTable) => {
    if (isOccupied(table)) {
      if (!table.openOrderId) {
        toast.error("هذه الطاولة مشغولة ولا يوجد طلب حالي مرتبط بها");
        return;
      }

      navigate("/orders", { state: { openOrderId: table.openOrderId } });
      return;
    }

    navigate("/pos-workspace", { state: { selectedTable: table } });
  };

  if (!currentBranch?.id) {
    return (
      <div className="p-6">
        <Card className="text-center text-sm text-gray-600">
          اختر فرعًا أولًا لعرض الطاولات.
        </Card>
      </div>
    );
  }

  if (isLoading) {
    return <Loading />;
  }

  return (
    <div className="min-h-full bg-slate-50 p-4 sm:p-6">
      <div className="mx-auto max-w-7xl space-y-5">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-3">
            <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-primary-50 text-primary-700">
              <Armchair className="h-6 w-6" />
            </div>
            <div>
              <h1 className="text-2xl font-black text-slate-900">الطاولات</h1>
              <p className="text-sm text-slate-500">
                اختر طاولة متاحة لبدء طلب صالة، أو افتح الطلب الحالي للطاولة المشغولة.
              </p>
            </div>
          </div>

          <button
            type="button"
            onClick={() => navigate("/settings/tables")}
            className="inline-flex min-h-[44px] items-center justify-center gap-2 rounded-2xl border border-primary-200 bg-white px-4 py-2 text-sm font-bold text-primary-700 transition hover:bg-primary-50"
          >
            <Plus className="h-4 w-4" />
            إدارة الطاولات
          </button>
        </div>

        <div className="grid gap-3 sm:grid-cols-3">
          <Card>
            <p className="text-sm text-gray-500">كل الطاولات</p>
            <p className="mt-1 text-2xl font-black text-slate-900">
              {tables.length}
            </p>
          </Card>
          <Card className="border-emerald-100">
            <p className="text-sm text-gray-500">متاحة</p>
            <p className="mt-1 text-2xl font-black text-emerald-600">
              {availableCount}
            </p>
          </Card>
          <Card className="border-red-100">
            <p className="text-sm text-gray-500">مشغولة</p>
            <p className="mt-1 text-2xl font-black text-red-600">
              {occupiedCount}
            </p>
          </Card>
        </div>

        {tables.length === 0 ? (
          <Card className="py-12 text-center">
            <ClipboardList className="mx-auto mb-3 h-10 w-10 text-slate-300" />
            <p className="font-bold text-slate-800">لا توجد طاولات بعد</p>
            <p className="mt-1 text-sm text-slate-500">
              أضف الطاولات من الإعدادات ثم ارجع لهذه الشاشة.
            </p>
          </Card>
        ) : (
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-5">
            {tables.map((table) => {
              const occupied = isOccupied(table);

              return (
                <button
                  key={table.id}
                  type="button"
                  onClick={() => handleTableClick(table)}
                  className={clsx(
                    "min-h-[132px] rounded-2xl border p-4 text-start shadow-sm transition hover:-translate-y-0.5 hover:shadow-md",
                    occupied
                      ? "border-red-200 bg-red-50 text-red-900"
                      : "border-emerald-200 bg-emerald-50 text-emerald-900",
                    !table.isActive && "opacity-60",
                  )}
                >
                  <div className="flex items-start justify-between gap-3">
                    <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-white/80">
                      <Armchair className="h-5 w-5" />
                    </span>
                    <span
                      className={clsx(
                        "rounded-full px-2.5 py-1 text-xs font-bold",
                        occupied
                          ? "bg-red-100 text-red-700"
                          : "bg-emerald-100 text-emerald-700",
                      )}
                    >
                      {occupied ? "مشغولة" : "متاحة"}
                    </span>
                  </div>

                  <p className="mt-5 text-3xl font-black">طاولة {table.number}</p>
                  <p className="mt-2 text-sm font-semibold opacity-80">
                    {occupied
                      ? table.openOrderNumber
                        ? `عرض الطلب #${table.openOrderNumber}`
                        : "عرض الطلب الحالي"
                      : "بدء طلب صالة"}
                  </p>
                </button>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
};

export default TablesGridPage;
