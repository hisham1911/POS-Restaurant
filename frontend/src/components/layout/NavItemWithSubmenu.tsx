import { useState } from "react";
import { NavLink, useLocation } from "react-router-dom";
import { ChevronDown, ChevronUp, LucideIcon } from "lucide-react";
import clsx from "clsx";

interface SubItem {
  path: string;
  label: string;
}

interface NavItemWithSubmenuProps {
  path: string;
  label: string;
  icon: LucideIcon;
  subItems: SubItem[];
  onItemClick?: () => void;
}

export const NavItemWithSubmenu = ({
  path,
  label,
  icon: Icon,
  subItems,
  onItemClick,
}: NavItemWithSubmenuProps) => {
  const location = useLocation();
  const isActive = location.pathname.startsWith(path);
  const [isOpen, setIsOpen] = useState(isActive);

  return (
    <div>
      <button
        onClick={() => setIsOpen(!isOpen)}
        className={clsx(
          "w-full flex items-center justify-between gap-3 px-4 py-3 rounded-lg transition-colors",
          isActive
            ? "bg-primary-600 text-white"
            : "text-gray-300 hover:bg-gray-800"
        )}
      >
        <div className="flex items-center gap-3">
          <Icon className="w-5 h-5" />
          <span>{label}</span>
        </div>
        {isOpen ? (
          <ChevronUp className="w-4 h-4" />
        ) : (
          <ChevronDown className="w-4 h-4" />
        )}
      </button>

      {isOpen && (
        <div className="mt-1 mr-4 space-y-1">
          {subItems.map((subItem) => (
            <NavLink
              key={subItem.path}
              to={subItem.path}
              onClick={onItemClick}
              className={({ isActive }) =>
                clsx(
                  "block px-4 py-2 rounded-lg text-sm transition-colors",
                  isActive
                    ? "bg-primary-500 text-white"
                    : "text-gray-400 hover:bg-gray-800 hover:text-gray-200"
                )
              }
            >
              {subItem.label}
            </NavLink>
          ))}
        </div>
      )}
    </div>
  );
};
