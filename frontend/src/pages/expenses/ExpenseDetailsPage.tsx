import { useState } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import {
  ArrowRight,
  Check,
  X,
  DollarSign,
  FileText,
  Download,
  Trash2,
  ChevronDown,
} from "lucide-react";
import { toast } from "sonner";
import {
  useGetExpenseByIdQuery,
  useApproveExpenseMutation,
  useRejectExpenseMutation,
  usePayExpenseMutation,
  useDeleteAttachmentMutation,
} from "../../api/expensesApi";
import { Button } from "../../components/common/Button";
import { Card } from "../../components/common/Card";
import { Loading } from "../../components/common/Loading";
import { Modal } from "../../components/common/Modal";
import { formatDateOnly, formatDateTimeFull } from "../../utils/formatters";
import { handleApiError } from "../../utils/errorHandler";
import type { ExpenseStatus } from "../../types/expense.types";

const getPaymentMethodLabel = (method?: string) => {
  switch (method) {
    case "Cash":
      return "نقدي";
    case "Card":
      return "بطاقة";
    case "BankTransfer":
      return "تحويل بنكي";
    case "Fawry":
      return "فوري";
    default:
      return method || "غير محدد";
  }
};

export function ExpenseDetailsPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [showApproveModal, setShowApproveModal] = useState(false);
  const [showRejectModal, setShowRejectModal] = useState(false);
  const [showPayModal, setShowPayModal] = useState(false);
  const [rejectReason, setRejectReason] = useState("");
  const [paymentMethod, setPaymentMethod] = useState("Cash");
  const [paymentNotes, setPaymentNotes] = useState("");
  const [approveNotes, setApproveNotes] = useState("");

  const {
    data: response,
    isLoading,
    error,
  } = useGetExpenseByIdQuery(Number(id));
  const [approveExpense, { isLoading: isApproving }] =
    useApproveExpenseMutation();
  const [rejectExpense, { isLoading: isRejecting }] =
    useRejectExpenseMutation();
  const [payExpense, { isLoading: isPaying }] = usePayExpenseMutation();
  const [deleteAttachment] = useDeleteAttachmentMutation();

  const expense = response?.data;

  const openPayModal = () => {
    setPaymentMethod("Cash");
    setPaymentNotes("");
    setShowPayModal(true);
  };

  const closePayModal = () => {
    setShowPayModal(false);
    setPaymentMethod("Cash");
    setPaymentNotes("");
  };

  const handleApprove = async () => {
    try {
      await approveExpense({
        id: Number(id),
        request: { notes: approveNotes || undefined },
      }).unwrap();
      setShowApproveModal(false);
      setApproveNotes("");
    } catch (error) {
      console.error("Failed to approve expense:", error);
    }
  };

  const handleReject = async () => {
    if (!rejectReason.trim()) {
      alert("يرجى إدخال سبب الرفض");
      return;
    }
    try {
      await rejectExpense({
        id: Number(id),
        request: { reason: rejectReason },
      }).unwrap();
      setShowRejectModal(false);
      setRejectReason("");
    } catch (error) {
      console.error("Failed to reject expense:", error);
    }
  };

  const handlePay = async () => {
    try {
      await payExpense({
        id: Number(id),
        request: {
          paymentMethod,
          notes: paymentNotes || undefined,
        },
      }).unwrap();

      const methodLabel = getPaymentMethodLabel(paymentMethod);
      if (paymentMethod === "Cash") {
        toast.success("تم تسجيل الدفع النقدي وخصم المبلغ من الخزينة");
      } else {
        toast.success(
          `تم تسجيل الدفع بطريقة ${methodLabel} (لا يؤثر على الخزينة)`,
        );
      }

      closePayModal();
    } catch (error) {
      toast.error(handleApiError(error));
      console.error("Failed to pay expense:", error);
    }
  };

  const handleDeleteAttachment = async (attachmentId: number) => {
    if (window.confirm("هل أنت متأكد من حذف هذا المرفق؟")) {
      try {
        await deleteAttachment({
          expenseId: Number(id),
          attachmentId,
        }).unwrap();
      } catch (error) {
        console.error("Failed to delete attachment:", error);
      }
    }
  };

  const getStatusBadge = (status: ExpenseStatus) => {
    const badges = {
      Draft: "bg-gray-100 text-gray-800",
      Approved: "bg-blue-100 text-blue-800",
      Paid: "bg-green-100 text-green-800",
      Rejected: "bg-red-100 text-red-800",
    };
    const labels = {
      Draft: "مسودة",
      Approved: "معتمد",
      Paid: "مدفوع",
      Rejected: "مرفوض",
    };
    return (
      <span
        className={`px-3 py-1 text-sm font-semibold rounded-full ${badges[status]}`}
      >
        {labels[status]}
      </span>
    );
  };

  if (isLoading) return <Loading />;
  if (error || !expense)
    return <div className="text-red-600">حدث خطأ في تحميل المصروف</div>;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex justify-between items-center">
        <div className="flex items-center gap-4">
          <Link to="/expenses">
            <Button variant="outline">
              <ArrowRight className="w-4 h-4 ml-2" />
              رجوع
            </Button>
          </Link>
          <div>
            <h1 className="text-2xl font-bold text-gray-900">تفاصيل المصروف</h1>
            <p className="text-gray-600 mt-1">{expense.expenseNumber}</p>
          </div>
        </div>
        <div className="flex gap-2">
          {expense.status === "Draft" && (
            <>
              <Button
                variant="success"
                onClick={() => setShowApproveModal(true)}
              >
                <Check className="w-4 h-4 ml-2" />
                اعتماد
              </Button>
              <Button variant="danger" onClick={() => setShowRejectModal(true)}>
                <X className="w-4 h-4 ml-2" />
                رفض
              </Button>
            </>
          )}
          {expense.status === "Approved" && (
            <Button onClick={openPayModal}>
              <DollarSign className="w-4 h-4 ml-2" />
              دفع
            </Button>
          )}
        </div>
      </div>

      {/* Main Info */}
      <Card>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div>
            <h3 className="text-lg font-semibold text-gray-900 mb-4">
              المعلومات الأساسية
            </h3>
            <div className="space-y-3">
              <div>
                <span className="text-sm text-gray-600">رقم المصروف:</span>
                <p className="font-medium">{expense.expenseNumber}</p>
              </div>
              <div>
                <span className="text-sm text-gray-600">التصنيف:</span>
                <p className="font-medium">{expense.categoryName}</p>
              </div>
              <div>
                <span className="text-sm text-gray-600">المبلغ:</span>
                <p className="text-xl font-bold text-gray-900">
                  {expense.amount.toFixed(2)} جنيه
                </p>
              </div>
              <div>
                <span className="text-sm text-gray-600">تاريخ المصروف:</span>
                <p className="font-medium">
                  {formatDateOnly(expense.expenseDate)}
                </p>
              </div>
              <div>
                <span className="text-sm text-gray-600">الحالة:</span>
                <div className="mt-1">{getStatusBadge(expense.status)}</div>
              </div>
            </div>
          </div>

          <div>
            <h3 className="text-lg font-semibold text-gray-900 mb-4">
              تفاصيل إضافية
            </h3>
            <div className="space-y-3">
              <div>
                <span className="text-sm text-gray-600">الوصف:</span>
                <p className="font-medium">{expense.description}</p>
              </div>
              {expense.vendorName && (
                <div>
                  <span className="text-sm text-gray-600">اسم المورد:</span>
                  <p className="font-medium">{expense.vendorName}</p>
                </div>
              )}
              {expense.receiptNumber && (
                <div>
                  <span className="text-sm text-gray-600">رقم الإيصال:</span>
                  <p className="font-medium">{expense.receiptNumber}</p>
                </div>
              )}
              {expense.notes && (
                <div>
                  <span className="text-sm text-gray-600">ملاحظات:</span>
                  <p className="font-medium">{expense.notes}</p>
                </div>
              )}
              <div>
                <span className="text-sm text-gray-600">الفرع:</span>
                <p className="font-medium">{expense.branchName}</p>
              </div>
            </div>
          </div>
        </div>
      </Card>

      {/* Approval/Rejection Info */}
      {(expense.status === "Approved" ||
        expense.status === "Paid" ||
        expense.status === "Rejected") && (
        <Card>
          <h3 className="text-lg font-semibold text-gray-900 mb-4">
            {expense.status === "Rejected"
              ? "معلومات الرفض"
              : "معلومات الاعتماد"}
          </h3>
          <div className="space-y-3">
            {expense.status === "Rejected" ? (
              <>
                <div>
                  <span className="text-sm text-gray-600">سبب الرفض:</span>
                  <p className="font-medium text-red-600">
                    {expense.rejectionReason}
                  </p>
                </div>
                <div>
                  <span className="text-sm text-gray-600">
                    تم الرفض بواسطة:
                  </span>
                  <p className="font-medium">{expense.rejectedByUserName}</p>
                </div>
                <div>
                  <span className="text-sm text-gray-600">تاريخ الرفض:</span>
                  <p className="font-medium">
                    {expense.rejectedAt &&
                      formatDateTimeFull(expense.rejectedAt)}
                  </p>
                </div>
              </>
            ) : (
              <>
                <div>
                  <span className="text-sm text-gray-600">
                    تم الاعتماد بواسطة:
                  </span>
                  <p className="font-medium">{expense.approvedByUserName}</p>
                </div>
                <div>
                  <span className="text-sm text-gray-600">تاريخ الاعتماد:</span>
                  <p className="font-medium">
                    {expense.approvedAt &&
                      formatDateTimeFull(expense.approvedAt)}
                  </p>
                </div>
              </>
            )}
          </div>
        </Card>
      )}

      {/* Payment Info */}
      {expense.status === "Paid" && (
        <Card>
          <h3 className="text-lg font-semibold text-gray-900 mb-4">
            معلومات الدفع
          </h3>
          <div className="space-y-3">
            <div>
              <span className="text-sm text-gray-600">طريقة الدفع:</span>
              <p className="font-medium">
                {getPaymentMethodLabel(expense.paymentMethod)}
              </p>
            </div>
            <div>
              <span className="text-sm text-gray-600">تم الدفع بواسطة:</span>
              <p className="font-medium">{expense.paidByUserName}</p>
            </div>
            <div>
              <span className="text-sm text-gray-600">تاريخ الدفع:</span>
              <p className="font-medium">
                {expense.paidAt && formatDateTimeFull(expense.paidAt)}
              </p>
            </div>
          </div>
        </Card>
      )}

      {/* Attachments */}
      {expense.attachments && expense.attachments.length > 0 && (
        <Card>
          <h3 className="text-lg font-semibold text-gray-900 mb-4">المرفقات</h3>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {expense.attachments.map((attachment) => (
              <div
                key={attachment.id}
                className="border border-gray-200 rounded-lg p-4"
              >
                <div className="flex items-start justify-between">
                  <div className="flex items-center gap-2">
                    <FileText className="w-5 h-5 text-gray-400" />
                    <div>
                      <p className="text-sm font-medium text-gray-900">
                        {attachment.fileName}
                      </p>
                      <p className="text-xs text-gray-500">
                        {(attachment.fileSize / 1024).toFixed(2)} KB
                      </p>
                    </div>
                  </div>
                  <div className="flex gap-1">
                    <a
                      href={attachment.fileUrl}
                      download
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      <button className="text-blue-600 hover:text-blue-900">
                        <Download className="w-4 h-4" />
                      </button>
                    </a>
                    {expense.status === "Draft" && (
                      <button
                        onClick={() => handleDeleteAttachment(attachment.id)}
                        className="text-red-600 hover:text-red-900"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        </Card>
      )}

      {/* Approve Modal */}
      <Modal
        isOpen={showApproveModal}
        onClose={() => setShowApproveModal(false)}
        title="اعتماد المصروف"
      >
        <div className="space-y-4">
          <p className="text-gray-600">هل أنت متأكد من اعتماد هذا المصروف؟</p>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              ملاحظات (اختياري)
            </label>
            <textarea
              value={approveNotes}
              onChange={(e) => setApproveNotes(e.target.value)}
              rows={3}
              className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              placeholder="أضف ملاحظات إن وجدت..."
            />
          </div>
          <div className="flex gap-2 justify-end">
            <Button
              variant="outline"
              onClick={() => setShowApproveModal(false)}
            >
              إلغاء
            </Button>
            <Button
              variant="success"
              onClick={handleApprove}
              disabled={isApproving}
            >
              {isApproving ? "جاري الاعتماد..." : "اعتماد"}
            </Button>
          </div>
        </div>
      </Modal>

      {/* Reject Modal */}
      <Modal
        isOpen={showRejectModal}
        onClose={() => setShowRejectModal(false)}
        title="رفض المصروف"
      >
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              سبب الرفض <span className="text-red-500">*</span>
            </label>
            <textarea
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              rows={3}
              className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              placeholder="اكتب سبب رفض المصروف..."
              required
            />
          </div>
          <div className="flex gap-2 justify-end">
            <Button variant="outline" onClick={() => setShowRejectModal(false)}>
              إلغاء
            </Button>
            <Button
              variant="danger"
              onClick={handleReject}
              disabled={isRejecting}
            >
              {isRejecting ? "جاري الرفض..." : "رفض"}
            </Button>
          </div>
        </div>
      </Modal>

      {/* Pay Modal */}
      <Modal isOpen={showPayModal} onClose={closePayModal} title="دفع المصروف">
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              طريقة الدفع <span className="text-red-500">*</span>
            </label>
            <div className="relative">
              <select
                value={paymentMethod}
                onChange={(e) => setPaymentMethod(e.target.value)}
                className="w-full appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 hover:border-gray-400 transition-all duration-200 shadow-sm"
              >
                <option value="Cash">نقدي</option>
                <option value="Card">بطاقة</option>
                <option value="BankTransfer">تحويل بنكي</option>
              </select>
              <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
            </div>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              ملاحظات (اختياري)
            </label>
            <textarea
              value={paymentNotes}
              onChange={(e) => setPaymentNotes(e.target.value)}
              rows={3}
              className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              placeholder="أضف ملاحظات إن وجدت..."
            />
          </div>
          <div className="bg-blue-50 border border-blue-200 rounded-md p-3">
            <p className="text-sm text-blue-800">
              <strong>المبلغ المطلوب دفعه:</strong> {expense.amount.toFixed(2)}{" "}
              جنيه
            </p>
            {paymentMethod !== "Cash" && (
              <p className="text-sm text-blue-800 mt-1">
                الدفعات غير النقدية لا تُسجل في معاملات الخزينة.
              </p>
            )}
          </div>
          <div className="flex gap-2 justify-end">
            <Button variant="outline" onClick={closePayModal}>
              إلغاء
            </Button>
            <Button onClick={handlePay} disabled={isPaying}>
              {isPaying ? "جاري الدفع..." : "تأكيد الدفع"}
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
