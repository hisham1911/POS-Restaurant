# مراجعة كل رقم في التطبيق - Zero Tolerance Audit

التاريخ: 2026-05-05  
النطاق: تم تنفيذ ملف `C:\Users\Hisham\Downloads\COMPLETE_NUMBERS_AUDIT_PROMPT.md` على ملفات الحسابات المطلوبة: الطلب، المخزون، الوصفة، الوردية، الخزينة، التقارير، وحالات الحافة والاتساق.  
مهم: لم يتم تعديل أي كود تطبيق. هذا الملف تقرير فقط.

## القسم A - جدول المعادلات

| الرقم | المعادلة الفعلية | السطر | صح/غلط | السبب |
|---|---|---|---|---|
| `OrderItem.UnitPrice` | السعر يأتي من `ProductBatch.SellingPrice` ثم `BranchProductPrice.Price` ثم `Product.Price`، ولو المنتج شامل الضريبة يتم تحويله إلى صافي السعر: `gross / (1 + tax/100)` مع تقريب 4 خانات | `OrderService.cs:183`, `OrderService.cs:211`, `OrderService.cs:1632`, `OrderService.cs:1636`, `OrderService.cs:1643` | صح | ليس من `Product.Price` مباشرة؛ الأولوية موجودة في الكود. |
| `OrderItem.UnitCost` | `batchCost ?? product.AverageCost ?? product.Cost` | `OrderService.cs:212` | صح بتحفظ | صحيح كـ snapshot، لكن لو كل القيم null تصبح التكلفة null وتظهر كـ 0 في التقارير. |
| `OrderItem.DiscountAmount` | `Subtotal = Round(UnitPrice * Quantity, 2)` ثم الخصم percentage = clamp 0..100، أو fixed = clamp 0..Subtotal | `OrderService.cs:1467`, `OrderService.cs:1474`, `OrderService.cs:1479` | صح حسابيا | يمنع السالب/الزيادة بالحساب، لكنه لا يرفض الإدخال الخاطئ كـ validation. |
| `OrderItem.TaxAmount` | `Round((Subtotal - DiscountAmount) * TaxRate / 100, 2)` | `OrderService.cs:1488`, `OrderService.cs:1492` | صح بتحفظ | صحيح بعد خصم البند، لكن يعتمد على TaxRate غير محمي في المنتج/الصنف المخصص. |
| `OrderItem.Subtotal` | `Round(UnitPrice * Quantity, 2)` | `OrderService.cs:1467`, `OrderService.cs:1470` | صح | Subtotal قبل الخصم والضريبة. |
| `OrderItem.Total` | `Round(Subtotal - DiscountAmount + TaxAmount, 2)` | `OrderService.cs:1488`, `OrderService.cs:1495` | صح | إجمالي البند بعد خصم البند وضريبته. |
| `Order.Subtotal` | `Round(Sum(OrderItem.Subtotal), 2)` | `OrderService.cs:1501` | صح | يجمع Subtotal قبل خصومات البنود. |
| `Order.DiscountAmount` | خصم الطلب فقط: يطبق على `Order.Subtotal - Sum(Item.DiscountAmount)`، percentage clamp 0..100 أو fixed clamp 0..net | `OrderService.cs:1504`, `OrderService.cs:1508`, `OrderService.cs:1513` | صح بتحفظ | هذا ليس إجمالي كل الخصومات؛ خصومات البنود منفصلة. |
| `Order.TaxAmount` | لو يوجد خصم طلب: يعيد حساب الضريبة لكل بند بعد توزيع نسبة خصم الطلب. وإلا: `Sum(Item.TaxAmount)` | `OrderService.cs:1532`, `OrderService.cs:1533`, `OrderService.cs:1543` | صح | الضريبة لا تحسب على مبلغ قبل خصم الطلب. |
| `Order.ServiceChargeAmount` | `Round((netAfterItemDiscounts - Order.DiscountAmount) * ServiceChargePercent / 100, 2)` | `OrderService.cs:1547` | صح | الخدمة بعد خصومات البنود وخصم الطلب. |
| `Order.DeliveryFee` | ينسخ من `CreateOrderRequest.DeliveryFee` ويدخل مباشرة في الإجمالي | `OrderService.cs:120`, `OrderService.cs:1550` | غلط | لا يوجد validation يمنع DeliveryFee السالب. |
| `Order.Total` | `Round(afterDiscount + TaxAmount + ServiceChargeAmount + DeliveryFee, 2)` | `OrderService.cs:1550` | صح بتحفظ | صحيح حسب الكود، لكنه يتأثر برسوم توصيل سالبة. |
| `Order.AmountPaid` | مجموع المدفوعات المقبولة بعد قص كل Payment إلى المتبقي من Total، ثم `Min(totalPaid, orderTotal)` | `OrderService.cs:723`, `OrderService.cs:728`, `OrderService.cs:740`, `OrderService.cs:759` | صح | التخزين يمنع AmountPaid أكبر من Total. |
| `Order.AmountDue` | `Max(0, Round(orderTotal - totalPaid, 2))` عند الإغلاق، وفي إعادة الحساب: `Round(Total - AmountPaid, 2)` | `OrderService.cs:760`, `OrderService.cs:1551` | صح | يحمي من الدين السالب عند الإغلاق. |
| `Order.ChangeAmount` | `Max(0, Round(totalPaymentAmount - orderTotal, 2))` | `OrderService.cs:761` | صح | يعتمد على إجمالي المطلوب دفعه قبل قص المدفوعات. |
| `Order.RefundAmount` | يزيد بـ `totalRefundAmount` مع cap على `originalOrder.Total` | `OrderService.cs:1157`, `OrderService.cs:1158`, `OrderService.cs:1255`, `OrderService.cs:1256` | صح بتحفظ | لا يتجاوز الإجمالي، لكن معادلة refund نفسها بها مشاكل عند وجود خصم طلب. |
| `Partial refund amount` | `unitPriceWithTax = orderItem.Total / orderItem.Quantity`; ثم `Round(unitPriceWithTax * refundQty, 2)` | `OrderService.cs:1074`, `OrderService.cs:1075` | غلط | لا يوزع خصم الطلب أو رسوم الخدمة/التوصيل على البنود. |
| `Full refund amount` | لكل بند: `Round(item.Total * remainingQty / item.Quantity, 2)` | `OrderService.cs:1180`, `OrderService.cs:1181` | غلط | يستخدم مجموع البنود قبل خصم الطلب؛ قد يفشل full refund لو كان هناك خصم طلب. |
| `BranchInventory.Quantity بعد البيع` | `inventory.Quantity -= OrderItem.Quantity`، والحركة `Quantity = -OrderItem.Quantity` | `InventoryService.cs:338`, `InventoryService.cs:405`, `InventoryService.cs:408` | صح بتحفظ | لا يحدث شيء لو لا يوجد BranchInventory و`AllowNegativeStock=true`. |
| `BranchInventory.Quantity بعد المرتجع` | `inventory.Quantity += refundQuantity` | `InventoryService.cs:455`, `InventoryService.cs:472`, `InventoryService.cs:473` | صح | يرجع على مخزون الفرع. |
| `BranchInventory.Quantity بعد الشراء` | لو لا يوجد صف: `Quantity = item.Quantity`; لو موجود: `Quantity += item.Quantity` | `PurchaseInvoiceService.cs:563`, `PurchaseInvoiceService.cs:572`, `PurchaseInvoiceService.cs:573` | صح | تحديث فرعي وليس `Product.StockQuantity`. |
| `BranchInventory.Quantity بعد التحويل` | اعتماد التحويل: source `-= transfer.Quantity`، الاستلام: destination `+= transfer.Quantity`، الإلغاء بعد الاعتماد: source `+= transfer.Quantity` | `InventoryService.cs:672`, `InventoryService.cs:767`, `InventoryService.cs:849` | صح بتحفظ | لا يوجد validation صريح يمنع Quantity سالبة عند إنشاء التحويل. |
| `BranchInventory.Quantity بعد الجرد` | `diff = ActualQuantity - SystemQuantity` ثم `inventory.Quantity = ActualQuantity` وحركة بكمية `diff` | `StockTakingService.cs:302`, `StockTakingService.cs:339`, `StockTakingService.cs:348` | غلط/خطر | لو المخزون تغير بعد snapshot، الجرد يكتب Actual قديم فوق الرصيد الحالي. |
| `ProductBatch.Quantity بعد البيع` | يختار batch نشط بكمية > 0، مرتب بـ `PurchaseDate`, ثم `CreatedAt`, ثم `Id`، ويخصم `Min(batch.Quantity, remaining)` | `InventoryService.cs:343`, `InventoryService.cs:349`, `InventoryService.cs:381`, `InventoryService.cs:382` | غلط | المطلوب FEFO، لكن الكود FIFO حسب تاريخ الشراء وليس أقرب ExpiryDate. |
| `ProductBatch.Quantity بعد المرتجع` | لو `BatchId` موجود: `batch.Quantity += refundQuantity` ويرجع Depleted إلى Active | `InventoryService.cs:478`, `InventoryService.cs:488`, `InventoryService.cs:489` | صح بتحفظ | لو البيع سحب من أكثر من batch، `OrderItem` يحتفظ بـ batch أساسي واحد فقط. |
| `StockMovement.BalanceBefore` | يأخذ الرصيد الحالي قبل التعديل في أغلب المسارات | `InventoryService.cs:330`, `InventoryService.cs:472`, `PurchaseInvoiceService.cs:572`, `StockTakingService.cs:338` | صح | مصدره قاعدة البيانات/الكيان قبل التعديل. |
| `StockMovement.BalanceAfter` | الرصيد بعد التعديل، وغالبا يساوي `BalanceBefore + Quantity` لأن `Quantity` قد تكون سالبة | `InventoryService.cs:408`, `InventoryService.cs:508`, `PurchaseInvoiceService.cs:589`, `StockTakingService.cs:352` | صح | اتجاه الحركة في الإشارة. |
| `StockMovement.Quantity` | موجب للإضافة، سالب للبيع/الخصم/الخروج | `StockMovement.cs:22`, `StockMovement.cs:25`, `InventoryService.cs:405`, `PurchaseInvoiceService.cs:822` | صح | ليس دائما موجب. |
| `RecipeIngredient.Cost` | `(RawMaterial.Cost ?? 0) * Ingredient.Quantity` | `RecipeService.cs:144`, `RecipeService.cs:207` | صح بتحفظ | تكلفة صفرية مسموحة لو المادة بلا Cost. |
| `Recipe.TotalCost` | `Sum((RawMaterial.Cost ?? 0) * Ingredient.Quantity)` | `RecipeService.cs:153`, `RecipeService.cs:217`, `RecipeService.cs:328`, `RecipeService.cs:336` | صح | لا يقسم على YieldQuantity. |
| `Recipe.CostPerUnit` | غير مخزن؛ يحسب داخل هامش الربح فقط: `TotalCost / YieldQuantity` | `RecipeService.cs:344` | صح بتحفظ | YieldQuantity=0 غير محمي. |
| `Recipe.ProfitMargin` | `(Product.Price - TotalCost/YieldQuantity) / Product.Price * 100` | `RecipeService.cs:341`, `RecipeService.cs:344`, `RecipeService.cs:346` | صح بتحفظ | صحيح لو YieldQuantity موجب. |
| `خصم مكونات الوصفة عند البيع` | `requiredQty = RecipeIngredient.Quantity * OrderItem.Quantity` | `OrderService.cs:817`, `RecipeService.cs:278`, `RecipeService.cs:295` | غلط | لا يقسم على `YieldQuantity` كما يتوقع نموذج الوصفات الإنتاجية. |
| `دقة كميات الوصفة` | Decimal عادي بلا rounding أو precision في الخدمة؛ 0.333 * 3 = 0.999 | `RecipeIngredient.cs:10`, `RecipeService.cs:278` | صح حسب الكود | لا يوجد تحويل إلى 1.000. |
| `Shift.TotalCashSales` | `SumAppliedPayments(salesOrders, Cash)` | `ShiftService.cs:57`, `ShiftService.cs:759` | صح | من Payment records وليس CashRegisterTransactions. |
| `Shift.TotalCardSales` | غير موجود في enum أو DTO الحالي | `PaymentMethod.cs:6`, `ShiftDto.cs:56` | غير موجود | النظام الحالي لديه Cash/BankAccount/Wallet فقط. |
| `Shift.TotalFawrySales` | غير موجود | `PaymentMethod.cs:6`, `ShiftDto.cs:56` | غير موجود | لا يوجد نوع دفع Fawry. |
| `Shift.TotalVodafoneCashSales` | غير موجود | `PaymentMethod.cs:6`, `ShiftDto.cs:58` | غير موجود | المحافظ مجمعة كـ Wallet فقط. |
| `Shift.TotalBankTransferSales` | ممثل فعليا باسم `BankAccount` | `PaymentMethod.cs:11`, `ShiftService.cs:760` | صح بالاسم الحالي | لا يوجد اسم BankTransfer مستقل. |
| `Shift.TotalRefunds` | `Round(returnPayments.Sum(p.Amount), 2)` | `ShiftService.cs:756`, `ShiftService.cs:776` | غلط | `RefundAsync` لا ينشئ Payment للـ return order، فيظهر غالبا 0. |
| `Shift.TotalExpenses` | داخل snapshot = 0، وفي DTO = `shift.Expenses.Sum(e.Amount)` | `ShiftService.cs:794`, `ShiftService.cs:939` | غير متسق | الحساب في snapshot لا يستخدم المصروفات. |
| `Shift.TotalCreditSales` | `Sum(AmountDue)` للطلبات البيعية التي عليها دين | `ShiftService.cs:786`, `ShiftService.cs:787` | صح | يعرض الدين المتبقي، لا قيمة الطلبات الآجلة الأصلية. |
| `Shift.ExpectedBalance` | في الإغلاق: آخر `CashRegisterTransaction.BalanceAfter`، fallback فقط `OpeningBalance + TotalCash` | `ShiftService.cs:272`, `ShiftService.cs:819`, `ShiftService.cs:827` | صح بتحفظ | ليس معادلة صريحة من الطلبات؛ يعتمد على الخزينة. |
| `Shift.Difference` | `Round(ClosingBalance - ExpectedBalance, 2)` | `ShiftService.cs:271`, `ShiftService.cs:273` | صح | الاتجاه واضح: الفعلي ناقص المتوقع. |
| `Shift.NetCash` | `TotalCash = CashSales - CashRefundPayments` ثم `NetCash = TotalCash` | `ShiftService.cs:763`, `ShiftService.cs:790` | صح بتحفظ | لا يخصم المصروفات النقدية ولا دين العملاء. |
| `CashRegister.CurrentBalance` | آخر `CashRegisterTransaction.BalanceAfter` | `CashRegisterService.cs:36`, `CashRegisterService.cs:57`, `CashRegisterService.cs:549`, `CashRegisterService.cs:556` | صح | ليس `Sum(Amount)`. |
| `Cash balance بعد بيع نقدي` | `Sale => currentBalance + amount` | `CashRegisterService.cs:492`, `CashRegisterService.cs:493` | صح | النوع يحدد الاتجاه. |
| `Cash balance بعد مرتجع نقدي` | `Refund => currentBalance - amount` | `CashRegisterService.cs:496` | صح | Amount محفوظ موجب غالبا. |
| `Cash balance بعد مصروف` | `Expense => currentBalance - amount` | `CashRegisterService.cs:496`, `ExpenseService.cs:493` | صح | يوجد فحص رصيد قبل مصروف نقدي. |
| `Cash balance بعد Adjustment` | `Adjustment => currentBalance + amount` | `CashRegisterService.cs:500` | صح بتحفظ | amount قد يكون سالبا من إغلاق الوردية. |
| `Cash balance بعد إيداع يدوي` | `Deposit => currentBalance + request.Amount` | `CashRegisterService.cs:193`, `CashRegisterService.cs:204` | غلط/خطر | لا يوجد validation يمنع amount سالب في الخزينة اليدوية. |
| `Sales report TotalSales` | في `GetSalesReportAsync`: `Sum(salesOrders.Total) - Abs(Sum(returnOrders.Total))` | `ReportService.cs:356`, `ReportService.cs:357`, `ReportService.cs:358` | صح | يستبعد الملغى ويشمل Completed/PartiallyRefunded/Refunded. |
| `Daily report GrossSales` | `Sum(completed non-return orders.Subtotal)` | `ReportService.cs:85`, `ReportService.cs:121` | صح | gross قبل المرتجعات ثم يخصمها في actualGrossSales. |
| `Daily report TotalDiscount` | `Sum(item discounts) + Sum(order discounts) - Abs(return discounts)` | `ReportService.cs:122`, `ReportService.cs:126`, `ReportService.cs:127` | صح | ليس فقط `Order.DiscountAmount`. |
| `Daily report TotalTax` | `Sum(non-return Order.TaxAmount) - Abs(return TaxAmount)` | `ReportService.cs:129`, `ReportService.cs:138` | صح | يحسب صافي الضريبة بعد المرتجعات. |
| `Daily report ServiceCharge` | لا يوجد حقل/تجميع صريح للخدمة في التقرير اليومي المقروء | `ReportService.cs:247`, `ReportService.cs:285` | غير موجود | الطلب طلب مراجعته، لكنه غير ظاهر كرقم مستقل في الكود. |
| `Daily report OrderCount` | `orders.Count` من كل طلبات الورديات، والتفصيل completed/refund منفصل | `ReportService.cs:83`, `ReportService.cs:258`, `ReportService.cs:259` | صح بتحفظ | قد يشمل حالات غير مبيعات في عداد عام حسب الورديات. |
| `Daily report AverageOrderValue` | `actualTotalSales / completedOrders.Count` | `ReportService.cs:264`, `ReportService.cs:265` | صح | لا يقسم على كل orders. |
| `ProfitLoss COGS` | `Sum(UnitCost * Quantity)` للطلبات - تكلفة المرتجعات | `FinancialReportService.cs:92`, `FinancialReportService.cs:97`, `FinancialReportService.cs:100` | صح | يستخدم snapshot التاريخي. |
| `ProfitLoss GrossProfit` | `actualNetSales - netCost` | `FinancialReportService.cs:87`, `FinancialReportService.cs:102` | صح بتحفظ | يستخدم NetSales بدون الضريبة والإيرادات الإضافية. |
| `ProfitLoss GrossProfitMargin` | `grossProfit / actualNetSales * 100` | `FinancialReportService.cs:103`, `FinancialReportService.cs:104` | صح | محمي من القسمة على صفر. |
| `ProfitLoss NetProfit` | `grossProfit - totalExpenses` | `FinancialReportService.cs:118`, `FinancialReportService.cs:137` | صح | المصروفات Paid فقط. |
| `Inventory report TotalValue` | `Quantity * Product.AverageCost` | `InventoryReportService.cs:71`, `InventoryReportService.cs:72`, `InventoryReportService.cs:83` | صح بتحفظ | لا يستخدم Cost fallback، بخلاف COGS. |
| `Low stock` | `Quantity <= ReorderLevel` | `InventoryReportService.cs:56`, `InventoryReportService.cs:57`, `InventoryReportService.cs:299` | صح | يستخدم ReorderLevel وليس Product.ReorderPoint. |
| `TurnoverRate` | `QuantitySold / CurrentStock` | `ProductReportService.cs:141` | صح بتحفظ | ليس متوسط المخزون؛ قد يكون مضللا كمعدل دوران محاسبي. |
| `Customer total purchases` | `Sum(Order.Total) - customer returns` | `CustomerReportService.cs:93`, `CustomerReportService.cs:122`, `CustomerReportService.cs:124` | صح | من الطلبات وليس من Customer.TotalSpent. |
| `Customer debts report` | مصدر الدين هو `CustomerBranchBalance.AmountDue` | `CustomerReportService.cs:180`, `CustomerReportService.cs:181`, `CustomerReportService.cs:191` | صح | لا يحسب من `Order.AmountDue`. |
| `Loyalty points on sale` | `Floor(Order.Total)` | `OrderService.cs:840`, `CustomerService.cs:247` | صح | يضاف عند إتمام الطلب. |
| `Loyalty points on refund` | `Floor(totalRefundAmount)` ثم لا يسمح بالسالب | `OrderService.cs:1318`, `CustomerService.cs:278`, `CustomerService.cs:279` | صح | محمي من الرصيد السالب. |
| `PurchaseInvoice item total` | `Quantity * PurchasePrice` | `PurchaseInvoiceService.cs:324`, `PurchaseInvoiceService.cs:335` | صح | لا يوجد rounding للبند عند الإنشاء. |
| `PurchaseInvoice subtotal/tax/total` | `Subtotal = sum items`; `Tax = subtotal * taxRate/100`; `Total = Subtotal + Tax`; `AmountDue = Total` | `PurchaseInvoiceService.cs:343`, `PurchaseInvoiceService.cs:345`, `PurchaseInvoiceService.cs:346`, `PurchaseInvoiceService.cs:347` | صح بتحفظ | لا يقرب tax/total عند الإنشاء. |
| `PurchaseInvoice payment` | `AmountPaid += payment.Amount`; `AmountDue = Total - AmountPaid` | `PurchaseInvoiceService.cs:903`, `PurchaseInvoiceService.cs:904` | صح | يمنع الدفع أكبر من المستحق. |
| `AverageCost` | weighted average: `(oldStock*oldAvgCost + purchasedQty*purchasePrice) / newStock` rounded 4 | `PurchaseInvoiceService.cs:653`, `PurchaseInvoiceService.cs:660` | صح | متوسط مرجح على مخزون الفرع وقت الشراء. |
| `Supplier purchases report` | `TotalPurchases = Sum(PurchaseInvoice.Total)` مع استبعاد Cancelled | `SupplierReportService.cs:40`, `SupplierReportService.cs:45`, `SupplierReportService.cs:59`, `SupplierReportService.cs:89` | صح | الملغاة مستثناة. |
| `Average purchase price للمنتج` | لا يوجد رقم تقرير مباشر؛ المخزن المتاح هو `LastPurchasePrice` و`TotalAmountSpent/TotalQuantityPurchased` | `PurchaseInvoiceService.cs:648`, `PurchaseInvoiceService.cs:680`, `PurchaseInvoiceService.cs:682`, `PurchaseInvoiceService.cs:693` | غير موجود | لا توجد معادلة average ظاهرة في التقرير. |
| `Wallet.CurrentBalance deposit` | `CurrentBalance += Amount` | `WalletService.cs:135`, `WalletService.cs:136` | صح | محمي من amount <= 0. |
| `Wallet.CurrentBalance withdrawal` | `CurrentBalance -= Amount` | `WalletService.cs:170`, `WalletService.cs:176`, `WalletService.cs:177` | صح | يمنع السحب الأكبر من الرصيد. |
| `Wallet.CurrentBalance order payment` | `CurrentBalance += amount` القادم من `OrderService` | `OrderService.cs:864`, `OrderService.cs:871`, `WalletService.cs:239`, `WalletService.cs:240` | غلط | يستخدم مبلغ الطلب الخام capped بالـ order total وليس Payment.Amount الفعلي بعد القص. |
| `Employee revenue` | `Sum(sales order totals) - Abs(Sum(return order totals))` | `EmployeeReportService.cs:106`, `EmployeeReportService.cs:107`, `EmployeeReportService.cs:367` | صح | يستبعد returns من الإيراد. |
| `Cashier performance score` | points ثابتة حسب وجود orders/hour وAOV وcancellationRate وcompletedShifts، capped 0..100 | `EmployeeReportService.cs:137`, `EmployeeReportService.cs:141` | صح | مقياس تشغيلي لا مالي. |

## القسم B - الأرقام الغلط

### 1. خصم مكونات الوصفة لا يقسم على YieldQuantity

الرقم: كمية المادة الخام المخصومة عند بيع منتج مصنع  
المعادلة الحالية: `RecipeIngredient.Quantity * OrderItem.Quantity`  
المعادلة الصحيحة حسب نموذج yield في ملف المراجعة: `RecipeIngredient.Quantity * OrderItem.Quantity / Recipe.YieldQuantity`  
الأثر: لو الوصفة تنتج أكثر من وحدة، يتم خصم خامات أكثر من اللازم، وتصبح تكلفة/مخزون المواد الخام أقل من الحقيقة.  
الملف والسطر: `RecipeService.cs:278`, `RecipeService.cs:295`, `OrderService.cs:817`

### 2. Refund لا يوزع خصم الطلب على البنود

الرقم: قيمة المرتجع الجزئي والكامل  
المعادلة الحالية: الجزئي = `(OrderItem.Total / OrderItem.Quantity) * refundQty`، والكامل = `OrderItem.Total * remainingQty / OrderItem.Quantity`  
المعادلة الصحيحة: يجب توزيع خصم الطلب ورسوم الخدمة/التوصيل حسب سياسة واضحة، أو حساب refund من إجمالي الطلب المدفوع الفعلي بشكل نسبي.  
الأثر: مرتجع جزئي قد يرد أكثر من نصيب البند بعد خصم الطلب، ومرتجع كامل قد يفشل عندما يكون `Sum(item.Total) > originalOrder.Total`.  
الملف والسطر: `OrderService.cs:1074`, `OrderService.cs:1075`, `OrderService.cs:1180`, `OrderService.cs:1181`, `OrderService.cs:1250`

### 3. اختيار الباتش للبيع ليس FEFO

الرقم: `ProductBatch.Quantity` بعد البيع، والباتش المختار للسعر والخصم  
المعادلة الحالية: ترتيب الباتشات بـ `PurchaseDate`, ثم `CreatedAt`, ثم `Id`  
المعادلة الصحيحة لو المطلوب FEFO: الترتيب بأقرب `ExpiryDate` أولا، مع null آخر القائمة، ثم tie-breaker مناسب.  
الأثر: قد يبيع النظام من دفعة أحدث انتهاء لاحقا بينما توجد دفعة ستنتهي أولا.  
الملف والسطر: `InventoryService.cs:349`, `InventoryService.cs:1139`, `OrderService.cs:1597`

### 4. رصيد المحفظة عند دفع الطلب قد يزيد بمبلغ غير فعلي

الرقم: `Wallet.CurrentBalance` و`WalletTransaction.Amount` عند دفع طلب بمحفظة  
المعادلة الحالية: في `OrderService` يتم تمرير `Min(paymentReq.Amount, orderTotal)`، بينما Payment المخزن نفسه يتم قصه على المتبقي من الإجمالي.  
المعادلة الصحيحة: تسجيل المحفظة يجب أن يستخدم `payment.Amount` الفعلي المخزن بعد القص.  
الأثر: لو الطلب فيه مدفوعات متعددة أو overpay، Payment قد يكون 20 لكن المحفظة تسجل 80.  
الملف والسطر: `OrderService.cs:728`, `OrderService.cs:733`, `OrderService.cs:864`, `OrderService.cs:871`, `WalletService.cs:239`

### 5. مرتجعات المحافظ والبنك لا تنعكس في أرصدة المحافظ

الرقم: `Wallet.CurrentBalance` بعد refund  
المعادلة الحالية: `RefundAsync` يسجل cash refund فقط في الخزينة، ولا يوجد call مقابل للمحفظة.  
المعادلة الصحيحة: لو أصل الدفع Wallet، يجب إنشاء WalletTransaction عكسي وتخفيض `Wallet.CurrentBalance` حسب مبلغ refund الفعلي.  
الأثر: تقارير الدفع قد تعرض مرتجع، لكن رصيد المحفظة لا ينقص.  
الملف والسطر: `OrderService.cs:1344`, `OrderService.cs:1374`, `WalletService.cs:233`

### 6. `Shift.TotalRefunds` غالبا يساوي صفر رغم وجود مرتجع

الرقم: `Shift.TotalRefunds`  
المعادلة الحالية: `returnOrders.SelectMany(o.Payments).Sum(p.Amount)`  
المعادلة الصحيحة: استخدام `Abs(returnOrders.Sum(o.Total))` أو RefundLog/ReturnOrder totals حسب مصدر الحقيقة.  
الأثر: الوردية تعرض مرتجعات أقل من الحقيقة؛ لأن `RefundAsync` لا ينشئ Payment للـ return order.  
الملف والسطر: `ShiftService.cs:756`, `ShiftService.cs:776`, `OrderService.cs:723`

### 7. الجرد يكتب ActualQuantity فوق الرصيد الحالي

الرقم: `BranchInventory.Quantity` عند إكمال الجرد  
المعادلة الحالية: `inventory.Quantity = item.ActualQuantity` مع حركة `diff = Actual - System`  
المعادلة الصحيحة في وجود حركة بعد snapshot: إما منع الحركات أثناء الجرد، أو تطبيق diff على الرصيد الحالي: `current + diff`.  
الأثر: أي بيع/شراء بعد بدء الجرد وقبل إكماله يمكن أن يضيع.  
الملف والسطر: `StockTakingService.cs:302`, `StockTakingService.cs:338`, `StockTakingService.cs:339`

### 8. الجرد على Batch لا يعدل `ProductBatch.Quantity`

الرقم: `ProductBatch.Quantity` بعد الجرد  
المعادلة الحالية: يتحقق من وجود batch فقط ثم يعدل `BranchInventory.Quantity`، ولا يغير batch.  
المعادلة الصحيحة: تعديل batch المقصود أو منع batch-level stock taking بدون تحديث الباتش.  
الأثر: مجموع الباتشات لا يساوي مخزون الفرع.  
الملف والسطر: `StockTakingService.cs:305`, `StockTakingService.cs:316`, `StockTakingService.cs:339`

### 9. إنشاء تحويل بكمية سالبة غير محمي

الرقم: `InventoryTransfer.Quantity` وتأثيره على المصدر/الوجهة  
المعادلة الحالية: لا يوجد check `request.Quantity > 0` في إنشاء التحويل.  
المعادلة الصحيحة: رفض `Quantity <= 0` قبل فحص المتاح.  
الأثر: كمية سالبة تعكس اتجاه المعادلات عند الاعتماد/الاستلام.  
الملف والسطر: `InventoryService.cs:577`, `InventoryService.cs:596`, `InventoryService.cs:672`, `InventoryService.cs:767`

### 10. تقرير COGS يقدر opening inventory من closing/current

الرقم: `OpeningInventoryValue` و`CostOfGoodsSold` في COGS report  
المعادلة الحالية: `opening = Max(0, closing + totalCost - totalPurchases)` ثم `cogs = Max(0, opening + purchases - closing)`  
المعادلة الصحيحة للتقرير المحاسبي: opening يجب أن يأتي من رصيد/قيمة المخزون في بداية الفترة أو من حركات تاريخية.  
الأثر: التقرير يعطي تقديرا وليس COGS تاريخي حقيقي عند وجود تعديلات/تحويلات/جرد.  
الملف والسطر: `ProductReportService.cs:471`, `ProductReportService.cs:475`, `ProductReportService.cs:479`

### 11. سعر المنتج المقترح يستخدم FEFO لكن البيع الفعلي يستخدم FIFO

الرقم: `ProductDto.SuggestedPrice` مقابل السعر الفعلي في POS  
المعادلة الحالية: `ProductService` يختار السعر المقترح بأقرب `ExpiryDate`، لكن `OrderService` يبيع حسب `PurchaseDate`.  
المعادلة الصحيحة: نفس ترتيب الباتش في العرض والبيع.  
الأثر: المستخدم قد يرى سعر batch مختلف عن السعر المستخدم فعليا عند البيع.  
الملف والسطر: `ProductService.cs:127`, `ProductService.cs:184`, `OrderService.cs:1597`, `OrderService.cs:1632`

## القسم C - الحالات غير المحمية

| # | السيناريو | الـ validation موجود؟ | السطر أو "غير محمي" |
|---|---|---|---|
| 1 | خصم على البند = 100% | لا يوجد رفض، لكن الحساب يعمل clamp ويسمح بـ 100% | `OrderService.cs:1476` |
| 2 | خصم على البند > 100% | غير محمي كـ validation؛ يتم clamp إلى 100% | `OrderService.cs:1476` |
| 3 | خصم على الطلب كله > SubTotal | غير محمي كـ validation؛ fixed discount يتم clamp إلى net subtotal | `OrderService.cs:1515`, `OrderService.cs:1521` |
| 4 | ضريبة = 0% | مسموح ومحمى في tenant update كقيمة داخل 0..100 | `TenantService.cs:57`, `OrderService.cs:1492` |
| 5 | ضريبة < 0% | محمي في Tenant فقط؛ غير محمي في Product/Custom item | `TenantService.cs:57`, `ProductService.cs:270`, `ProductService.cs:469`, `OrderService.cs:514` |
| 6 | طلب بـ Total = 0 | غير محمي كقيمة طلب؛ يمكن بسعر صفر أو خصم 100% | `OrderService.cs:495`, `OrderService.cs:1550` |
| 7 | مدفوع أكثر من Total بأكثر من ضعف | غير محمي للنقد داخل حد `Max(total*10, total+1000)`؛ غير النقدي مرفوض | `OrderService.cs:694`, `OrderService.cs:699`, `OrderService.cs:702` |
| 8 | مرتجع بمبلغ > مبلغ الطلب الأصلي | محمي بالمتبقي وبـ cap على إجمالي الطلب | `OrderService.cs:997`, `OrderService.cs:1151`, `OrderService.cs:1158`, `OrderService.cs:1250` |
| 9 | بيع منتج Quantity = 0 مع `AllowNegativeStock=false` | محمي في إتمام الطلب بفحص المتاح | `OrderService.cs:766`, `OrderService.cs:785`, `OrderService.cs:790` |
| 10 | بيع منتج Quantity < المطلوب | محمي في create/add soft check وفي complete authoritative check | `OrderService.cs:154`, `OrderService.cs:401`, `OrderService.cs:766` |
| 11 | `YieldQuantity = 0` في الوصفة | غير محمي | `RecipeService.cs:122`, `RecipeService.cs:179`, `RecipeService.cs:344` |
| 12 | كمية مكون في الوصفة = 0 | غير محمي | `RecipeService.cs:142`, `RecipeService.cs:205` |
| 13 | سعر مادة خام = 0 أو null | غير محمي؛ يتحول إلى تكلفة صفر | `RecipeService.cs:144`, `RecipeService.cs:336` |
| 14 | فاتورة شراء بكمية = 0 | محمي | `PurchaseInvoiceService.cs:305`, `PurchaseInvoiceService.cs:399` |
| 15 | تحويل مخزون بكمية > المتاح | محمي عند الإنشاء والاعتماد | `InventoryService.cs:577`, `InventoryService.cs:653`, `InventoryService.cs:662` |
| 16 | جرد بكمية فعلية سالبة | غير محمي | `StockTakingService.cs:225`, `StockTakingService.cs:235`, `StockTakingService.cs:339` |
| 17 | OpeningBalance للوردية = سالب | غير محمي | `ShiftService.cs:175` |
| 18 | ClosingBalance للوردية = سالب | غير محمي | `ShiftService.cs:271` |
| 19 | إغلاق وردية وفيه طلبات Draft | الإغلاق العادي محمي؛ force close/auto close لا يطبقان نفس الحماية | `ShiftService.cs:250`, `ShiftService.cs:263`, `ShiftService.cs:468`, `AutoCloseShiftBackgroundService.cs:126` |
| 20 | نقاط ولاء العميل تصبح سالبة | محمي بالـ clamp إلى صفر | `CustomerService.cs:247`, `CustomerService.cs:249`, `CustomerService.cs:278`, `CustomerService.cs:279`, `CustomerService.cs:381` |
| إضافي | DeliveryFee سالب | غير محمي | `OrderService.cs:120`, `OrderService.cs:1550` |
| إضافي | Cash register deposit/withdraw amount سالب | غير محمي | `CashRegisterService.cs:160`, `CashRegisterService.cs:193`, `CashRegisterService.cs:204` |
| إضافي | Initial stock للمنتج سالب في create الكامل | غير محمي | `ProductService.cs:300`, `ProductService.cs:305`, `ProductService.cs:313` |
| إضافي | Product.TaxRate أقل من صفر | غير محمي | `ProductService.cs:270`, `ProductService.cs:469` |

## القسم D - الأرقام غير المتسقة

### 1. `sum(StockMovement.Quantity للبيع)` لا يساوي دائما `sum(OrderItem.Quantity)`

المشكلة: البيع يسجل StockMovement فقط لو يوجد `BranchInventory`. مع `AllowNegativeStock=true` وغياب صف inventory، الطلب قد يكتمل بدون حركة مخزون.  
السبب: `BatchDecrementStockAsync` يرجع مبكرا لو لا يوجد inventory، بينما check الإتمام يسمح بالسالب حسب tenant.  
الخطر: تقارير الحركات لا تطابق الطلبات.  
الأسطر: `InventoryService.cs:325`, `InventoryService.cs:338`, `OrderService.cs:766`

### 2. `Customer.TotalDue` لا يساوي دائما `sum(Order.AmountDue)`

المشكلة: دفع الدين يقلل `Customer.TotalDue` و`CustomerBranchBalance.AmountDue` لكنه لا يوزع الدفع على `Order.AmountDue`.  
السبب: `PayDebtAsync` يحدث العميل والفرع فقط؛ `Order.AmountDue` يبقى كما هو في الطلبات الأصلية.  
الخطر: شاشة العميل/تقرير الديون يعرضان رقما مختلفا عن جمع مستحقات الطلبات.  
الأسطر: `OrderService.cs:760`, `CustomerService.cs:491`, `CustomerService.cs:493`, `CustomerService.cs:702`

### 3. `CashRegister.CurrentBalance` لا يساوي `sum(CashRegisterTransaction.Amount)`

المشكلة: الرصيد الحالي هو آخر `BalanceAfter`، والـ Amount ليس له إشارة موحدة.  
السبب: Refund/Expense تسجل amount موجب والنوع يطرح، Adjustment قد يأتي بسالب، Reconcile يسجل `Abs(variance)` لكنه يثبت BalanceAfter.  
الخطر: أي تقرير يعتمد على sum(amount) سيخرج خطأ.  
الأسطر: `CashRegisterService.cs:57`, `CashRegisterService.cs:271`, `CashRegisterService.cs:284`, `CashRegisterService.cs:500`

### 4. `Shift.TotalCashSales` لا يساوي دائما صافي حركة الخزينة النقدية

المشكلة: الشيفت يحسب cash من Payment records، لكن الخزينة تشمل افتتاحية، مبيعات، مرتجعات نقدية، مصروفات، سداد ديون، supplier payments، adjustments.  
السبب: مصدران مختلفان: `ShiftService.SumAppliedPayments` مقابل `CashRegisterTransaction.BalanceAfter`.  
الخطر: أرقام الشيفت والخزينة قد تختلف، وهذا طبيعي فقط لو التسمية واضحة.  
الأسطر: `ShiftService.cs:57`, `ShiftService.cs:759`, `CashRegisterService.cs:491`, `CashRegisterService.cs:503`

### 5. معادلة المواد الخام للوصفات لا تطابق معادلة yield المطلوبة

المشكلة: `sum(StockMovement.Quantity على مادة خام)` يساوي حاليا `sum(RecipeIngredient.Quantity * OrderItem.Quantity)` وليس القسمة على YieldQuantity.  
السبب: `requiredQty` لا يستخدم `recipe.YieldQuantity`.  
الخطر: مخزون المواد الخام ينقص أكثر من اللازم.  
الأسطر: `RecipeService.cs:278`, `RecipeService.cs:295`

### 6. `Wallet.CurrentBalance` لا يطابق مدفوعات الطلبات والمرتجعات

المشكلة: الدفع بالمحفظة يسجل أحيانا مبلغ request لا مبلغ Payment الفعلي، والمرتجع لا يخصم من المحفظة.  
السبب: `OrderService` يمرر `Min(paymentReq.Amount, orderTotal)`، ولا يوجد مسار refund في WalletService.  
الخطر: رصيد المحفظة أعلى من الحقيقة.  
الأسطر: `OrderService.cs:728`, `OrderService.cs:871`, `WalletService.cs:239`

### 7. تقارير المبيعات تستخدم تعريفات مختلفة للـ Sales

المشكلة: `GetSalesReportAsync.TotalSales` يستخدم `Order.Total` بعد الضريبة والخدمة والتوصيل، بينما `ProfitLoss.NetSales` يستخدم subtotal بعد الخصم وقبل الضريبة والخدمة.  
السبب: أسماء متشابهة لمقاييس مختلفة.  
الخطر: المستخدم قد يقارن تقرير المبيعات بتقرير الأرباح ويظن أن هناك فرق حسابي.  
الأسطر: `ReportService.cs:356`, `ReportService.cs:358`, `FinancialReportService.cs:71`, `FinancialReportService.cs:76`, `FinancialReportService.cs:87`

### 8. ExpectedBalance له أكثر من تعريف في النظام

المشكلة: `ShiftService` يستخدم آخر `CashRegisterTransaction.BalanceAfter`، والـ helper يعرف معادلة `opening + sales - refunds - expenses + cashIn - cashOut`، والـ auto-close يستخدم `opening + totalCash`.  
السبب: ثلاثة مصادر/معادلات.  
الخطر: وردية مغلقة يدويا ووردية مغلقة تلقائيا قد تعرض ExpectedBalance بمنطق مختلف.  
الأسطر: `ShiftService.cs:819`, `ShiftCalculationHelper.cs:11`, `AutoCloseShiftBackgroundService.cs:134`, `AutoCloseShiftBackgroundService.cs:135`

### 9. Inventory value غير متسق بين تقرير المخزون وCOGS

المشكلة: تقرير المخزون يستخدم `AverageCost` فقط، بينما COGS يستخدم `AverageCost ?? Cost ?? 0`.  
السبب: fallback مختلف.  
الخطر: منتج له Cost وليس AverageCost يظهر بقيمة صفر في تقرير المخزون، لكن يظهر بتكلفة في COGS.  
الأسطر: `InventoryReportService.cs:72`, `InventoryReportService.cs:155`, `ProductReportService.cs:610`

### 10. `ProductBatch.Quantity` ومخزون الفرع يمكن أن ينفصلا

المشكلة: جرد batch لا يغير ProductBatch، والمرتجع إلى batch قد يرجع الكمية كلها إلى batch أساسي واحد إذا البيع استهلك أكثر من batch.  
السبب: `OrderItem.BatchId` يتم تحديثه بـ primary batch فقط.  
الخطر: مجموع batch quantities لا يساوي `BranchInventory.Quantity`.  
الأسطر: `StockTakingService.cs:339`, `InventoryService.cs:416`, `InventoryService.cs:425`, `InventoryService.cs:488`

### 11. تقارير wallet breakdown لا تعكس refund حقيقي

المشكلة: return orders لا تحتوي Payments، وwallet breakdown مبني على payments فقط.  
السبب: `RefundAsync` ينشئ return order totals وcash register refund، لكنه لا ينشئ Payment للمرتجع.  
الخطر: إجمالي المحفظة في التقارير قد لا يطابق صافي البيع بعد المرتجعات.  
الأسطر: `ReportService.cs:151`, `ReportService.cs:380`, `FinancialReportService.cs:143`, `OrderService.cs:1374`

### 12. Supplier totals denormalized وقد تنفصل عن فواتير الشراء

المشكلة: `Supplier.TotalDue`, `TotalPaid`, `TotalPurchases` تتحدث في confirm/payment/cancel/delete payment منفصلة عن الفواتير.  
السبب: قيم مخزنة وليست محسوبة دائما من الفواتير.  
الخطر: أي فشل جزئي أو مسار غير مغطى قد يسبب اختلاف المورد عن مجموع فواتيره.  
الأسطر: `PurchaseInvoiceService.cs:507`, `PurchaseInvoiceService.cs:768`, `PurchaseInvoiceService.cs:911`, `PurchaseInvoiceService.cs:998`

## الخلاصة التنفيذية

أخطر الأخطاء التي تحتاج إصلاحا قبل الاعتماد المالي:

1. خصم خامات الوصفة لا يستخدم `YieldQuantity`.
2. Refund لا يوزع خصم الطلب/الخدمة/التوصيل على البنود.
3. اختيار الباتش للبيع ليس FEFO.
4. رصيد المحفظة قد يزيد بمبلغ أكبر من Payment الفعلي ولا ينقص عند refund.
5. `Customer.TotalDue` لا يساوي جمع `Order.AmountDue` بعد سداد الديون.
6. الجرد قد يكتب رصيد قديم فوق حركات أحدث ولا يضبط ProductBatch.
7. `ExpectedBalance` و`TotalRefunds` في الوردية غير مستقرين كمصدر حقيقة.

لم يتم تعديل أي كود تطبيق.
