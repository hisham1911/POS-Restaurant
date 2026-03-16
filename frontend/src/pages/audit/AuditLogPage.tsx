import { useState, useMemo } from "react";
import {
  ClipboardList,
  Filter,
  ChevronLeft,
  ChevronRight,
  ShoppingCart,
  Package,
  FolderOpen,
  User,
  Building2,
  Clock,
  CreditCard,
  Edit,
  Plus,
  Trash2,
  RefreshCw,
  CheckCircle,
  XCircle,
  FileEdit,
  ChevronDown,
} from "lucide-react";
import { useGetAuditLogsQuery } from "@/api/auditApi";
import { formatDateTime } from "@/utils/formatters";
import type { AuditLog, AuditLogFilters } from "@/types/audit.types";

// Entity type icons and labels
const entityConfig: Record<string, { icon: typeof ShoppingCart; label: string; color: string }> = {
  Order: { icon: ShoppingCart, label: "طلب", color: "bg-blue-100 text-blue-700" },
  Product: { icon: Package, label: "منتج", color: "bg-green-100 text-green-700" },
  Category: { icon: FolderOpen, label: "تصنيف", color: "bg-purple-100 text-purple-700" },
  User: { icon: User, label: "مستخدم", color: "bg-orange-100 text-orange-700" },
  Branch: { icon: Building2, label: "فرع", color: "bg-cyan-100 text-cyan-700" },
  Shift: { icon: Clock, label: "وردية", color: "bg-amber-100 text-amber-700" },
  Payment: { icon: CreditCard, label: "دفع", color: "bg-pink-100 text-pink-700" },
};

// Action icons and colors
const actionConfig: Record<string, { icon: typeof Plus; color: string; bgColor: string }> = {
  Create: { icon: Plus, color: "text-green-700", bgColor: "bg-green-100" },
  Update: { icon: Edit, color: "text-blue-700", bgColor: "bg-blue-100" },
  Delete: { icon: Trash2, color: "text-red-700", bgColor: "bg-red-100" },
};

/**
 * Parse JSON values safely
 */
const parseJson = (json?: string): Record<string, unknown> | null => {
  if (!json) return null;
  try {
    return JSON.parse(json);
  } catch {
    return null;
  }
};

/**
 * Get human-readable action description in Arabic
 * Uses "Order" terminology (طلب) not "Sale" (بيع)
 */
const getActionDescription = (log: AuditLog): string => {
  const { entityType, action, newValues, oldValues } = log;
  const newData = parseJson(newValues);
  const oldData = parseJson(oldValues);

  // Order actions - استخدام مصطلح "طلب" وليس "بيع"
  if (entityType === "Order") {
    if (action === "Create") return "إنشاء طلب جديد";
    if (action === "Update") {
      const newStatus = newData?.Status as string | number;
      const oldStatus = oldData?.Status as string | number;
      // Status 2 = Completed - تم إتمام الدفع وإغلاق الطلب
      if (newStatus === "Completed" || newStatus === 2 || newStatus === "2") {
        return "تم إتمام الدفع وإغلاق الطلب";
      }
      // Status 3 = Cancelled
      if (newStatus === "Cancelled" || newStatus === 3 || newStatus === "3") {
        return "إلغاء الطلب";
      }
      if (newStatus !== oldStatus) return "تغيير حالة الطلب";
      return "تعديل بيانات الطلب";
    }
    if (action === "Delete") return "حذف طلب";
  }

  // Payment actions - المدفوعات
  if (entityType === "Payment") {
    if (action === "Create") {
      // Check payment method from newValues
      const method = newData?.Method as string | number;
      if (method === "Cash" || method === 0 || method === "0") {
        return "تسجيل دفعة نقدية";
      }
      if (method === "Card" || method === 1 || method === "1") {
        return "تسجيل دفعة بالبطاقة";
      }
      if (method === "Fawry" || method === 2 || method === "2") {
        return "تسجيل دفعة فوري";
      }
      return "تسجيل دفعة";
    }
    if (action === "Update") return "تعديل دفعة";
    if (action === "Delete") return "حذف دفعة";
  }

  // Shift actions
  if (entityType === "Shift") {
    if (action === "Create") return "فتح وردية";
    if (action === "Update") {
      const isClosed = newData?.IsClosed;
      if (isClosed === true || isClosed === "True" || isClosed === "true") return "إغلاق الوردية";
      return "تعديل الوردية";
    }
    if (action === "Delete") return "حذف وردية";
  }

  // Product actions
  if (entityType === "Product") {
    if (action === "Create") return "إضافة منتج";
    if (action === "Update") {
      if (newData?.Price !== oldData?.Price) return "تعديل سعر منتج";
      if (newData?.Stock !== oldData?.Stock) return "تعديل مخزون منتج";
      return "تعديل منتج";
    }
    if (action === "Delete") return "حذف منتج";
  }

  // Category actions
  if (entityType === "Category") {
    if (action === "Create") return "إضافة تصنيف";
    if (action === "Update") return "تعديل تصنيف";
    if (action === "Delete") return "حذف تصنيف";
  }

  // User actions
  if (entityType === "User") {
    if (action === "Create") return "إضافة مستخدم";
    if (action === "Update") return "تعديل مستخدم";
    if (action === "Delete") return "حذف مستخدم";
  }

  // Branch actions
  if (entityType === "Branch") {
    if (action === "Create") return "إضافة فرع";
    if (action === "Update") return "تعديل فرع";
    if (action === "Delete") return "حذف فرع";
  }

  // Fallback
  const actionMap: Record<string, string> = {
    Create: "إنشاء",
    Update: "تعديل",
    Delete: "حذف",
  };
  return `${actionMap[action] || action} ${entityConfig[entityType]?.label || entityType}`;
};

/**
 * Get status badge for order status changes
 */
const getStatusBadge = (log: AuditLog): { text: string; icon: typeof CheckCircle; color: string } | null => {
  if (log.entityType !== "Order" || log.action !== "Update") return null;
  
  const newData = parseJson(log.newValues);
  const newStatus = newData?.Status as string | number;
  
  if (newStatus === "Completed" || newStatus === 2 || newStatus === "2") {
    return { text: "تم الدفع", icon: CheckCircle, color: "bg-green-100 text-green-700" };
  }
  if (newStatus === "Cancelled" || newStatus === 3 || newStatus === "3") {
    return { text: "ملغي", icon: XCircle, color: "bg-red-100 text-red-700" };
  }
  if (newStatus === "Draft" || newStatus === 0 || newStatus === "0") {
    return { text: "مسودة", icon: FileEdit, color: "bg-gray-100 text-gray-700" };
  }
  return null;
};

/**
 * Get details from the log (order number, product name, etc.)
 */
const getDetails = (log: AuditLog): string | null => {
  const newData = parseJson(log.newValues);
  const oldData = parseJson(log.oldValues);
  const data = newData || oldData;

  if (!data) return null;

  if (log.entityType === "Order") {
    return (data.OrderNumber as string) || null;
  }
  if (log.entityType === "Product" || log.entityType === "Category") {
    return (data.Name as string) || (data.NameEn as string) || null;
  }
  if (log.entityType === "User") {
    return (data.Name as string) || (data.Email as string) || null;
  }
  if (log.entityType === "Branch") {
    return (data.Name as string) || null;
  }
  if (log.entityType === "Shift" && data.OpeningBalance) {
    return `رصيد: ${data.OpeningBalance} ج.م`;
  }

  return null;
};

const AuditLogPage = () => {
  const [filters, setFilters] = useState<AuditLogFilters>({
    page: 1,
    pageSize: 20,
  });

  const { data, isLoading, isFetching, refetch } = useGetAuditLogsQuery(filters);
  const logs = data?.data?.items || [];
  const pagination = data?.data;

  const handleFilterChange = (key: keyof AuditLogFilters, value: string) => {
    setFilters((prev) => ({
      ...prev,
      [key]: value || undefined,
      page: 1,
    }));
  };

  const handlePageChange = (newPage: number) => {
    setFilters((prev) => ({ ...prev, page: newPage }));
  };

  const clearFilters = () => {
    setFilters({ page: 1, pageSize: 20 });
  };

  const hasActiveFilters = useMemo(() => {
    return !!(filters.entityType || filters.action || filters.fromDate || filters.toDate);
  }, [filters]);

  return (
    <div className="h-full flex flex-col p-4 lg:p-6">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 bg-primary-100 rounded-lg flex items-center justify-center">
            <ClipboardList className="w-5 h-5 text-primary-600" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-gray-900">سجل العمليات</h1>
            <p className="text-sm text-gray-500">تتبع جميع العمليات في النظام</p>
          </div>
        </div>
        <button
          onClick={() => refetch()}
          className="flex items-center gap-2 px-4 py-2 text-sm bg-white border rounded-lg hover:bg-gray-50"
        >
          <RefreshCw className={`w-4 h-4 ${isFetching ? "animate-spin" : ""}`} />
          تحديث
        </button>
      </div>

      {/* Filters */}
      <div className="bg-white rounded-xl border p-4 mb-4">
        <div className="flex items-center justify-between mb-3">
          <div className="flex items-center gap-2">
            <Filter className="w-4 h-4 text-gray-500" />
            <span className="text-sm font-medium text-gray-700">الفلاتر</span>
          </div>
          {hasActiveFilters && (
            <button
              onClick={clearFilters}
              className="text-xs text-red-600 hover:text-red-700"
            >
              مسح الفلاتر
            </button>
          )}
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          {/* Entity Type Filter */}
          <div>
            <label className="block text-xs text-gray-500 mb-1">نوع العملية</label>
            <div className="relative">
              <select
                value={filters.entityType || ""}
                onChange={(e) => handleFilterChange("entityType", e.target.value)}
                className="w-full appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 hover:border-gray-400 transition-all duration-200 shadow-sm"
              >
                <option value="">الكل</option>
                {Object.entries(entityConfig).map(([key, { label }]) => (
                  <option key={key} value={key}>{label}</option>
                ))}
              </select>
              <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
            </div>
          </div>

          {/* Action Filter */}
          <div>
            <label className="block text-xs text-gray-500 mb-1">نوع الإجراء</label>
            <div className="relative">
              <select
                value={filters.action || ""}
                onChange={(e) => handleFilterChange("action", e.target.value)}
                className="w-full appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 hover:border-gray-400 transition-all duration-200 shadow-sm"
              >
                <option value="">الكل</option>
                <option value="Create">إنشاء</option>
                <option value="Update">تعديل</option>
                <option value="Delete">حذف</option>
              </select>
              <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
            </div>
          </div>

          {/* From Date */}
          <div>
            <label className="block text-xs text-gray-500 mb-1">من تاريخ</label>
            <input
              type="date"
              value={filters.fromDate || ""}
              onChange={(e) => handleFilterChange("fromDate", e.target.value)}
              className="w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
            />
          </div>

          {/* To Date */}
          <div>
            <label className="block text-xs text-gray-500 mb-1">إلى تاريخ</label>
            <input
              type="date"
              value={filters.toDate || ""}
              onChange={(e) => handleFilterChange("toDate", e.target.value)}
              className="w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
            />
          </div>
        </div>
      </div>

      {/* Table */}
      <div className="flex-1 bg-white rounded-xl border overflow-hidden flex flex-col">
        <div className="overflow-x-auto flex-1">
          <table className="w-full">
            <thead className="bg-gray-50 border-b">
              <tr>
                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase w-1/3">العملية</th>
                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">التفاصيل</th>
                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">المستخدم</th>
                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">التاريخ</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {isLoading || isFetching ? (
                Array.from({ length: 5 }).map((_, i) => (
                  <tr key={i} className="animate-pulse">
                    <td className="px-4 py-3"><div className="h-4 bg-gray-200 rounded w-40" /></td>
                    <td className="px-4 py-3"><div className="h-4 bg-gray-200 rounded w-32" /></td>
                    <td className="px-4 py-3"><div className="h-4 bg-gray-200 rounded w-24" /></td>
                    <td className="px-4 py-3"><div className="h-4 bg-gray-200 rounded w-28" /></td>
                  </tr>
                ))
              ) : logs.length === 0 ? (
                <tr>
                  <td colSpan={4} className="px-4 py-12 text-center text-gray-500">
                    لا توجد سجلات
                  </td>
                </tr>
              ) : (
                logs.map((log) => {
                  const entity = entityConfig[log.entityType];
                  const EntityIcon = entity?.icon || ClipboardList;
                  const actionCfg = actionConfig[log.action] || actionConfig.Update;
                  const ActionIcon = actionCfg.icon;
                  const details = getDetails(log);
                  const statusBadge = getStatusBadge(log);

                  return (
                    <tr key={log.id} className="hover:bg-gray-50">
                      {/* Action (Primary Column) */}
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-3">
                          <span className={`inline-flex items-center justify-center w-8 h-8 rounded-lg ${actionCfg.bgColor}`}>
                            <ActionIcon className={`w-4 h-4 ${actionCfg.color}`} />
                          </span>
                          <div className="flex flex-col">
                            <span className="text-sm font-medium text-gray-900">
                              {getActionDescription(log)}
                            </span>
                            {statusBadge && (
                              <span className={`inline-flex items-center gap-1 mt-1 px-2 py-0.5 rounded text-xs font-medium ${statusBadge.color} w-fit`}>
                                <statusBadge.icon className="w-3 h-3" />
                                {statusBadge.text}
                              </span>
                            )}
                          </div>
                        </div>
                      </td>

                      {/* Details */}
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-2">
                          <span className={`inline-flex items-center justify-center w-6 h-6 rounded ${entity?.color || "bg-gray-100"}`}>
                            <EntityIcon className="w-3.5 h-3.5" />
                          </span>
                          <div className="flex flex-col">
                            <span className="text-sm text-gray-700">
                              {entity?.label || log.entityType}
                              {log.entityId && (
                                <span className="text-gray-400 mr-1">#{log.entityId}</span>
                              )}
                            </span>
                            {details && (
                              <span className="text-xs text-gray-500">{details}</span>
                            )}
                          </div>
                        </div>
                      </td>

                      {/* User */}
                      <td className="px-4 py-3">
                        <span className="text-sm text-gray-700">
                          {log.userName || "-"}
                        </span>
                      </td>

                      {/* Date (Cairo timezone) */}
                      <td className="px-4 py-3 text-sm text-gray-500">
                        {formatDateTime(log.createdAt)}
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {pagination && pagination.totalPages > 1 && (
          <div className="border-t px-4 py-3 flex items-center justify-between bg-gray-50">
            <div className="text-sm text-gray-500">
              عرض {((pagination.page - 1) * pagination.pageSize) + 1} - {Math.min(pagination.page * pagination.pageSize, pagination.totalCount)} من {pagination.totalCount}
            </div>
            <div className="flex items-center gap-2">
              <button
                onClick={() => handlePageChange(pagination.page - 1)}
                disabled={!pagination.hasPreviousPage}
                className="p-2 rounded-lg border hover:bg-gray-100 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <ChevronRight className="w-4 h-4" />
              </button>
              <span className="text-sm text-gray-600">
                صفحة {pagination.page} من {pagination.totalPages}
              </span>
              <button
                onClick={() => handlePageChange(pagination.page + 1)}
                disabled={!pagination.hasNextPage}
                className="p-2 rounded-lg border hover:bg-gray-100 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export default AuditLogPage;
