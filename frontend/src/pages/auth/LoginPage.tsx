import { useState } from "react";
import { useAuth } from "@/hooks/useAuth";
import { Button } from "@/components/common/Button";
import { Input } from "@/components/common/Input";
import { Eye, EyeOff, LogIn } from "lucide-react";

export const LoginPage = () => {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const { login, isLoggingIn } = useAuth();

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    login({ email, password });
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-600 to-primary-800 flex items-center justify-center p-4">
      <div className="bg-white rounded-3xl shadow-2xl p-8 w-full max-w-md animate-fade-in">
        {/* Logo */}
        <div className="text-center mb-8">
          <div className="w-20 h-20 bg-primary-100 rounded-2xl flex items-center justify-center mx-auto mb-4">
            <span className="text-4xl">🏪</span>
          </div>
          <h1 className="text-3xl font-bold text-primary-600">TajerPro</h1>
          <p className="text-gray-500 mt-2">نظام نقاط البيع</p>
        </div>

        {/* Form */}
        <form onSubmit={handleSubmit} className="space-y-5">
          <Input
            label="البريد الإلكتروني"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="admin@kasserpro.com"
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

        {/* Demo Credentials — Development Only */}
        {import.meta.env.DEV && (
          <div className="mt-6 p-4 bg-gray-50 rounded-xl text-sm">
            <p className="font-medium text-gray-700 mb-2">بيانات تجريبية:</p>
            <p className="text-gray-600">
              <span className="font-medium">المدير:</span> admin@kasserpro.com /
              Admin@123
            </p>
            <p className="text-gray-600">
              <span className="font-medium">الكاشير:</span> ahmed@kasserpro.com
              / 123456
            </p>
          </div>
        )}
      </div>
    </div>
  );
};

export default LoginPage;
