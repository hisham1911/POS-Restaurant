import UserManagementCard from "./components/UserManagementCard";

export default function UserManagementPage() {
  return (
    <div className="container mx-auto p-4 sm:p-6" dir="rtl">
      <div className="mb-6">
        <h1 className="text-3xl font-bold mb-2">إدارة المستخدمين</h1>
        <p className="text-gray-600">إدارة حسابات المستخدمين وصلاحياتهم</p>
      </div>

      <UserManagementCard />
    </div>
  );
}

