import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { useTransferCashMutation } from "@/api/cashRegisterApi";
import { useGetBranchesQuery } from "@/api/branchesApi";
import type { TransferCashDto } from "@/types/cashRegister.types";
import { Button } from "@/components/common/Button";

const schema = z
  .object({
    sourceBranchId: z.number().min(1, "اختر الفرع المرسِل"),
    targetBranchId: z.number().min(1, "اختر الفرع المستقبِل"),
    amount: z.number().min(0.01, "المبلغ يجب أن يكون أكبر من صفر"),
    description: z.string().optional(),
    transactionDate: z.string().min(1, "التاريخ مطلوب"),
  })
  .refine((d) => d.sourceBranchId !== d.targetBranchId, {
    message: "الفرع المرسِل والمستقبِل لا يمكن أن يكونا نفس الفرع",
    path: ["targetBranchId"],
  });

type FormData = z.infer<typeof schema>;

interface Props {
  isOpen: boolean;
  onClose: () => void;
}

export const TransferCashModal = ({ isOpen, onClose }: Props) => {
  const { data: branchesData } = useGetBranchesQuery();
  const [transfer, { isLoading }] = useTransferCashMutation();
  const branches = branchesData?.data ?? [];

  const {
    register,
    handleSubmit,
    formState: { errors },
    reset,
    setError,
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { transactionDate: new Date().toISOString().split("T")[0] },
  });

  const onSubmit = async (data: FormData) => {
    try {
      await transfer(data as TransferCashDto).unwrap();
      toast.success("تم تحويل النقد بنجاح");
      reset();
      onClose();
    } catch (err) {
      const error = err as { data: { errorCode: string; message: string } };
      switch (error.data?.errorCode) {
        case "CASH_REGISTER_INSUFFICIENT_BALANCE":
          setError("amount", { message: "رصيد الفرع المرسِل غير كافٍ" });
          break;
        case "BRANCH_NOT_FOUND":
          toast.error("أحد الفروع غير موجود");
          break;
        case "TENANT_ISOLATION_VIOLATION":
          toast.error("لا يمكن التحويل بين فروع مختلفة");
          break;
        default:
          toast.error(error.data?.message ?? "حدث خطأ أثناء التحويل");
      }
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-md p-6">
        <h2 className="text-lg font-bold text-gray-800 mb-6">
          تحويل نقدي بين الفروع
        </h2>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              الفرع المرسِل
            </label>
            <select
              {...register("sourceBranchId", { valueAsNumber: true })}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
            >
              <option value={0}>اختر فرع</option>
              {branches.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name}
                </option>
              ))}
            </select>
            {errors.sourceBranchId && (
              <p className="text-danger-600 text-xs mt-1">
                {errors.sourceBranchId.message}
              </p>
            )}
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              الفرع المستقبِل
            </label>
            <select
              {...register("targetBranchId", { valueAsNumber: true })}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
            >
              <option value={0}>اختر فرع</option>
              {branches.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name}
                </option>
              ))}
            </select>
            {errors.targetBranchId && (
              <p className="text-danger-600 text-xs mt-1">
                {errors.targetBranchId.message}
              </p>
            )}
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              المبلغ (جنيه)
            </label>
            <input
              type="number"
              step="0.01"
              min="0"
              {...register("amount", { valueAsNumber: true })}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
              placeholder="0.00"
            />
            {errors.amount && (
              <p className="text-danger-600 text-xs mt-1">
                {errors.amount.message}
              </p>
            )}
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              التاريخ
            </label>
            <input
              type="date"
              {...register("transactionDate")}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              ملاحظة (اختياري)
            </label>
            <input
              type="text"
              {...register("description")}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
              placeholder="سبب التحويل..."
            />
          </div>

          <div className="flex gap-3 mt-2">
            <Button
              type="button"
              variant="outline"
              className="flex-1"
              onClick={onClose}
            >
              إلغاء
            </Button>
            <Button
              type="submit"
              variant="primary"
              className="flex-1"
              disabled={isLoading}
            >
              {isLoading ? "جاري التحويل..." : "تأكيد التحويل"}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
};
