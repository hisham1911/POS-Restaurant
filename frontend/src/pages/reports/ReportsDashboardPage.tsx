import { useNavigate } from "react-router-dom";
import {
  BarChart3,
  ShoppingBag,
  Package,
  TrendingUp,
  Receipt,
  ArrowRightLeft,
  Users,
  AlertTriangle,
  Activity,
  DollarSign,
  Boxes,
  UserCheck,
  Clock,
  BadgeDollarSign,
  ArrowUpDown,
  Star,
  TrendingDown,
  Calculator,
  Truck,
  CreditCard,
  Award,
} from "lucide-react";
import { Card } from "@/components/common/Card";

interface ReportCard {
  id: string;
  title: string;
  description: string;
  icon: React.ElementType;
  path: string;
  color: string;
  bgColor: string;
}

const reportCategories = {
  sales: {
    title: "تقارير المبيعات والمالية",
    reports: [
      {
        id: "daily",
        title: "التقرير اليومي",
        description: "ملخص المبيعات والطلبات والورديات اليومية",
        icon: BarChart3,
        path: "/reports/daily",
        color: "text-primary-600",
        bgColor: "bg-primary-50",
      },
      {
        id: "sales",
        title: "تقرير المبيعات",
        description: "تحليل شامل للمبيعات حسب الفترة الزمنية",
        icon: ShoppingBag,
        path: "/reports/sales",
        color: "text-blue-600",
        bgColor: "bg-blue-50",
      },
      {
        id: "profit-loss",
        title: "الأرباح والخسائر",
        description: "تقرير مالي شامل للإيرادات والمصروفات والأرباح",
        icon: TrendingUp,
        path: "/reports/profit-loss",
        color: "text-green-600",
        bgColor: "bg-green-50",
      },
      {
        id: "expenses",
        title: "تقرير المصروفات",
        description: "تحليل تفصيلي للمصروفات حسب الفئة وطريقة الدفع",
        icon: Receipt,
        path: "/reports/expenses",
        color: "text-red-600",
        bgColor: "bg-red-50",
      },
    ] as ReportCard[],
  },
  inventory: {
    title: "تقارير المخزون",
    reports: [
      {
        id: "inventory",
        title: "تقرير المخزون",
        description: "حالة المخزون الحالية والمنتجات المنخفضة",
        icon: Package,
        path: "/reports/inventory",
        color: "text-purple-600",
        bgColor: "bg-purple-50",
      },
      {
        id: "transfer-history",
        title: "تاريخ التحويلات",
        description: "سجل تحويلات المخزون بين الفروع",
        icon: ArrowRightLeft,
        path: "/reports/transfer-history",
        color: "text-indigo-600",
        bgColor: "bg-indigo-50",
      },
    ] as ReportCard[],
  },
  customers: {
    title: "تقارير العملاء",
    reports: [
      {
        id: "top-customers",
        title: "أفضل العملاء",
        description: "العملاء الأكثر شراءً وإنفاقاً",
        icon: Users,
        path: "/reports/customers/top",
        color: "text-cyan-600",
        bgColor: "bg-cyan-50",
      },
      {
        id: "customer-debts",
        title: "ديون العملاء",
        description: "المستحقات والديون المتأخرة للعملاء",
        icon: AlertTriangle,
        path: "/reports/customers/debts",
        color: "text-orange-600",
        bgColor: "bg-orange-50",
      },
      {
        id: "customer-activity",
        title: "نشاط العملاء",
        description: "تحليل سلوك العملاء ومعدل الاحتفاظ",
        icon: Activity,
        path: "/reports/customers/activity",
        color: "text-teal-600",
        bgColor: "bg-teal-50",
      },
    ] as ReportCard[],
  },
  employees: {
    title: "تقارير الموظفين",
    reports: [
      {
        id: "cashier-performance",
        title: "أداء الكاشير",
        description: "تحليل أداء الكاشير من حيث المبيعات والورديات",
        icon: UserCheck,
        path: "/reports/employees/cashier-performance",
        color: "text-emerald-600",
        bgColor: "bg-emerald-50",
      },
      {
        id: "shifts",
        title: "تفاصيل الورديات",
        description: "تقرير تفصيلي بجميع الورديات وأوقاتها ومبيعاتها",
        icon: Clock,
        path: "/reports/employees/shifts",
        color: "text-sky-600",
        bgColor: "bg-sky-50",
      },
      {
        id: "sales-by-employee",
        title: "المبيعات حسب الموظف",
        description: "مقارنة أداء الموظفين في المبيعات",
        icon: BadgeDollarSign,
        path: "/reports/employees/sales",
        color: "text-violet-600",
        bgColor: "bg-violet-50",
      },
    ] as ReportCard[],
  },
  products: {
    title: "تقارير المنتجات",
    reports: [
      {
        id: "product-movement",
        title: "حركة المنتجات",
        description: "تتبع حركة المنتجات من مبيعات ومشتريات وتحويلات",
        icon: ArrowUpDown,
        path: "/reports/products/movement",
        color: "text-amber-600",
        bgColor: "bg-amber-50",
      },
      {
        id: "profitability",
        title: "المنتجات الأكثر ربحية",
        description: "ترتيب المنتجات حسب الربحية وهامش الربح",
        icon: Star,
        path: "/reports/products/profitability",
        color: "text-yellow-600",
        bgColor: "bg-yellow-50",
      },
      {
        id: "slow-moving",
        title: "المنتجات الراكدة",
        description: "المنتجات بطيئة الحركة والمخزون المتراكم",
        icon: TrendingDown,
        path: "/reports/products/slow",
        color: "text-rose-600",
        bgColor: "bg-rose-50",
      },
      {
        id: "cogs",
        title: "تكلفة البضاعة المباعة",
        description: "تحليل تكاليف البضاعة المباعة وهوامش الربح",
        icon: Calculator,
        path: "/reports/products/cogs",
        color: "text-slate-600",
        bgColor: "bg-slate-50",
      },
    ] as ReportCard[],
  },
  suppliers: {
    title: "تقارير الموردين",
    reports: [
      {
        id: "supplier-purchases",
        title: "مشتريات الموردين",
        description: "تفاصيل المشتريات والفواتير لكل مورد",
        icon: Truck,
        path: "/reports/suppliers/purchases",
        color: "text-pink-600",
        bgColor: "bg-pink-50",
      },
      {
        id: "supplier-debts",
        title: "ديون الموردين",
        description: "المستحقات والديون المتأخرة للموردين",
        icon: CreditCard,
        path: "/reports/suppliers/debts",
        color: "text-red-600",
        bgColor: "bg-red-50",
      },
      {
        id: "supplier-performance",
        title: "أداء الموردين",
        description: "تقييم أداء الموردين من حيث الالتزام والجودة",
        icon: Award,
        path: "/reports/suppliers/performance",
        color: "text-fuchsia-600",
        bgColor: "bg-fuchsia-50",
      },
    ] as ReportCard[],
  },
};

export const ReportsDashboardPage = () => {
  const navigate = useNavigate();

  const handleReportClick = (path: string) => {
    navigate(path);
  };

  return (
    <div className="h-full overflow-auto p-6 space-y-8">
      {/* Header */}
      <div className="text-center mb-8">
        <div className="flex items-center justify-center gap-3 mb-4">
          <div className="w-16 h-16 bg-primary-100 rounded-2xl flex items-center justify-center">
            <BarChart3 className="w-8 h-8 text-primary-600" />
          </div>
        </div>
        <h1 className="text-3xl font-bold text-gray-800 mb-2">مركز التقارير</h1>
        <p className="text-gray-500 text-lg">
          اختر التقرير الذي تريد عرضه من الخيارات أدناه
        </p>
      </div>

      {/* Sales & Financial Reports */}
      <section>
        <div className="flex items-center gap-3 mb-4">
          <DollarSign className="w-6 h-6 text-gray-600" />
          <h2 className="text-xl font-bold text-gray-800">
            {reportCategories.sales.title}
          </h2>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          {reportCategories.sales.reports.map((report) => (
            <Card
              key={report.id}
              className="cursor-pointer hover:shadow-lg transition-all duration-200 hover:scale-105 group"
              onClick={() => handleReportClick(report.path)}
            >
              <div className="flex flex-col items-center text-center p-4">
                <div
                  className={`w-16 h-16 ${report.bgColor} rounded-2xl flex items-center justify-center mb-4 group-hover:scale-110 transition-transform`}
                >
                  <report.icon className={`w-8 h-8 ${report.color}`} />
                </div>
                <h3 className="text-lg font-bold text-gray-800 mb-2">
                  {report.title}
                </h3>
                <p className="text-sm text-gray-500 mb-4 min-h-[40px]">
                  {report.description}
                </p>
                <button className="w-full px-4 py-2 bg-gray-100 hover:bg-primary-600 hover:text-white text-gray-700 rounded-lg transition-colors font-medium">
                  فتح التقرير
                </button>
              </div>
            </Card>
          ))}
        </div>
      </section>

      {/* Inventory Reports */}
      <section>
        <div className="flex items-center gap-3 mb-4">
          <Boxes className="w-6 h-6 text-gray-600" />
          <h2 className="text-xl font-bold text-gray-800">
            {reportCategories.inventory.title}
          </h2>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          {reportCategories.inventory.reports.map((report) => (
            <Card
              key={report.id}
              className="cursor-pointer hover:shadow-lg transition-all duration-200 hover:scale-105 group"
              onClick={() => handleReportClick(report.path)}
            >
              <div className="flex flex-col items-center text-center p-4">
                <div
                  className={`w-16 h-16 ${report.bgColor} rounded-2xl flex items-center justify-center mb-4 group-hover:scale-110 transition-transform`}
                >
                  <report.icon className={`w-8 h-8 ${report.color}`} />
                </div>
                <h3 className="text-lg font-bold text-gray-800 mb-2">
                  {report.title}
                </h3>
                <p className="text-sm text-gray-500 mb-4 min-h-[40px]">
                  {report.description}
                </p>
                <button className="w-full px-4 py-2 bg-gray-100 hover:bg-primary-600 hover:text-white text-gray-700 rounded-lg transition-colors font-medium">
                  فتح التقرير
                </button>
              </div>
            </Card>
          ))}
        </div>
      </section>

      {/* Customer Reports */}
      <section>
        <div className="flex items-center gap-3 mb-4">
          <Users className="w-6 h-6 text-gray-600" />
          <h2 className="text-xl font-bold text-gray-800">
            {reportCategories.customers.title}
          </h2>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {reportCategories.customers.reports.map((report) => (
            <Card
              key={report.id}
              className="cursor-pointer hover:shadow-lg transition-all duration-200 hover:scale-105 group"
              onClick={() => handleReportClick(report.path)}
            >
              <div className="flex flex-col items-center text-center p-4">
                <div
                  className={`w-16 h-16 ${report.bgColor} rounded-2xl flex items-center justify-center mb-4 group-hover:scale-110 transition-transform`}
                >
                  <report.icon className={`w-8 h-8 ${report.color}`} />
                </div>
                <h3 className="text-lg font-bold text-gray-800 mb-2">
                  {report.title}
                </h3>
                <p className="text-sm text-gray-500 mb-4 min-h-[40px]">
                  {report.description}
                </p>
                <button className="w-full px-4 py-2 bg-gray-100 hover:bg-primary-600 hover:text-white text-gray-700 rounded-lg transition-colors font-medium">
                  فتح التقرير
                </button>
              </div>
            </Card>
          ))}
        </div>
      </section>

      {/* Employee Reports */}
      <section>
        <div className="flex items-center gap-3 mb-4">
          <UserCheck className="w-6 h-6 text-gray-600" />
          <h2 className="text-xl font-bold text-gray-800">
            {reportCategories.employees.title}
          </h2>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {reportCategories.employees.reports.map((report) => (
            <Card
              key={report.id}
              className="cursor-pointer hover:shadow-lg transition-all duration-200 hover:scale-105 group"
              onClick={() => handleReportClick(report.path)}
            >
              <div className="flex flex-col items-center text-center p-4">
                <div
                  className={`w-16 h-16 ${report.bgColor} rounded-2xl flex items-center justify-center mb-4 group-hover:scale-110 transition-transform`}
                >
                  <report.icon className={`w-8 h-8 ${report.color}`} />
                </div>
                <h3 className="text-lg font-bold text-gray-800 mb-2">
                  {report.title}
                </h3>
                <p className="text-sm text-gray-500 mb-4 min-h-[40px]">
                  {report.description}
                </p>
                <button className="w-full px-4 py-2 bg-gray-100 hover:bg-primary-600 hover:text-white text-gray-700 rounded-lg transition-colors font-medium">
                  فتح التقرير
                </button>
              </div>
            </Card>
          ))}
        </div>
      </section>

      {/* Product Reports */}
      <section>
        <div className="flex items-center gap-3 mb-4">
          <Package className="w-6 h-6 text-gray-600" />
          <h2 className="text-xl font-bold text-gray-800">
            {reportCategories.products.title}
          </h2>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          {reportCategories.products.reports.map((report) => (
            <Card
              key={report.id}
              className="cursor-pointer hover:shadow-lg transition-all duration-200 hover:scale-105 group"
              onClick={() => handleReportClick(report.path)}
            >
              <div className="flex flex-col items-center text-center p-4">
                <div
                  className={`w-16 h-16 ${report.bgColor} rounded-2xl flex items-center justify-center mb-4 group-hover:scale-110 transition-transform`}
                >
                  <report.icon className={`w-8 h-8 ${report.color}`} />
                </div>
                <h3 className="text-lg font-bold text-gray-800 mb-2">
                  {report.title}
                </h3>
                <p className="text-sm text-gray-500 mb-4 min-h-[40px]">
                  {report.description}
                </p>
                <button className="w-full px-4 py-2 bg-gray-100 hover:bg-primary-600 hover:text-white text-gray-700 rounded-lg transition-colors font-medium">
                  فتح التقرير
                </button>
              </div>
            </Card>
          ))}
        </div>
      </section>

      {/* Supplier Reports */}
      <section>
        <div className="flex items-center gap-3 mb-4">
          <Truck className="w-6 h-6 text-gray-600" />
          <h2 className="text-xl font-bold text-gray-800">
            {reportCategories.suppliers.title}
          </h2>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {reportCategories.suppliers.reports.map((report) => (
            <Card
              key={report.id}
              className="cursor-pointer hover:shadow-lg transition-all duration-200 hover:scale-105 group"
              onClick={() => handleReportClick(report.path)}
            >
              <div className="flex flex-col items-center text-center p-4">
                <div
                  className={`w-16 h-16 ${report.bgColor} rounded-2xl flex items-center justify-center mb-4 group-hover:scale-110 transition-transform`}
                >
                  <report.icon className={`w-8 h-8 ${report.color}`} />
                </div>
                <h3 className="text-lg font-bold text-gray-800 mb-2">
                  {report.title}
                </h3>
                <p className="text-sm text-gray-500 mb-4 min-h-[40px]">
                  {report.description}
                </p>
                <button className="w-full px-4 py-2 bg-gray-100 hover:bg-primary-600 hover:text-white text-gray-700 rounded-lg transition-colors font-medium">
                  فتح التقرير
                </button>
              </div>
            </Card>
          ))}
        </div>
      </section>

      {/* Info Card */}
      <Card className="bg-gradient-to-br from-primary-50 to-primary-100 border-primary-200">
        <div className="flex items-start gap-4 p-4">
          <div className="w-12 h-12 bg-primary-500 rounded-xl flex items-center justify-center flex-shrink-0">
            <BarChart3 className="w-6 h-6 text-white" />
          </div>
          <div>
            <h3 className="text-lg font-bold text-primary-800 mb-2">
              💡 نصيحة
            </h3>
            <p className="text-primary-700">
              يمكنك استخدام الفلاتر في كل تقرير لتخصيص البيانات حسب الفترة الزمنية
              أو الفرع. جميع التقارير تدعم التصدير إلى CSV حيثما ينطبق.
            </p>
          </div>
        </div>
      </Card>
    </div>
  );
};

export default ReportsDashboardPage;
