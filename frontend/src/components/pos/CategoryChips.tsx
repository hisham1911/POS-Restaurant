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
    <div className="flex flex-wrap gap-2">
      {/* All Categories */}
      <button
        onClick={() => onSelect(null)}
        className={clsx(
          "px-4 py-2 rounded-full text-sm font-medium transition-all duration-200",
          selectedId === null
            ? "bg-primary-600 text-white shadow-md scale-105"
            : "bg-gray-100 text-gray-700 hover:bg-gray-200",
        )}
      >
        الكل
      </button>

      {/* Category Chips */}
      {categories.map((category) => (
        <button
          key={category.id}
          onClick={() => onSelect(category.id)}
          className={clsx(
            "px-4 py-2 rounded-full text-sm font-medium transition-all duration-200",
            "border-2",
            selectedId === category.id
              ? "bg-primary-600 text-white border-primary-600 shadow-md scale-105"
              : "bg-white text-gray-700 border-gray-200 hover:border-primary-300 hover:bg-primary-50",
          )}
        >
          {category.imageUrl || "📁"} {category.name}
        </button>
      ))}
    </div>
  );
};
