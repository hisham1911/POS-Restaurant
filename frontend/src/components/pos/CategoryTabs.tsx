import { Category } from "@/types/category.types";
import clsx from "clsx";

interface CategoryTabsProps {
  categories: Category[];
  selectedId: number | null;
  onSelect: (id: number | null) => void;
}

export const CategoryTabs = ({ categories, selectedId, onSelect }: CategoryTabsProps) => {
  return (
    <div className="flex gap-2 pb-1">
      {/* All */}
      <button
        onClick={() => onSelect(null)}
        className={clsx(
          "px-4 py-2 rounded-lg text-sm font-bold whitespace-nowrap border-2 shrink-0",
          selectedId === null
            ? "bg-primary-600 text-white border-primary-500"
            : "bg-white text-gray-700 border-gray-300 hover:border-primary-400 hover:bg-primary-50"
        )}
        aria-pressed={selectedId === null}
      >
        🏪 الكل
      </button>

      {/* Categories */}
      {categories.map((category) => (
        <button
          key={category.id}
          onClick={() => onSelect(category.id)}
          className={clsx(
            "px-4 py-2 rounded-lg text-sm font-bold whitespace-nowrap border-2 shrink-0",
            selectedId === category.id
              ? "bg-primary-600 text-white border-primary-500"
              : "bg-white text-gray-700 border-gray-300 hover:border-primary-400 hover:bg-primary-50"
          )}
          aria-pressed={selectedId === category.id}
        >
          {category.imageUrl || "📁"} {category.name}
        </button>
      ))}
    </div>
  );
};
