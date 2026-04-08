import { Category } from "@/types/category.types";
import clsx from "clsx";

interface CategoryChipsProps {
  categories: Category[];
  selectedId: number | null;
  onSelect: (id: number | null) => void;
}

export const CategoryChips = ({
  categories,
  selectedId,
  onSelect,
}: CategoryChipsProps) => {
  return (
    <div className="flex gap-1.5 overflow-x-auto pb-1 [scrollbar-width:none] [-ms-overflow-style:none]">
      <button
        type="button"
        onClick={() => onSelect(null)}
        className={clsx(
          "shrink-0 rounded-full border px-3 py-1.5 text-xs font-semibold transition-colors",
          selectedId === null
            ? "border-primary-600 bg-primary-600 text-white"
            : "border-gray-200 bg-white text-gray-700 hover:bg-gray-50",
        )}
      >
        الكل
      </button>

      {categories.map((category) => (
        <button
          key={category.id}
          type="button"
          onClick={() => onSelect(category.id)}
          className={clsx(
            "shrink-0 rounded-full border px-3 py-1.5 text-xs font-semibold transition-colors",
            selectedId === category.id
              ? "border-primary-600 bg-primary-600 text-white"
              : "border-gray-200 bg-white text-gray-700 hover:bg-gray-50",
          )}
        >
          {category.name}
        </button>
      ))}
    </div>
  );
};
