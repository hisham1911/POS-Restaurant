import { useMemo, useState } from "react";
import {
  ArrowRight,
  CalendarDays,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  CircleDollarSign,
  Clock3,
  Eye,
  MapPin,
  NotebookPen,
  Route,
  Truck,
  UserRound,
  XCircle,
} from "lucide-react";
import { toast } from "sonner";
import clsx from "clsx";
import { Button } from "@/components/common/Button";
import { Card } from "@/components/common/Card";
import { Input } from "@/components/common/Input";
import { Loading } from "@/components/common/Loading";
import { Modal } from "@/components/common/Modal";
import { OrderDetailsModal } from "@/components/orders/OrderDetailsModal";
import {
  useAssignDeliveryPersonMutation,
  useGetActiveDeliveryPersonsQuery,
  useGetDeliveryOrdersQuery,
  useUpdateDeliveryStatusMutation,
} from "@/api/deliveryApi";
import { useGetOrderQuery } from "@/api/ordersApi";
import type {
  DeliveryOrderDto,
  DeliveryOrderFilters,
} from "@/types/delivery.types";
import { formatCurrency, formatDateTime } from "@/utils/formatters";

const DELIVERY_TABS = [
  { status: "PendingAssignment", label: "غير معين" },
  { status: "Assigned", label: "معين" },
  { status: "OutForDelivery", label: "في الطريق" },
  { status: "Delivered", label: "تم التوصيل" },
  { status: "Cancelled", label: "ملغي" },
] as const;

const getDeliveryStatusMeta = (status?: string) => {
  switch (status) {
    case "Assigned":
      return {
        label: "معين",
        className: "border border-sky-200 bg-sky-50 text-sky-700",
      };
    case "OutForDelivery":
      return {
        label: "في الطريق",
        className: "border border-amber-200 bg-amber-50 text-amber-700",
      };
    case "Delivered":
      return {
        label: "تم التوصيل",
        className: "border border-emerald-200 bg-emerald-50 text-emerald-700",
      };
    case "Cancelled":
      return {
        label: "ملغي",
        className: "border border-rose-200 bg-rose-50 text-rose-700",
      };
    default:
      return {
        label: "في انتظار التعيين",
        className: "border border-slate-200 bg-slate-50 text-slate-700",
      };
  }
};

const getAvailableStatusOptions = (status?: string) => {
  switch (status) {
    case "Assigned":
      return ["OutForDelivery", "Delivered", "Cancelled"];
    case "OutForDelivery":
      return ["Delivered", "Cancelled"];
    case "PendingAssignment":
      return ["Cancelled"];
    default:
      return [];
  }
};

const formatDateOrDash = (value?: string) => {
  if (!value) {
    return "—";
  }

  return formatDateTime(value);
};

export default function DeliveryOperationsPage() {
  const [activeStatus, setActiveStatus] =
    useState<(typeof DELIVERY_TABS)[number]["status"]>("PendingAssignment");
  const [page, setPage] = useState(1);
  const [deliveryPersonFilter, setDeliveryPersonFilter] = useState("");
  const [dateFilter, setDateFilter] = useState("");
  const [assignmentOrder, setAssignmentOrder] = useState<DeliveryOrderDto | null>(
    null,
  );
  const [selectedDeliveryPersonId, setSelectedDeliveryPersonId] = useState("");
  const [assignmentNotes, setAssignmentNotes] = useState("");
  const [statusOrder, setStatusOrder] = useState<DeliveryOrderDto | null>(null);
  const [nextStatus, setNextStatus] = useState("");
  const [statusNotes, setStatusNotes] = useState("");
  const [selectedOrderId, setSelectedOrderId] = useState<number | null>(null);

  const filters = useMemo<DeliveryOrderFilters>(
    () => ({
      page,
      pageSize: 12,
      status: activeStatus,
      deliveryPersonId: deliveryPersonFilter
        ? Number(deliveryPersonFilter)
        : undefined,
      date: dateFilter || undefined,
    }),
    [activeStatus, dateFilter, deliveryPersonFilter, page],
  );

  const {
    data: deliveryOrdersResponse,
    isLoading,
    isFetching,
  } = useGetDeliveryOrdersQuery(filters);
  const { data: deliveryPersonsResponse, isLoading: isLoadingPersons } =
    useGetActiveDeliveryPersonsQuery();
  const { data: selectedOrderResponse, isFetching: isFetchingOrder } =
    useGetOrderQuery(selectedOrderId ?? 0, {
      skip: selectedOrderId === null,
    });

  const [assignDeliveryPerson, { isLoading: isAssigning }] =
    useAssignDeliveryPersonMutation();
  const [updateDeliveryStatus, { isLoading: isUpdatingStatus }] =
    useUpdateDeliveryStatusMutation();

  const orders = deliveryOrdersResponse?.data?.items ?? [];
  const paging = deliveryOrdersResponse?.data;
  const deliveryPersons = deliveryPersonsResponse?.data ?? [];
  const selectedOrder = selectedOrderResponse?.data;
  const statusOptions = statusOrder
    ? getAvailableStatusOptions(statusOrder.deliveryStatus)
    : [];

  const openAssignmentModal = (order: DeliveryOrderDto) => {
    setAssignmentOrder(order);
    setSelectedDeliveryPersonId(order.deliveryPersonId?.toString() ?? "");
    setAssignmentNotes(order.deliveryNotes ?? "");
  };

  const openStatusModal = (order: DeliveryOrderDto) => {
    const availableOptions = getAvailableStatusOptions(order.deliveryStatus);
    setStatusOrder(order);
    setNextStatus(availableOptions[0] ?? "");
    setStatusNotes(order.deliveryNotes ?? "");
  };

  const handleAssignDeliveryPerson = async () => {
    if (!assignmentOrder) {
      return;
    }

    if (!selectedDeliveryPersonId) {
      toast.error("اختر مندوبًا أولًا");
      return;
    }

    try {
      await assignDeliveryPerson({
        orderId: assignmentOrder.id,
        body: {
          deliveryPersonId: Number(selectedDeliveryPersonId),
          deliveryNotes: assignmentNotes.trim() || undefined,
        },
      }).unwrap();

      toast.success("تم حفظ تعيين المندوب");
      setAssignmentOrder(null);
      setSelectedDeliveryPersonId("");
      setAssignmentNotes("");
    } catch {
      // Global API handling shows the toast.
    }
  };

  const handleUpdateStatus = async () => {
    if (!statusOrder || !nextStatus) {
      toast.error("اختر الحالة الجديدة أولًا");
      return;
    }

    try {
      await updateDeliveryStatus({
        orderId: statusOrder.id,
        body: {
          deliveryStatus: nextStatus,
          deliveryNotes: statusNotes.trim() || undefined,
        },
      }).unwrap();

      toast.success("تم تحديث حالة الطلب");
      setStatusOrder(null);
      setNextStatus("");
      setStatusNotes("");
    } catch {
      // Global API handling shows the toast.
    }
  };

  if (isLoading || isLoadingPersons) {
    return <Loading />;
  }

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="mx-auto max-w-7xl space-y-5 px-4 py-5 sm:px-6 lg:px-8">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <div className="mb-3 inline-flex h-12 w-12 items-center justify-center rounded-2xl bg-primary-50 text-primary-600">
              <Truck className="h-6 w-6" />
            </div>
            <h1 className="text-3xl font-black text-slate-900">
              إدارة التوصيل
            </h1>
            <p className="mt-2 text-sm text-slate-600">
              متابعة طلبات التوصيل، تعيين المناديب، وتحريك الطلبات بين
              المراحل حتى التسليم.
            </p>
          </div>

          <Card className="min-w-[15rem] bg-white/90">
            <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-400">
              الطلبات المطابقة
            </p>
            <p className="mt-2 text-3xl font-black text-slate-900">
              {paging?.totalCount ?? 0}
            </p>
            <p className="mt-1 text-sm text-slate-500">
              {isFetching ? "جارٍ تحديث القائمة..." : "بحسب الحالة والفلاتر الحالية"}
            </p>
          </Card>
        </div>

        <Card className="space-y-4">
          <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_minmax(0,220px)]">
            <div>
              <label className="mb-2 block text-sm font-medium text-slate-700">
                المندوب
              </label>
              <div className="relative">
                <UserRound className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                <select
                  value={deliveryPersonFilter}
                  onChange={(event) => {
                    setDeliveryPersonFilter(event.target.value);
                    setPage(1);
                  }}
                  className="w-full rounded-xl border border-slate-300 bg-white py-2.5 pe-10 ps-4 text-sm text-slate-900 outline-none transition focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
                >
                  <option value="">كل المناديب</option>
                  {deliveryPersons.map((person) => (
                    <option key={person.id} value={person.id}>
                      {person.name}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div>
              <label className="mb-2 block text-sm font-medium text-slate-700">
                التاريخ
              </label>
              <div className="relative">
                <CalendarDays className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                <Input
                  type="date"
                  value={dateFilter}
                  onChange={(event) => {
                    setDateFilter(event.target.value);
                    setPage(1);
                  }}
                  className="pe-10"
                />
              </div>
            </div>

            <div className="flex items-end">
              <Button
                variant="outline"
                className="w-full"
                onClick={() => {
                  setDeliveryPersonFilter("");
                  setDateFilter("");
                  setPage(1);
                }}
              >
                تصفير الفلاتر
              </Button>
            </div>
          </div>

          <div className="flex flex-wrap gap-2">
            {DELIVERY_TABS.map((tab) => {
              const isActive = activeStatus === tab.status;
              return (
                <button
                  key={tab.status}
                  type="button"
                  onClick={() => {
                    setActiveStatus(tab.status);
                    setPage(1);
                  }}
                  className={clsx(
                    "rounded-full px-4 py-2 text-sm font-semibold transition-all",
                    isActive
                      ? "bg-primary-600 text-white shadow-sm"
                      : "border border-slate-200 bg-white text-slate-700 hover:bg-slate-50",
                  )}
                >
                  {tab.label}
                </button>
              );
            })}
          </div>
        </Card>

        {orders.length === 0 ? (
          <Card className="flex min-h-[18rem] flex-col items-center justify-center gap-3 text-center">
            <Route className="h-10 w-10 text-slate-300" />
            <h2 className="text-xl font-bold text-slate-900">
              لا توجد طلبات مطابقة
            </h2>
            <p className="max-w-md text-sm text-slate-500">
              جرّب تغيير الحالة أو الفلاتر لعرض مجموعة أخرى من طلبات التوصيل.
            </p>
          </Card>
        ) : (
          <div className="grid gap-4 lg:grid-cols-2 2xl:grid-cols-3">
            {orders.map((order) => {
              const deliveryStatus = getDeliveryStatusMeta(order.deliveryStatus);
              const canAssign =
                order.deliveryStatus === "PendingAssignment" ||
                order.deliveryStatus === "Assigned";
              const canUpdateStatus =
                getAvailableStatusOptions(order.deliveryStatus).length > 0;

              return (
                <Card key={order.id} className="space-y-4 border-slate-200">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-400">
                        طلب توصيل
                      </p>
                      <h2 className="mt-1 text-xl font-black text-slate-900">
                        #{order.orderNumber}
                      </h2>
                    </div>

                    <span
                      className={clsx(
                        "inline-flex rounded-full px-3 py-1 text-xs font-semibold",
                        deliveryStatus.className,
                      )}
                    >
                      {deliveryStatus.label}
                    </span>
                  </div>

                  <div className="grid gap-3 sm:grid-cols-2">
                    <div className="rounded-2xl bg-slate-50 p-3">
                      <p className="mb-2 flex items-center gap-2 text-xs font-semibold text-slate-500">
                        <MapPin className="h-4 w-4 text-primary-500" />
                        العنوان
                      </p>
                      <p className="text-sm font-medium text-slate-900">
                        {order.deliveryAddress || "—"}
                      </p>
                    </div>

                    <div className="rounded-2xl bg-slate-50 p-3">
                      <p className="mb-2 flex items-center gap-2 text-xs font-semibold text-slate-500">
                        <CircleDollarSign className="h-4 w-4 text-primary-500" />
                        الرسوم والإجمالي
                      </p>
                      <p className="text-sm font-medium text-slate-900">
                        رسوم: {formatCurrency(order.deliveryFee)}
                      </p>
                      <p className="mt-1 text-sm text-slate-600">
                        الإجمالي: {formatCurrency(order.total)}
                      </p>
                    </div>
                  </div>

                  <div className="grid gap-3 sm:grid-cols-2">
                    <div className="rounded-2xl border border-slate-200 p-3">
                      <p className="mb-2 flex items-center gap-2 text-xs font-semibold text-slate-500">
                        <UserRound className="h-4 w-4 text-slate-400" />
                        المندوب
                      </p>
                      <p className="text-sm font-semibold text-slate-900">
                        {order.deliveryPersonName || "غير معين"}
                      </p>
                    </div>

                    <div className="rounded-2xl border border-slate-200 p-3">
                      <p className="mb-2 flex items-center gap-2 text-xs font-semibold text-slate-500">
                        <NotebookPen className="h-4 w-4 text-slate-400" />
                        ملاحظات التوصيل
                      </p>
                      <p className="line-clamp-2 text-sm text-slate-700">
                        {order.deliveryNotes || "—"}
                      </p>
                    </div>
                  </div>

                  <div className="grid gap-3 text-sm text-slate-600 sm:grid-cols-2">
                    <div className="rounded-2xl bg-white p-3 ring-1 ring-slate-200">
                      <p className="mb-2 flex items-center gap-2 font-semibold text-slate-500">
                        <Clock3 className="h-4 w-4 text-slate-400" />
                        وقت الإنشاء
                      </p>
                      <p className="font-medium text-slate-900">
                        {formatDateTime(order.createdAt)}
                      </p>
                    </div>

                    <div className="rounded-2xl bg-white p-3 ring-1 ring-slate-200">
                      <p className="mb-2 flex items-center gap-2 font-semibold text-slate-500">
                        <CheckCircle2 className="h-4 w-4 text-slate-400" />
                        وقت التعيين
                      </p>
                      <p className="font-medium text-slate-900">
                        {formatDateOrDash(order.assignedAt)}
                      </p>
                    </div>
                  </div>

                  <div className="flex flex-wrap gap-2 border-t border-slate-200 pt-4">
                    {canAssign && (
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => openAssignmentModal(order)}
                        rightIcon={<ArrowRight className="h-4 w-4" />}
                      >
                        {order.deliveryStatus === "Assigned"
                          ? "تغيير مندوب"
                          : "عيّن مندوب"}
                      </Button>
                    )}

                    {canUpdateStatus && (
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => openStatusModal(order)}
                        rightIcon={<Truck className="h-4 w-4" />}
                      >
                        تحديث الحالة
                      </Button>
                    )}

                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setSelectedOrderId(order.id)}
                      rightIcon={<Eye className="h-4 w-4" />}
                    >
                      تفاصيل الطلب
                    </Button>
                  </div>
                </Card>
              );
            })}
          </div>
        )}

        {paging && paging.totalPages > 1 && (
          <Card className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <p className="text-sm text-slate-600">
              صفحة {paging.page} من {paging.totalPages} ({paging.totalCount} طلب)
            </p>

            <div className="grid grid-cols-2 gap-2 sm:flex">
              <Button
                variant="outline"
                size="sm"
                disabled={!paging.hasPreviousPage}
                onClick={() => setPage((currentPage) => Math.max(1, currentPage - 1))}
                rightIcon={<ChevronRight className="h-4 w-4" />}
              >
                السابق
              </Button>
              <Button
                variant="outline"
                size="sm"
                disabled={!paging.hasNextPage}
                onClick={() => setPage((currentPage) => currentPage + 1)}
                leftIcon={<ChevronLeft className="h-4 w-4" />}
              >
                التالي
              </Button>
            </div>
          </Card>
        )}

        <Modal
          isOpen={assignmentOrder !== null}
          onClose={() => {
            setAssignmentOrder(null);
            setSelectedDeliveryPersonId("");
            setAssignmentNotes("");
          }}
          title={
            assignmentOrder?.deliveryStatus === "Assigned"
              ? "تغيير المندوب"
              : "تعيين مندوب"
          }
        >
          <div className="space-y-4">
            <div>
              <label className="mb-2 block text-sm font-medium text-slate-700">
                المندوب
              </label>
              <select
                value={selectedDeliveryPersonId}
                onChange={(event) => setSelectedDeliveryPersonId(event.target.value)}
                className="w-full rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm text-slate-900 outline-none transition focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
              >
                <option value="">اختر مندوبًا</option>
                {deliveryPersons.map((person) => (
                  <option key={person.id} value={person.id}>
                    {person.name}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="mb-2 block text-sm font-medium text-slate-700">
                ملاحظات
              </label>
              <textarea
                value={assignmentNotes}
                onChange={(event) => setAssignmentNotes(event.target.value)}
                rows={3}
                className="w-full rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
                placeholder="ملاحظات إضافية للمندوب"
              />
            </div>

            <div className="flex justify-end gap-2">
              <Button
                variant="outline"
                onClick={() => {
                  setAssignmentOrder(null);
                  setSelectedDeliveryPersonId("");
                  setAssignmentNotes("");
                }}
              >
                إلغاء
              </Button>
              <Button onClick={handleAssignDeliveryPerson} isLoading={isAssigning}>
                حفظ
              </Button>
            </div>
          </div>
        </Modal>

        <Modal
          isOpen={statusOrder !== null}
          onClose={() => {
            setStatusOrder(null);
            setNextStatus("");
            setStatusNotes("");
          }}
          title="تحديث حالة التوصيل"
        >
          <div className="space-y-4">
            <div>
              <label className="mb-2 block text-sm font-medium text-slate-700">
                الحالة الجديدة
              </label>
              <select
                value={nextStatus}
                onChange={(event) => setNextStatus(event.target.value)}
                className="w-full rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm text-slate-900 outline-none transition focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
              >
                {statusOptions.length === 0 ? (
                  <option value="">لا توجد حالة متاحة</option>
                ) : (
                  statusOptions.map((status) => (
                    <option key={status} value={status}>
                      {getDeliveryStatusMeta(status).label}
                    </option>
                  ))
                )}
              </select>
            </div>

            <div>
              <label className="mb-2 block text-sm font-medium text-slate-700">
                ملاحظات
              </label>
              <textarea
                value={statusNotes}
                onChange={(event) => setStatusNotes(event.target.value)}
                rows={3}
                className="w-full rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-primary-500 focus:ring-2 focus:ring-primary-100"
                placeholder="دوّن أي ملاحظة مرتبطة بالحالة الجديدة"
              />
            </div>

            <div className="flex justify-end gap-2">
              <Button
                variant="outline"
                onClick={() => {
                  setStatusOrder(null);
                  setNextStatus("");
                  setStatusNotes("");
                }}
              >
                إلغاء
              </Button>
              <Button
                onClick={handleUpdateStatus}
                isLoading={isUpdatingStatus}
                disabled={!nextStatus}
              >
                تحديث
              </Button>
            </div>
          </div>
        </Modal>

        {selectedOrderId !== null && isFetchingOrder && !selectedOrder && (
          <Modal
            isOpen
            onClose={() => setSelectedOrderId(null)}
            title="تحميل تفاصيل الطلب"
          >
            <div className="py-8">
              <Loading />
            </div>
          </Modal>
        )}

        {selectedOrderId !== null && selectedOrder && (
          <OrderDetailsModal
            order={selectedOrder}
            onClose={() => setSelectedOrderId(null)}
          />
        )}
      </div>
    </div>
  );
}
