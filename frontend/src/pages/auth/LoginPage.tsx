import { useState } from "react";
import { useAuth } from "@/hooks/useAuth";
import { Button } from "@/components/common/Button";
import { Input } from "@/components/common/Input";
import {
  BadgeCheck,
  Eye,
  EyeOff,
  LogIn,
  Mail,
  Sparkles,
  KeyRound,
} from "lucide-react";

const DEMO_ACCOUNT = {
  email: "karim@supermarket.com",
  password: "Admin@123",
};

export const LoginPage = () => {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const { login, isLoggingIn } = useAuth();

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    login({ email, password });
  };

  const fillDemoAccount = () => {
    setEmail(DEMO_ACCOUNT.email);
    setPassword(DEMO_ACCOUNT.password);
  };

  return (
    <div className="min-h-screen bg-[radial-gradient(circle_at_15%_20%,#dbeafe_0%,#eff6ff_35%,#f8fafc_70%)] p-4 sm:p-6 lg:p-10">
      <div className="mx-auto grid min-h-[calc(100vh-2rem)] w-full max-w-6xl grid-cols-1 overflow-hidden rounded-3xl border border-slate-200/70 bg-white/90 shadow-[0_32px_80px_-40px_rgba(15,23,42,0.45)] backdrop-blur-sm lg:grid-cols-[1.1fr_0.9fr]">
        <section className="relative hidden overflow-hidden bg-[linear-gradient(145deg,#0f172a_0%,#1e293b_45%,#1d4ed8_100%)] p-10 text-white lg:flex lg:flex-col lg:justify-between">
          <div className="absolute -left-14 -top-14 h-56 w-56 rounded-full bg-blue-300/20 blur-2xl" />
          <div className="absolute -bottom-16 -right-10 h-64 w-64 rounded-full bg-cyan-300/20 blur-2xl" />

          <div className="relative">
            <div className="mb-6 inline-flex items-center gap-2 rounded-full border border-white/20 bg-white/10 px-4 py-2 text-xs font-semibold tracking-wide text-blue-100">
              <Sparkles className="h-4 w-4" />
              تاجر برو | نظام نقاط البيع
            </div>

            <h1 className="text-4xl font-black leading-tight">
              تاجر برو
              <span className="mt-1 block text-xl font-medium text-blue-100">
                نظام بسيط لإدارة المبيعات والفواتير والمخزون
              </span>
            </h1>

            <p className="mt-6 max-w-md text-sm leading-7 text-blue-100/90">
              كل ما تحتاجه لإدارة الشغل اليومي في مكان واحد: بيع أسرع، متابعة
              واضحة، وتقارير مفهومة.
            </p>
          </div>

          <div className="relative space-y-3 rounded-2xl border border-white/20 bg-white/10 p-5">
            <div className="flex items-center gap-3 text-sm">
              <BadgeCheck className="h-5 w-5 text-emerald-300" />
              واجهة سهلة وسريعة لكل الموظفين
            </div>
            <div className="flex items-center gap-3 text-sm">
              <BadgeCheck className="h-5 w-5 text-amber-300" />
              بيع وفواتير ومخزون من نفس النظام
            </div>
            <div className="flex items-center gap-3 text-sm">
              <LogIn className="h-5 w-5 text-cyan-300" />
              تقارير يومية تساعدك تتابع الأداء
            </div>
          </div>
        </section>

        <section className="flex items-center justify-center p-5 sm:p-8 lg:p-10">
          <div className="w-full max-w-md animate-fade-in">
            <div className="mb-7 text-center">
              <div className="mx-auto mb-4 inline-flex h-20 w-20 items-center justify-center rounded-3xl bg-[linear-gradient(140deg,#1d4ed8_0%,#f97316_100%)] text-white shadow-[0_22px_35px_-24px_rgba(37,99,235,0.8)]">
                <span className="text-4xl font-black">ت</span>
              </div>

              <h2 className="text-3xl font-black text-slate-800">
                تسجيل الدخول
              </h2>
              <p className="mt-2 text-sm text-slate-500">
                أدخل بيانات حسابك للوصول إلى النظام
              </p>
            </div>

            <form onSubmit={handleSubmit} className="space-y-5">
              <Input
                label="البريد الإلكتروني"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="مثال: karim@supermarket.com"
                required
              />

              <div className="relative">
                <Input
                  label="كلمة المرور"
                  type={showPassword ? "text" : "password"}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="••••••••"
                  required
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  className="absolute left-3 top-[38px] text-gray-400 hover:text-gray-600 transition-colors"
                >
                  {showPassword ? (
                    <EyeOff className="w-5 h-5" />
                  ) : (
                    <Eye className="w-5 h-5" />
                  )}
                </button>
              </div>

              <Button
                type="submit"
                variant="primary"
                size="xl"
                className="w-full"
                isLoading={isLoggingIn}
                rightIcon={<LogIn className="w-5 h-5" />}
              >
                تسجيل الدخول
              </Button>
            </form>

            <div className="mt-6 rounded-2xl border border-amber-200 bg-amber-50/80 p-4">
              <div className="mb-3 flex items-center gap-2 text-sm font-bold text-amber-900">
                <BadgeCheck className="h-4 w-4" />
                حساب تجريبي للعملاء
              </div>

              <div className="space-y-2 text-sm text-amber-900">
                <p className="flex items-center gap-2">
                  <Mail className="h-4 w-4 text-amber-700" />
                  <span className="font-medium">البريد:</span>
                  <span className="font-semibold">{DEMO_ACCOUNT.email}</span>
                </p>
                <p className="flex items-center gap-2">
                  <KeyRound className="h-4 w-4 text-amber-700" />
                  <span className="font-medium">كلمة المرور:</span>
                  <span className="font-semibold">{DEMO_ACCOUNT.password}</span>
                </p>
              </div>

              <button
                type="button"
                onClick={fillDemoAccount}
                className="mt-3 w-full rounded-lg border border-amber-300 bg-white px-3 py-2 text-sm font-semibold text-amber-900 transition hover:bg-amber-100"
              >
                استخدام الحساب التجريبي تلقائيًا
              </button>
            </div>
          </div>
        </section>
      </div>
    </div>
  );
};

export default LoginPage;
