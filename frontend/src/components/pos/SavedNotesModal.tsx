import { useState } from "react";
import { MessageSquare, X } from "lucide-react";
import { Portal } from "@/components/common/Portal";
import type { SavedOrderNote } from "@/types/restaurant.types";

interface SavedNotesModalProps {
  notes: SavedOrderNote[];
  onApply: (text: string) => void;
  onClose: () => void;
}

export const SavedNotesModal = ({
  notes,
  onApply,
  onClose,
}: SavedNotesModalProps) => {
  const [freeText, setFreeText] = useState("");

  const applyText = (text: string) => {
    const trimmed = text.trim();
    if (!trimmed) return;
    onApply(trimmed);
    onClose();
  };

  return (
    <Portal>
      <div
        className="fixed inset-0 z-[100] flex items-center justify-center bg-black/50 p-4"
        onClick={onClose}
      >
        <div
          className="w-full max-w-xl rounded-2xl bg-white shadow-2xl"
          onClick={(event) => event.stopPropagation()}
        >
          <div className="flex items-center justify-between border-b border-slate-200 p-5">
            <div className="flex items-center gap-3">
              <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-50 text-amber-600">
                <MessageSquare className="h-5 w-5" />
              </span>
              <div>
                <h2 className="text-lg font-black text-slate-900">ملاحظات سريعة</h2>
                <p className="text-sm text-slate-500">تظهر في تذكرة المطبخ وفاتورة العميل.</p>
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

          <div className="space-y-4 p-5">
            <div className="flex flex-wrap gap-2">
              {notes.filter((note) => note.isActive).length === 0 ? (
                <div className="w-full rounded-xl border border-dashed border-slate-200 bg-slate-50 p-4 text-center text-sm text-slate-500">
                  لا توجد ملاحظات محفوظة. يمكنك كتابة ملاحظة حرة الآن.
                </div>
              ) : (
                notes
                  .filter((note) => note.isActive)
                  .map((note) => (
                  <button
                    key={note.id}
                    type="button"
                    onClick={() => applyText(note.text)}
                    className="rounded-full border border-slate-200 bg-slate-50 px-3 py-2 text-sm font-semibold text-slate-700 transition hover:border-primary-300 hover:bg-primary-50 hover:text-primary-700"
                  >
                    {note.text}
                  </button>
                  ))
              )}
            </div>

            <div className="space-y-2">
              <label className="text-sm font-bold text-slate-700">ملاحظة حرة</label>
              <textarea
                value={freeText}
                onChange={(event) => setFreeText(event.target.value)}
                rows={3}
                className="w-full resize-none rounded-xl border border-slate-200 px-4 py-3 text-sm outline-none transition focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
                placeholder="اكتب ملاحظة الطلب..."
              />
            </div>

            <button
              type="button"
              onClick={() => applyText(freeText)}
              className="w-full rounded-xl bg-primary-600 px-4 py-3 text-sm font-bold text-white transition hover:bg-primary-700 disabled:cursor-not-allowed disabled:opacity-50"
              disabled={!freeText.trim()}
            >
              إضافة الملاحظة
            </button>
          </div>
        </div>
      </div>
    </Portal>
  );
};
