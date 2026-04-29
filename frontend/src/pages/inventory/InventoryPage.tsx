import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAppSelector } from "../../store/hooks";
import { selectCurrentBranch } from "../../store/slices/branchSlice";
import { selectIsAdmin } from "../../store/slices/authSlice";
import {
  BranchInventoryList,
  LowStockAlerts,
  InventoryTransferForm,
  InventoryTransferList,
  BranchPricingEditor,
  BatchExpiryAlertBanner,
} from "../../components/inventory";
import { useGetLatestCompletedStockTakingQuery } from "../../api/stockTakingApi";
import {
  Package,
  AlertTriangle,
  ArrowRightLeft,
  DollarSign,
  Building2,
  ClipboardList,
  ArrowRight,
} from "lucide-react";

type TabType = "inventory" | "alerts" | "transfers" | "pricing";

export default function InventoryPage() {
  const currentBranch = useAppSelector(selectCurrentBranch);
  const isAdmin = useAppSelector(selectIsAdmin);
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<TabType>("inventory");
  const [showTransferForm, setShowTransferForm] = useState(false);

  const { data: latestStockTakingData } = useGetLatestCompletedStockTakingQuery();
  const latestStockTaking = latestStockTakingData?.data;

  const tabs = [
    {
      id: "inventory" as TabType,
      label: "مخزون الفرع",
      icon: Package,
      adminOnly: false,
    },
    {
      id: "alerts" as TabType,
      label: "تنبيهات المخزون",
      icon: AlertTriangle,
      adminOnly: false,
    },
    {
      id: "transfers" as TabType,
      label: "نقل المخزون",
      icon: ArrowRightLeft,
      adminOnly: true,
    },
    {
      id: "pricing" as TabType,
      label: "أسعار الفروع",
      icon: DollarSign,
      adminOnly: true,
    },
  ];

  // Filter tabs based on admin status
  const visibleTabs = tabs.filter((tab) => !tab.adminOnly || isAdmin);

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {/* Page Header */}
        <div className="mb-8">
          <div className="flex items-center gap-3 mb-2">
            <Building2 className="w-8 h-8 text-blue-600" />
            <h1 className="text-3xl font-bold text-gray-900">إدارة المخزون</h1>
          </div>
          <p className="text-gray-600">
            إدارة مخزون الفروع، التنبيهات، والنقل بين الفروع
          </p>
          {currentBranch && (
            <div className="mt-3 inline-flex items-center gap-2 px-4 py-2 bg-blue-50 border border-blue-200 rounded-lg">
              <Building2 className="w-4 h-4 text-blue-600" />
              <span className="text-sm font-medium text-blue-900">
                الفرع الحالي: {currentBranch.name}
              </span>
            </div>
          )}
        </div>

        {/* Batch Expiry Alert */}
        <BatchExpiryAlertBanner />

        {/* Latest Completed Stock Taking */}
        {latestStockTaking && (
          <div className="bg-purple-50 border border-purple-200 rounded-lg p-4 flex items-center justify-between">
            <div className="flex items-center gap-3">
              <ClipboardList className="w-5 h-5 text-purple-600" />
              <div>
                <p className="text-sm font-semibold text-purple-900">
                  آخر جرد مكتمل: {latestStockTaking.stockTakingNumber}
                </p>
                <p className="text-xs text-purple-700 mt-0.5">
                  {latestStockTaking.itemCount} بنود | الفرق الكلي: {latestStockTaking.totalDifference > 0 ? '+' : ''}{latestStockTaking.totalDifference}
                </p>
              </div>
            </div>
            <button
              onClick={() => navigate(`/stock-taking?open=${latestStockTaking.id}`)}
              className="flex items-center gap-1 text-sm text-purple-700 hover:text-purple-900 font-medium transition-colors"
            >
              عرض الجرد
              <ArrowRight className="w-4 h-4" />
            </button>
          </div>
        )}

        {/* Tabs */}
        <div className="bg-white rounded-lg border border-gray-200 mb-6">
          <div className="border-b border-gray-200">
            <nav className="flex -mb-px">
              {visibleTabs.map((tab) => {
                const Icon = tab.icon;
                const isActive = activeTab === tab.id;

                return (
                  <button
                    key={tab.id}
                    onClick={() => {
                      setActiveTab(tab.id);
                      setShowTransferForm(false);
                    }}
                    className={`flex items-center gap-2 px-6 py-4 border-b-2 font-medium text-sm transition-colors ${
                      isActive
                        ? "border-blue-600 text-blue-600"
                        : "border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300"
                    }`}
                  >
                    <Icon className="w-5 h-5" />
                    {tab.label}
                  </button>
                );
              })}
            </nav>
          </div>

          {/* Tab Content */}
          <div className="p-6">
            {activeTab === "inventory" && <BranchInventoryList />}

            {activeTab === "alerts" && <LowStockAlerts />}

            {activeTab === "transfers" && (
              <div className="space-y-6">
                {showTransferForm ? (
                  <InventoryTransferForm
                    onSuccess={() => setShowTransferForm(false)}
                    onCancel={() => setShowTransferForm(false)}
                  />
                ) : (
                  <div className="flex justify-end mb-4">
                    <button
                      onClick={() => setShowTransferForm(true)}
                      className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700"
                    >
                      <ArrowRightLeft className="w-4 h-4" />
                      طلب نقل جديد
                    </button>
                  </div>
                )}
                <InventoryTransferList />
              </div>
            )}

            {activeTab === "pricing" && <BranchPricingEditor />}
          </div>
        </div>

        {/* Help Section */}
        <div className="bg-blue-50 border border-blue-200 rounded-lg p-6">
          <h3 className="text-lg font-semibold text-blue-900 mb-3">
            💡 نصائح استخدام نظام المخزون
          </h3>
          <ul className="space-y-2 text-sm text-blue-800">
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>مخزون الفرع:</strong> عرض جميع المنتجات المتوفرة في الفرع
                الحالي مع الكميات وحالة المخزون
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>تنبيهات المخزون:</strong> المنتجات التي وصلت إلى حد إعادة
                الطلب وتحتاج إلى تعبئة
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>نقل المخزون:</strong> نقل المنتجات بين الفروع (للمديرين فقط)
                - يتطلب موافقة واستلام
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>أسعار الفروع:</strong> تخصيص أسعار مختلفة لكل فرع حسب
                الموقع والطلب
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>مهم:</strong> جميع التغييرات في المخزون تُسجل تلقائياً في
                سجل الحركات للمراجعة
              </span>
            </li>
          </ul>
        </div>
      </div>
    </div>
  );
}
