import { useState } from "react";
import {
  useCreateTenantMutation,
  useGetTenantsQuery,
  useRunSeedPipelineMutation,
  useSetTenantStatusMutation,
} from "../../api/systemApi";
import {
  Building2,
  Mail,
  Lock,
  MapPin,
  CheckCircle2,
  AlertCircle,
  RefreshCw,
  Power,
  ChevronDown,
  Database,
} from "lucide-react";
import { formatDateOnly } from "../../utils/formatters";
import { handleApiError } from "../../utils/errorHandler";
import { ConfirmDialog } from "../../components/common";

const strongPasswordRegex =
  /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\w\s]).{8,100}$/;

export default function TenantCreationPage() {
  const [createTenant, { isLoading }] = useCreateTenantMutation();
  const [runSeedPipeline, { isLoading: isSeeding }] =
    useRunSeedPipelineMutation();
  const [setTenantStatus, { isLoading: isUpdatingStatus }] =
    useSetTenantStatusMutation();
  const {
    data: tenantsResponse,
    isLoading: isLoadingTenants,
    refetch,
  } = useGetTenantsQuery();

  const [formData, setFormData] = useState({
    tenantName: "",
    adminEmail: "",
    adminPassword: "",
    branchName: "",
  });

  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<
    "all" | "active" | "inactive"
  >("all");
  const [showCreateDialog, setShowCreateDialog] = useState(false);
  const [toggleDialogTenant, setToggleDialogTenant] = useState<{
    id: number;
    currentStatus: boolean;
  } | null>(null);
  const [showSeedDialog, setShowSeedDialog] = useState(false);

  const tenants = tenantsResponse?.data ?? [];
  const activeCount = tenants.filter((tenant) => tenant.isActive).length;
  const inactiveCount = tenants.length - activeCount;
  const totalUsers = tenants.reduce(
    (sum, tenant) => sum + tenant.usersCount,
    0,
  );
  const totalActiveUsers = tenants.reduce(
    (sum, tenant) => sum + tenant.activeUsersCount,
    0,
  );
  const totalActiveBranches = tenants.reduce(
    (sum, tenant) => sum + tenant.activeBranchesCount,
    0,
  );
  const filteredTenants = tenants.filter((tenant) => {
    const matchesSearch =
      tenant.name.toLowerCase().includes(search.toLowerCase()) ||
      tenant.slug.toLowerCase().includes(search.toLowerCase());

    const matchesStatus =
      statusFilter === "all" ||
      (statusFilter === "active" && tenant.isActive) ||
      (statusFilter === "inactive" && !tenant.isActive);

    return matchesSearch && matchesStatus;
  });

  const validateForm = () => {
    if (formData.tenantName.trim().length < 2) {
      return "اسم الشركة يجب أن يكون على الأقل حرفين";
    }

    if (formData.branchName.trim().length < 2) {
      return "اسم الفرع يجب أن يكون على الأقل حرفين";
    }

    if (!strongPasswordRegex.test(formData.adminPassword)) {
      return "كلمة المرور يجب أن تحتوي على 8 أحرف على الأقل وتتضمن حرف كبير وحرف صغير ورقم ورمز خاص";
    }

    return "";
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    setSuccess("");

    const validationError = validateForm();
    if (validationError) {
      setError(validationError);
      return;
    }

    setShowCreateDialog(true);
  };

  const handleConfirmCreate = async () => {
    try {
      const payload = {
        tenantName: formData.tenantName.trim(),
        adminEmail: formData.adminEmail.trim(),
        adminPassword: formData.adminPassword,
        branchName: formData.branchName.trim(),
      };

      const result = await createTenant(payload).unwrap();
      setSuccess(result.message || "تم إنشاء الشركة بنجاح");
      await refetch();
      setFormData({
        tenantName: "",
        adminEmail: "",
        adminPassword: "",
        branchName: "",
      });
      setShowCreateDialog(false);
    } catch (err: unknown) {
      console.error("Create tenant error:", err);
      setError(handleApiError(err));
      setShowCreateDialog(false);
    }
  };

  const handleToggleTenantStatus = (
    tenantId: number,
    currentStatus: boolean,
  ) => {
    setToggleDialogTenant({ id: tenantId, currentStatus });
  };

  const handleConfirmToggleTenantStatus = async () => {
    if (!toggleDialogTenant) return;
    try {
      const result = await setTenantStatus({
        tenantId: toggleDialogTenant.id,
        body: { isActive: !toggleDialogTenant.currentStatus },
      }).unwrap();

      setSuccess(result.message || "تم تحديث حالة الشركة");
      setError("");
      await refetch();
      setToggleDialogTenant(null);
    } catch (err: unknown) {
      setError(handleApiError(err));
      setSuccess("");
      setToggleDialogTenant(null);
    }
  };

  const handleRunSeedPipeline = () => {
    setError("");
    setSuccess("");
    setShowSeedDialog(true);
  };

  const handleConfirmSeedPipeline = async () => {
    try {
      const result = await runSeedPipeline().unwrap();
      const warningsCount = result.data?.optionalWarnings?.length ?? 0;
      const message = result.message || "تم تشغيل السيدر بنجاح";

      setSuccess(
        warningsCount > 0
          ? `${message} (تحذيرات اختيارية: ${warningsCount})`
          : message,
      );

      await refetch();
      setShowSeedDialog(false);
    } catch (err: unknown) {
      setError(handleApiError(err));
      setSuccess("");
      setShowSeedDialog(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-purple-50 p-6">
      <div className="max-w-6xl mx-auto">
        {/* Header */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-16 h-16 bg-blue-600 rounded-full mb-4">
            <Building2 className="w-8 h-8 text-white" />
          </div>
          <h1 className="text-3xl font-bold text-gray-900 mb-2">
            إنشاء شركة جديدة
          </h1>
          <p className="text-gray-600">لوحة إدارة الشركات (مالك النظام)</p>
        </div>

        <div className="mb-6 bg-amber-50 border border-amber-200 rounded-xl p-4 sm:p-5">
          <div className="flex flex-col sm:flex-row gap-4 sm:items-center sm:justify-between">
            <div>
              <h2 className="text-lg font-semibold text-amber-900 flex items-center gap-2">
                <Database className="w-5 h-5" />
                تشغيل السيدر يدويًا
              </h2>
              <p className="text-sm text-amber-800 mt-1">
                التطبيق يبدأ الآن بشكل نظيف، ويمكنك تحميل نفس بيانات السيدر
                الحالية وقت الحاجة فقط.
              </p>
            </div>
            <button
              type="button"
              onClick={handleRunSeedPipeline}
              disabled={isSeeding}
              className="inline-flex items-center justify-center gap-2 px-4 py-2.5 rounded-lg bg-amber-600 text-white font-medium hover:bg-amber-700 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {isSeeding ? (
                <>
                  <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24">
                    <circle
                      className="opacity-25"
                      cx="12"
                      cy="12"
                      r="10"
                      stroke="currentColor"
                      strokeWidth="4"
                      fill="none"
                    />
                    <path
                      className="opacity-75"
                      fill="currentColor"
                      d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
                    />
                  </svg>
                  جاري تشغيل السيدر...
                </>
              ) : (
                "تشغيل السيدر الآن"
              )}
            </button>
          </div>
        </div>

        {/* Dashboard Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6 gap-4 mb-6">
          <div className="bg-white rounded-xl border border-gray-200 p-4 shadow-sm">
            <p className="text-sm text-gray-500">إجمالي الشركات</p>
            <p className="text-3xl font-bold text-gray-900 mt-2">
              {tenants.length}
            </p>
          </div>
          <div className="bg-white rounded-xl border border-gray-200 p-4 shadow-sm">
            <p className="text-sm text-gray-500">الشركات المفعلة</p>
            <p className="text-3xl font-bold text-green-600 mt-2">
              {activeCount}
            </p>
          </div>
          <div className="bg-white rounded-xl border border-gray-200 p-4 shadow-sm">
            <p className="text-sm text-gray-500">الشركات المعطلة</p>
            <p className="text-3xl font-bold text-red-600 mt-2">
              {inactiveCount}
            </p>
          </div>
          <div className="bg-white rounded-xl border border-gray-200 p-4 shadow-sm">
            <p className="text-sm text-gray-500">إجمالي المستخدمين</p>
            <p className="text-3xl font-bold text-gray-900 mt-2">
              {totalUsers}
            </p>
          </div>
          <div className="bg-white rounded-xl border border-gray-200 p-4 shadow-sm">
            <p className="text-sm text-gray-500">المستخدمون النشطون</p>
            <p className="text-3xl font-bold text-blue-600 mt-2">
              {totalActiveUsers}
            </p>
          </div>
          <div className="bg-white rounded-xl border border-gray-200 p-4 shadow-sm">
            <p className="text-sm text-gray-500">الفروع النشطة</p>
            <p className="text-3xl font-bold text-purple-600 mt-2">
              {totalActiveBranches}
            </p>
          </div>
        </div>

        {/* Alert Messages */}
        {error && (
          <div className="mb-6 p-4 bg-red-50 border-l-4 border-red-500 rounded-lg flex items-start gap-3">
            <AlertCircle className="w-5 h-5 text-red-500 flex-shrink-0 mt-0.5" />
            <div>
              <h3 className="font-semibold text-red-800 mb-1">خطأ</h3>
              <p className="text-red-700">{error}</p>
            </div>
          </div>
        )}

        {success && (
          <div className="mb-6 p-4 bg-green-50 border-l-4 border-green-500 rounded-lg flex items-start gap-3">
            <CheckCircle2 className="w-5 h-5 text-green-500 flex-shrink-0 mt-0.5" />
            <div>
              <h3 className="font-semibold text-green-800 mb-1">نجح!</h3>
              <p className="text-green-700">{success}</p>
            </div>
          </div>
        )}

        {/* Form Card */}
        <div className="bg-white rounded-xl shadow-lg border border-gray-200 overflow-hidden mb-6">
          <div className="bg-gradient-to-r from-blue-600 to-purple-600 px-6 py-4">
            <h2 className="text-xl font-semibold text-white">بيانات الشركة</h2>
          </div>

          <form onSubmit={handleSubmit} className="p-6 space-y-6">
            {/* Tenant Name */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                <Building2 className="w-4 h-4 inline-block ml-1" />
                اسم الشركة
              </label>
              <input
                type="text"
                value={formData.tenantName}
                onChange={(e) =>
                  setFormData({ ...formData, tenantName: e.target.value })
                }
                required
                minLength={2}
                maxLength={100}
                placeholder="مثال: مطعم الأمل"
                className="w-full px-4 py-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
              />
            </div>

            {/* Branch Name */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                <MapPin className="w-4 h-4 inline-block ml-1" />
                اسم الفرع الرئيسي
              </label>
              <input
                type="text"
                value={formData.branchName}
                onChange={(e) =>
                  setFormData({ ...formData, branchName: e.target.value })
                }
                required
                minLength={2}
                maxLength={100}
                placeholder="مثال: الفرع الرئيسي"
                className="w-full px-4 py-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
              />
            </div>

            {/* Divider */}
            <div className="border-t border-gray-200 pt-6">
              <h3 className="text-lg font-semibold text-gray-900 mb-4">
                بيانات المدير
              </h3>
            </div>

            {/* Admin Email */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                <Mail className="w-4 h-4 inline-block ml-1" />
                البريد الإلكتروني للمدير
              </label>
              <input
                type="email"
                value={formData.adminEmail}
                onChange={(e) =>
                  setFormData({ ...formData, adminEmail: e.target.value })
                }
                required
                placeholder="مثال: admin@example.com"
                className="w-full px-4 py-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
              />
            </div>

            {/* Admin Password */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                <Lock className="w-4 h-4 inline-block ml-1" />
                كلمة المرور
              </label>
              <input
                type="password"
                value={formData.adminPassword}
                onChange={(e) =>
                  setFormData({ ...formData, adminPassword: e.target.value })
                }
                required
                minLength={8}
                maxLength={100}
                placeholder="كلمة مرور قوية"
                pattern="^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\w\s]).{8,100}$"
                className="w-full px-4 py-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
              />
              <p className="mt-2 text-sm text-gray-500">
                8+ أحرف وتشمل: حرف كبير، حرف صغير، رقم، ورمز خاص
              </p>
            </div>

            {/* Submit Button */}
            <button
              type="submit"
              disabled={isLoading}
              className="w-full bg-gradient-to-r from-blue-600 to-purple-600 text-white font-semibold py-3 px-6 rounded-lg hover:from-blue-700 hover:to-purple-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed transition-all transform hover:scale-[1.02] active:scale-[0.98]"
            >
              {isLoading ? (
                <span className="flex items-center justify-center gap-2">
                  <svg className="animate-spin h-5 w-5" viewBox="0 0 24 24">
                    <circle
                      className="opacity-25"
                      cx="12"
                      cy="12"
                      r="10"
                      stroke="currentColor"
                      strokeWidth="4"
                      fill="none"
                    />
                    <path
                      className="opacity-75"
                      fill="currentColor"
                      d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                    />
                  </svg>
                  جاري الإنشاء...
                </span>
              ) : (
                "إنشاء الشركة"
              )}
            </button>
          </form>
        </div>

        {/* Tenants Table */}
        <div className="bg-white rounded-xl shadow-lg border border-gray-200 overflow-hidden">
          <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
            <h2 className="text-xl font-semibold text-gray-900">
              الشركات الحالية
            </h2>
            <button
              onClick={() => refetch()}
              className="inline-flex items-center gap-2 px-3 py-2 text-sm border border-gray-300 rounded-lg hover:bg-gray-50"
              type="button"
            >
              <RefreshCw className="w-4 h-4" />
              تحديث
            </button>
          </div>

          <div className="px-6 py-4 border-b border-gray-100 grid grid-cols-1 md:grid-cols-3 gap-3">
            <input
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="بحث بالاسم أو slug"
              className="md:col-span-2 px-3 py-2 border border-gray-300 rounded-lg text-sm"
            />
            <div className="relative">
              <select
                value={statusFilter}
                onChange={(e) =>
                  setStatusFilter(
                    e.target.value as "all" | "active" | "inactive",
                  )
                }
                className="appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 hover:border-gray-400 transition-all duration-200 shadow-sm"
              >
                <option value="all">كل الحالات</option>
                <option value="active">المفعلة فقط</option>
                <option value="inactive">المعطلة فقط</option>
              </select>
              <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
            </div>
          </div>

          {isLoadingTenants ? (
            <div className="p-6 text-gray-600">جاري تحميل الشركات...</div>
          ) : filteredTenants.length === 0 ? (
            <div className="p-6 text-gray-600">لا توجد شركات حتى الآن.</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-[840px] divide-y divide-gray-200">
                <thead className="bg-gray-50">
                  <tr>
                    <th className="px-4 py-3 text-right text-xs font-semibold text-gray-600">
                      الشركة
                    </th>
                    <th className="px-4 py-3 text-right text-xs font-semibold text-gray-600">
                      Slug
                    </th>
                    <th className="px-4 py-3 text-right text-xs font-semibold text-gray-600">
                      الفروع (النشطة)
                    </th>
                    <th className="px-4 py-3 text-right text-xs font-semibold text-gray-600">
                      المستخدمون
                    </th>
                    <th className="px-4 py-3 text-right text-xs font-semibold text-gray-600">
                      Admins / Cashiers
                    </th>
                    <th className="px-4 py-3 text-right text-xs font-semibold text-gray-600">
                      تفاصيل الشركة
                    </th>
                    <th className="px-4 py-3 text-right text-xs font-semibold text-gray-600">
                      تاريخ الإنشاء
                    </th>
                    <th className="px-4 py-3 text-right text-xs font-semibold text-gray-600">
                      الحالة
                    </th>
                    <th className="px-4 py-3 text-right text-xs font-semibold text-gray-600">
                      الإجراء
                    </th>
                  </tr>
                </thead>
                <tbody className="bg-white divide-y divide-gray-100">
                  {filteredTenants.map((tenant) => (
                    <tr key={tenant.id}>
                      <td className="px-4 py-3 text-sm text-gray-900 font-medium">
                        {tenant.name}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-600">
                        {tenant.slug}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-600">
                        {tenant.branchesCount} ({tenant.activeBranchesCount})
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-600">
                        {tenant.activeUsersCount} / {tenant.usersCount}
                        <div className="text-xs text-gray-500 mt-1">
                          غير نشط: {tenant.inactiveUsersCount}
                        </div>
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-600">
                        {tenant.adminsCount} / {tenant.cashiersCount}
                      </td>
                      <td className="px-4 py-3 text-xs text-gray-600 leading-5">
                        <div>العملة: {tenant.currency}</div>
                        <div>المنطقة الزمنية: {tenant.timezone}</div>
                        <div>
                          الضريبة:{" "}
                          {tenant.isTaxEnabled ? `${tenant.taxRate}%` : "معطلة"}
                        </div>
                        <div>
                          مخزون سالب:{" "}
                          {tenant.allowNegativeStock ? "مسموح" : "غير مسموح"}
                        </div>
                        <div>
                          آخر تحديث:{" "}
                          {tenant.updatedAt
                            ? formatDateOnly(tenant.updatedAt)
                            : "-"}
                        </div>
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-600">
                        {formatDateOnly(tenant.createdAt)}
                      </td>
                      <td className="px-4 py-3">
                        <span
                          className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium ${
                            tenant.isActive
                              ? "bg-green-100 text-green-700"
                              : "bg-red-100 text-red-700"
                          }`}
                        >
                          {tenant.isActive ? "مفعلة" : "معطلة"}
                        </span>
                      </td>
                      <td className="px-4 py-3">
                        <button
                          type="button"
                          onClick={() =>
                            handleToggleTenantStatus(tenant.id, tenant.isActive)
                          }
                          disabled={isUpdatingStatus}
                          className={`inline-flex items-center gap-2 px-3 py-2 rounded-lg text-xs font-medium ${
                            tenant.isActive
                              ? "bg-red-50 text-red-700 hover:bg-red-100"
                              : "bg-green-50 text-green-700 hover:bg-green-100"
                          } disabled:opacity-50`}
                        >
                          <Power className="w-3 h-3" />
                          {tenant.isActive ? "تعطيل" : "تفعيل"}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Info Box */}
        <div className="mt-6 p-4 bg-blue-50 border border-blue-200 rounded-lg">
          <h4 className="font-semibold text-blue-900 mb-2">ملاحظة</h4>
          <ul className="text-sm text-blue-800 space-y-1">
            <li>• سيتم إنشاء شركة جديدة منفصلة تماماً</li>
            <li>• سيتم إنشاء فرع رئيسي للشركة</li>
            <li>• سيتم إنشاء حساب مدير بالبيانات المدخلة</li>
            <li>• يمكن للمدير تسجيل الدخول فوراً بعد الإنشاء</li>
          </ul>
        </div>

        <ConfirmDialog
          open={showCreateDialog}
          onOpenChange={(open) => !open && setShowCreateDialog(false)}
          onConfirm={handleConfirmCreate}
          title="إنشاء شركة جديدة"
          description="هل أنت متأكد من إنشاء شركة جديدة؟ لا يمكن التراجع عن هذه العملية بسهولة."
          isLoading={isLoading}
        />

        <ConfirmDialog
          open={toggleDialogTenant !== null}
          onOpenChange={(open) => !open && setToggleDialogTenant(null)}
          onConfirm={handleConfirmToggleTenantStatus}
          title={toggleDialogTenant?.currentStatus ? "تعطيل الشركة" : "تفعيل الشركة"}
          description={
            toggleDialogTenant?.currentStatus
              ? "هل أنت متأكد من تعطيل هذه الشركة؟"
              : "هل أنت متأكد من تفعيل هذه الشركة؟"
          }
          isLoading={isUpdatingStatus}
        />

        <ConfirmDialog
          open={showSeedDialog}
          onOpenChange={(open) => !open && setShowSeedDialog(false)}
          onConfirm={handleConfirmSeedPipeline}
          title="تشغيل السيدر"
          description="سيتم تشغيل نفس السيدر داتا الحالية كما هي. قد تستغرق العملية عدة دقائق حسب حجم البيانات. هل تريد المتابعة؟"
          isLoading={isSeeding}
        />
      </div>
    </div>
  );
}
