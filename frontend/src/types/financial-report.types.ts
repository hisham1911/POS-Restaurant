// Profit & Loss Report Types
export interface ExpenseCategoryBreakdown {
  categoryId: number;
  categoryName: string;
  totalAmount: number;
  expenseCount: number;
  percentage: number;
}

export interface ProfitLossReport {
  fromDate: string;
  toDate: string;
  branchId: number;
  branchName?: string;
  
  // Revenue
  grossSales: number;
  totalDiscount: number;
  netSales: number;
  totalTax: number;
  totalRevenue: number;
  
  // Cost of Goods Sold
  totalCost: number;
  grossProfit: number;
  grossProfitMargin: number;
  
  // Operating Expenses
  totalExpenses: number;
  expensesByCategory: ExpenseCategoryBreakdown[];
  
  // Net Profit
  netProfit: number;
  netProfitMargin: number;
  
  // Additional Metrics
  totalOrders: number;
  averageOrderValue: number;
  refundsAmount: number;
}

// Expenses Report Types
export interface DailyExpense {
  date: string;
  amount: number;
  count: number;
}

export interface ExpenseDetail {
  id: number;
  date: string;
  categoryName: string;
  description: string;
  amount: number;
  paymentMethod: string;
  recipientName?: string;
}

export interface ExpensesReport {
  fromDate: string;
  toDate: string;
  branchId: number;
  branchName?: string;
  
  totalExpenses: number;
  totalExpenseCount: number;
  averageExpenseAmount: number;
  
  expensesByCategory: ExpenseCategoryBreakdown[];
  
  cashExpenses: number;
  cardExpenses: number;
  otherExpenses: number;
  
  dailyExpenses: DailyExpense[];
  topExpenses: ExpenseDetail[];
}
