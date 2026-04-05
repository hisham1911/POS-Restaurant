namespace KasserPro.Domain.Entities;

using KasserPro.Domain.Common;

public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string Currency { get; set; } = "EGP";
    public string Timezone { get; set; } = "Africa/Cairo";
    public bool IsActive { get; set; } = true;

    // Tax Settings - Dynamic per Tenant
    /// <summary>
    /// نسبة الضريبة الافتراضية للشركة (مثال: 14 = 14%)
    /// </summary>
    public decimal TaxRate { get; set; } = 14.0m;

    /// <summary>
    /// هل الضريبة مفعلة؟ إذا كانت false، لا يتم احتساب ضريبة
    /// </summary>
    public bool IsTaxEnabled { get; set; } = true;

    // Inventory Settings
    /// <summary>
    /// هل يُسمح بالمخزون السالب؟ إذا كانت false، لن يتم السماح بالبيع عند نفاذ المخزون
    /// </summary>
    public bool AllowNegativeStock { get; set; } = false;

    // Receipt Settings - إعدادات تنسيق الفاتورة
    /// <summary>مقاس الورق: "80mm" أو "58mm" أو "custom"</summary>
    public string ReceiptPaperSize { get; set; } = "80mm";
    /// <summary>عرض الورق المخصص بالبيكسل (يستخدم فقط إذا كان ReceiptPaperSize = "custom")</summary>
    public int? ReceiptCustomWidth { get; set; }
    /// <summary>حجم خط العنوان (الهيدر)</summary>
    public int ReceiptHeaderFontSize { get; set; } = 12;
    /// <summary>حجم الخط العادي</summary>
    public int ReceiptBodyFontSize { get; set; } = 9;
    /// <summary>حجم خط الإجمالي</summary>
    public int ReceiptTotalFontSize { get; set; } = 11;
    /// <summary>إظهار اسم الفرع في الفاتورة</summary>
    public bool ReceiptShowBranchName { get; set; } = true;
    /// <summary>إظهار اسم الكاشير</summary>
    public bool ReceiptShowCashier { get; set; } = true;
    /// <summary>إظهار رسالة الشكر</summary>
    public bool ReceiptShowThankYou { get; set; } = true;
    /// <summary>رسالة الفوتر المخصصة</summary>
    public string? ReceiptFooterMessage { get; set; }
    /// <summary>رقم هاتف المتجر في الفاتورة</summary>
    public string? ReceiptPhoneNumber { get; set; }
    /// <summary>إظهار اسم العميل في الفاتورة</summary>
    public bool ReceiptShowCustomerName { get; set; } = true;
    /// <summary>إظهار لوجو الشركة في الفاتورة</summary>
    public bool ReceiptShowLogo { get; set; } = true;

    // Print Routing Settings - إعدادات الطباعة التلقائية
    /// <summary>وضع توجيه الطباعة: BranchOnly | BranchWithFallback | AllDevices | Disabled</summary>
    public string PrintRoutingMode { get; set; } = "BranchWithFallback";
    /// <summary>طباعة الفاتورة تلقائياً عند إتمام البيع</summary>
    public bool AutoPrintOnSale { get; set; } = true;
    /// <summary>طباعة إيصال سداد الدين تلقائياً</summary>
    public bool AutoPrintOnDebtPayment { get; set; } = true;
    /// <summary>طباعة التقرير اليومي تلقائياً</summary>
    public bool AutoPrintDailyReports { get; set; } = false;

    // Navigation
    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
