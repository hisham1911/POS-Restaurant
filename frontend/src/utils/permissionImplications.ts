const PERMISSION_IMPLICATIONS: Record<string, string[]> = {
  OrdersCreate: ["OrdersView"],
  ProductsManage: ["ProductsView"],
  CategoriesManage: ["CategoriesView"],
  ExpensesCreate: ["ExpensesView"],
  ExpensesManage: ["ExpensesView"],
  InventoryManage: ["InventoryView"],
  InventoryTransfer: ["InventoryView"],
  CashRegisterManage: ["CashRegisterView"],
  SuppliersManage: ["SuppliersView"],
  PurchaseInvoicesManage: ["PurchaseInvoicesView"],
  UsersManage: ["UsersView"],
  DeliveryManage: ["DeliveryView"],
  PosSell: [
    "OrdersView",
    "OrdersCreate",
    "ProductsView",
    "CategoriesView",
    "InventoryView",
    "BranchesView",
  ],
};

export const expandPermissions = (permissions: string[]): string[] => {
  const effectivePermissions = new Set(permissions);
  let changed = true;

  while (changed) {
    changed = false;

    for (const permission of Array.from(effectivePermissions)) {
      for (const impliedPermission of PERMISSION_IMPLICATIONS[permission] ?? []) {
        if (!effectivePermissions.has(impliedPermission)) {
          effectivePermissions.add(impliedPermission);
          changed = true;
        }
      }
    }
  }

  return Array.from(effectivePermissions);
};
