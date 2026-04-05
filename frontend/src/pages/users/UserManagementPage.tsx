import { useState } from "react";
import { Users, Shield } from "lucide-react";
import UserManagementCard from "./components/UserManagementCard";
import PermissionsManagementCard from "./components/PermissionsManagementCard";

export default function UserManagementPage() {
  const [activeCard, setActiveCard] = useState<"users" | "permissions">(
    "users",
  );

  return (
    <div className="container mx-auto p-6" dir="rtl">
      <div className="mb-6">
        <h1 className="text-3xl font-bold mb-2">إدارة المستخدمين</h1>
        <p className="text-gray-600">إدارة حسابات المستخدمين وصلاحياتهم</p>
      </div>

      {/* Card Selector */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
        <button
          onClick={() => setActiveCard("users")}
          className={`p-6 rounded-xl border-2 transition-all text-right ${
            activeCard === "users"
              ? "border-blue-500 bg-blue-50 shadow-md"
              : "border-gray-200 hover:border-gray-300 bg-white"
          }`}
        >
          <div className="flex items-center gap-3">
            <div
              className={`p-3 rounded-lg ${
                activeCard === "users" ? "bg-blue-100" : "bg-gray-100"
              }`}
            >
              <Users
                className={`w-6 h-6 ${
                  activeCard === "users" ? "text-blue-600" : "text-gray-600"
                }`}
              />
            </div>
            <div className="flex-1">
              <h3 className="font-bold text-lg mb-1">إدارة المستخدمين</h3>
              <p className="text-sm text-gray-600">
                إضافة وتعديل وحذف حسابات المستخدمين
              </p>
            </div>
          </div>
        </button>

        <button
          onClick={() => setActiveCard("permissions")}
          className={`p-6 rounded-xl border-2 transition-all text-right ${
            activeCard === "permissions"
              ? "border-green-500 bg-green-50 shadow-md"
              : "border-gray-200 hover:border-gray-300 bg-white"
          }`}
        >
          <div className="flex items-center gap-3">
            <div
              className={`p-3 rounded-lg ${
                activeCard === "permissions" ? "bg-green-100" : "bg-gray-100"
              }`}
            >
              <Shield
                className={`w-6 h-6 ${
                  activeCard === "permissions"
                    ? "text-green-600"
                    : "text-gray-600"
                }`}
              />
            </div>
            <div className="flex-1">
              <h3 className="font-bold text-lg mb-1">إدارة الصلاحيات</h3>
              <p className="text-sm text-gray-600">
                تحديد صلاحيات الكاشيرين والمستخدمين
              </p>
            </div>
          </div>
        </button>
      </div>

      {/* Active Card Content */}
      {activeCard === "users" && <UserManagementCard />}
      {activeCard === "permissions" && <PermissionsManagementCard />}
    </div>
  );
}
