import { X, Armchair } from "lucide-react";
import clsx from "clsx";
import { Portal } from "@/components/common/Portal";
import type { RestaurantTable } from "@/types/restaurant.types";

interface TableSelectionModalProps {
  tables: RestaurantTable[];
  selectedTableId?: number;
  onSelect: (table: RestaurantTable) => void;
  onClose: () => void;
}

const isOccupied = (table: RestaurantTable) =>
  table.status === "Occupied" || table.status === 1;

export const TableSelectionModal = ({
  tables,
  selectedTableId,
  onSelect,
  onClose,
}: TableSelectionModalProps) => {
  const availableTables = tables.filter(
    (table) => table.isActive && !isOccupied(table),
  );

  return (
    <Portal>
      <div
        className="fixed inset-0 z-[100] flex items-center justify-center bg-black/50 p-4"
        onClick={onClose}
      >
        <div
          className="w-full max-w-3xl rounded-2xl bg-white shadow-2xl"
          onClick={(event) => event.stopPropagation()}
        >
          <div className="flex items-center justify-between border-b border-slate-200 p-5">
            <div className="flex items-center gap-3">
              <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary-50 text-primary-600">
                <Armchair className="h-5 w-5" />
              </span>
              <div>
                <h2 className="text-lg font-black text-slate-900">
                  اختيار الطاولة
                </h2>
                <p className="text-sm text-slate-500">
                  اختر طاولة متاحة لبدء طلب صالة جديد.
                </p>
              </div>
            </div>
            <button
              type="button"
              onClick={onClose}
              className="rounded-lg p-2 text-slate-500 transition hover:bg-slate-100"
            >
              <X className="h-5 w-5" />
            </button>
          </div>

          <div className="grid max-h-[65vh] gap-3 overflow-y-auto p-5 sm:grid-cols-2 lg:grid-cols-3">
            {availableTables.length === 0 ? (
              <div className="col-span-full rounded-xl border border-dashed border-slate-200 bg-slate-50 p-8 text-center text-sm text-slate-500">
                لا توجد طاولات متاحة الآن.
              </div>
            ) : (
              availableTables.map((table) => {
                const selected = selectedTableId === table.id;
                return (
                  <button
                    key={table.id}
                    type="button"
                    onClick={() => onSelect(table)}
                    className={clsx(
                      "min-h-[96px] rounded-xl border p-4 text-start transition",
                      selected &&
                        "border-primary-500 bg-primary-50 ring-2 ring-primary-100",
                      !selected &&
                        "border-emerald-200 bg-emerald-50 hover:border-emerald-400",
                    )}
                  >
                    <div className="flex items-center justify-between gap-3">
                      <span className="text-2xl font-black text-slate-900">
                        طاولة {table.number}
                      </span>
                      <span className="rounded-full bg-emerald-100 px-2 py-1 text-xs font-bold text-emerald-700">
                        متاحة
                      </span>
                    </div>
                  </button>
                );
              })
            )}
          </div>
        </div>
      </div>
    </Portal>
  );
};
