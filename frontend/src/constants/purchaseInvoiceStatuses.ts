export const purchaseInvoiceStatusColors: Record<string, string> = {
  Draft: "bg-gray-100 text-gray-800",
  Confirmed: "bg-blue-100 text-blue-800",
  Paid: "bg-green-100 text-green-800",
  PartiallyPaid: "bg-yellow-100 text-yellow-800",
  Cancelled: "bg-red-100 text-red-800",
  Returned: "bg-orange-100 text-orange-800",
  PartiallyReturned: "bg-amber-100 text-amber-800",
};

export const purchaseInvoiceStatusLabels: Record<string, string> = {
  Draft: "مسودة",
  Confirmed: "مؤكدة",
  Paid: "مدفوعة",
  PartiallyPaid: "مدفوعة جزئياً",
  Cancelled: "ملغاة",
  Returned: "مُسترد",
  PartiallyReturned: "مسترد جزئياً",
};
