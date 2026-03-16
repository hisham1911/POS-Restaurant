// Product Report Types

export interface ProductMovementDetail {
  productId: number;
  productName: string;
  sku?: string;
  categoryName?: string;
  quantitySold: number;
  totalRevenue: number;
  totalCost: number;
  grossProfit: number;
  profitMargin: number;
  openingStock: number;
  purchasedQuantity: number;
  transferredIn: number;
  transferredOut: number;
  closingStock: number;
  turnoverRate: number;
  daysToSellOut: number;
}

export interface ProductMovementReport {
  fromDate: string;
  toDate: string;
  branchId: number;
  branchName?: string;
  totalProducts: number;
  productsSold: number;
  productsNotSold: number;
  totalRevenue: number;
  productMovements: ProductMovementDetail[];
}

export interface ProfitableProductDetail {
  productId: number;
  productName: string;
  categoryName?: string;
  quantitySold: number;
  revenue: number;
  cost: number;
  profit: number;
  profitMargin: number;
  averageSellingPrice: number;
  averageCost: number;
}

export interface ProfitableProductsReport {
  fromDate: string;
  toDate: string;
  branchId: number;
  branchName?: string;
  totalRevenue: number;
  totalCost: number;
  totalProfit: number;
  averageProfitMargin: number;
  topProfitableProducts: ProfitableProductDetail[];
  leastProfitableProducts: ProfitableProductDetail[];
}

export interface SlowMovingProductDetail {
  productId: number;
  productName: string;
  categoryName?: string;
  currentStock: number;
  quantitySold: number;
  averageDailySales: number;
  daysOfStock: number;
  lastSoldDate?: string;
  daysSinceLastSale: number;
  stockValue: number;
  movementStatus: string;
}

export interface SlowMovingProductsReport {
  fromDate: string;
  toDate: string;
  branchId: number;
  branchName?: string;
  totalSlowMovingProducts: number;
  totalValueAtRisk: number;
  totalQuantityAtRisk: number;
  slowMovingProducts: SlowMovingProductDetail[];
}

export interface CogsCategoryBreakdown {
  categoryId: number;
  categoryName: string;
  openingValue: number;
  purchases: number;
  closingValue: number;
  cogs: number;
  revenue: number;
  grossProfit: number;
  grossProfitMargin: number;
}

export interface CogsReport {
  fromDate: string;
  toDate: string;
  branchId: number;
  branchName?: string;
  openingInventoryValue: number;
  totalPurchases: number;
  closingInventoryValue: number;
  costOfGoodsSold: number;
  totalRevenue: number;
  grossProfit: number;
  grossProfitMargin: number;
  categoryBreakdown: CogsCategoryBreakdown[];
}
