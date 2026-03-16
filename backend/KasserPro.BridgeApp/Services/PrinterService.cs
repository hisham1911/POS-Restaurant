using System.Drawing.Printing;
using System.Drawing;
using System.Linq;
using System.Text;
using ESCPOS_NET;
using ESCPOS_NET.Emitters;
using KasserPro.BridgeApp.Models;
using Serilog;

namespace KasserPro.BridgeApp.Services;

/// <summary>
/// Manages thermal printer operations using ESC/POS commands
/// </summary>
public class PrinterService : IPrinterService
{
    private readonly ISettingsManager _settingsManager;

    public PrinterService(ISettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
    }

    /// <summary>
    /// Initializes printer service and logs available printers
    /// </summary>
    public async Task InitializeAsync()
    {
        Log.Information("Initializing printer service");
        var printers = await GetAvailablePrintersAsync();
        Log.Information("Found {Count} printers: {Printers}", printers.Count, string.Join(", ", printers));
    }

    /// <summary>
    /// Gets all installed printers from Windows
    /// </summary>
    public Task<List<string>> GetAvailablePrintersAsync()
    {
        var printers = new List<string>();
        foreach (string printerName in PrinterSettings.InstalledPrinters)
        {
            printers.Add(printerName);
        }
        return Task.FromResult(printers);
    }

    /// <summary>
    /// Prints a receipt on the configured thermal printer
    /// </summary>
    public async Task<bool> PrintReceiptAsync(PrintCommandDto command)
    {
        try
        {
            var settings = await _settingsManager.GetSettingsAsync();
            var printerName = settings.DefaultPrinterName;

            if (string.IsNullOrEmpty(printerName))
            {
                Log.Error("No default printer configured");
                return false;
            }

            Log.Information("Printing receipt {ReceiptNumber} on printer {Printer}",
                command.Receipt.ReceiptNumber, printerName);

            // Use PrintDocument (Windows Print API) for all printers
            // This ensures proper Arabic text rendering
            await PrintUsingPrintDocumentAsync(printerName, command.Receipt, command.Settings);

            Log.Information("Receipt {ReceiptNumber} printed successfully", command.Receipt.ReceiptNumber);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print receipt {ReceiptNumber}", command.Receipt.ReceiptNumber);
            return false;
        }
    }

    /// <summary>
    /// Checks if the printer is a PDF printer
    /// </summary>
    private bool IsPdfPrinter(string printerName)
    {
        var pdfPrinters = new[] { "pdf", "xps", "onenote", "fax" };
        return pdfPrinters.Any(p => printerName.ToLower().Contains(p));
    }

    /// <summary>
    /// Prints receipt using PrintDocument (Windows Print API)
    /// Uses settings from server (tenant configuration) instead of local settings
    /// </summary>
    private Task PrintUsingPrintDocumentAsync(string printerName, ReceiptDto receipt, ReceiptPrintSettings rs)
    {
        return Task.Run(() =>
        {
            var printDoc = new PrintDocument();
            printDoc.PrinterSettings.PrinterName = printerName;

            // Set default document name (used when printing to PDF)
            printDoc.DocumentName = receipt.ReceiptNumber;

            // Also set PrintFileName for better compatibility with Print to PDF
            if (printerName.Contains("PDF", StringComparison.OrdinalIgnoreCase) ||
                printerName.Contains("XPS", StringComparison.OrdinalIgnoreCase))
            {
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                printDoc.PrinterSettings.PrintFileName = System.IO.Path.Combine(documentsPath, $"{receipt.ReceiptNumber}.pdf");
            }

            // Set paper size based on server settings
            float paperWidth = rs.PaperSize switch
            {
                "58mm" => 219,
                "custom" => rs.CustomWidth ?? 280,
                _ => 302  // 80mm
            };
            printDoc.DefaultPageSettings.PaperSize = new PaperSize("Receipt", (int)paperWidth, 1200);

            printDoc.PrintPage += (sender, e) =>
            {
                if (e.Graphics == null) return;

                var graphics = e.Graphics;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                float margin = 4;
                float W = paperWidth - (margin * 2);
                float y = 4;

                // Fonts from server settings
                var font = new Font("Arial", rs.BodyFontSize, FontStyle.Regular);
                var fontBold = new Font("Arial", rs.BodyFontSize, FontStyle.Bold);
                var fontHeader = new Font("Arial", rs.HeaderFontSize, FontStyle.Bold);
                var fontTotal = new Font("Arial", rs.TotalFontSize, FontStyle.Bold);
                var fontSmall = new Font("Arial", Math.Max(rs.BodyFontSize - 1, 7), FontStyle.Regular);

                float lineH = font.GetHeight(graphics) + 4;

                // RTL format
                var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
                var sfRight = new StringFormat(StringFormatFlags.DirectionRightToLeft) { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };
                var sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };

                // Dashed line pen
                var dashPen = new Pen(System.Drawing.Color.Black, 0.8f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };

                // Helper: draw text centered
                void DrawCenter(string text, Font f)
                {
                    graphics.DrawString(text, f, Brushes.Black, new RectangleF(margin, y, W, f.GetHeight(graphics) + 4), sfCenter);
                    y += f.GetHeight(graphics) + 4;
                }

                // Helper: draw two texts on same line (right and left)
                void DrawRow(string right, string left, Font f)
                {
                    var h = f.GetHeight(graphics) + 4;
                    graphics.DrawString(right, f, Brushes.Black, new RectangleF(margin, y, W, h), sfRight);
                    graphics.DrawString(left, f, Brushes.Black, new RectangleF(margin, y, W, h), sfLeft);
                    y += h;
                }

                // Helper: draw dashed line
                void DrawDash()
                {
                    y += 2;
                    graphics.DrawLine(dashPen, margin, y, margin + W, y);
                    y += 3;
                }

                // ========== 1. LOGO (if enabled and URL exists) ==========
                if (rs.ShowLogo && !string.IsNullOrEmpty(rs.LogoUrl))
                {
                    try
                    {
                        using var client = new System.Net.Http.HttpClient();
                        client.Timeout = TimeSpan.FromSeconds(5);
                        var imageBytes = client.GetByteArrayAsync(rs.LogoUrl).Result;
                        using var ms = new System.IO.MemoryStream(imageBytes);
                        using var logo = System.Drawing.Image.FromStream(ms);

                        // Scale logo to fit receipt width (max 80px height)
                        float maxLogoHeight = 60;
                        float logoRatio = (float)logo.Width / logo.Height;
                        float logoHeight = Math.Min(maxLogoHeight, logo.Height);
                        float logoWidth = logoHeight * logoRatio;
                        if (logoWidth > W * 0.6f)
                        {
                            logoWidth = W * 0.6f;
                            logoHeight = logoWidth / logoRatio;
                        }

                        float logoX = margin + (W - logoWidth) / 2;
                        graphics.DrawImage(logo, logoX, y, logoWidth, logoHeight);
                        y += logoHeight + 6;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("Failed to load logo: {Error}", ex.Message);
                    }
                }

                // ========== 2. HEADER ==========
                bool isDailyReport = receipt.ReceiptNumber.Contains("تقرير يومي");

                if (rs.ShowBranchName)
                {
                    DrawCenter(receipt.BranchName, fontHeader);
                    y += 2;
                }

                if (isDailyReport)
                {
                    DrawCenter(receipt.ReceiptNumber, fontBold);
                    // No time line for daily reports
                }
                else
                {
                    DrawRow("فاتورة رقم", receipt.ReceiptNumber, font);
                    DrawCenter(receipt.Date.ToString("dd/MM/yyyy  hh:mm tt"), font);
                }

                // ========== REFUND INDICATOR ==========
                if (receipt.IsRefund)
                {
                    y += 4;
                    DrawCenter("*** فاتورة ارجاع ***", fontHeader);
                    y += 2;
                }

                DrawDash();

                // ========== 3. CASHIER, CUSTOMER & PAYMENT ==========
                if (!string.IsNullOrEmpty(receipt.PaymentMethod) || !string.IsNullOrEmpty(receipt.CashierName))
                {
                    var paymentAr = TranslatePaymentMethod(receipt.PaymentMethod);
                    if (rs.ShowCashier)
                    {
                        DrawRow($"الكاشير: {receipt.CashierName}", $"الدفع: {paymentAr}", font);
                    }
                    else
                    {
                        DrawRow($"الدفع: {paymentAr}", "", font);
                    }
                }
                if (rs.ShowCustomerName && !string.IsNullOrEmpty(receipt.CustomerName))
                {
                    DrawRow($"العميل: {receipt.CustomerName}", "", font);
                }

                DrawDash();

                // ========== 4. ITEMS ==========
                foreach (var item in receipt.Items)
                {
                    if (isDailyReport)
                    {
                        // Smart rendering for daily report items
                        if (item.Quantity == -1)
                        {
                            // Section header
                            DrawDash();
                            DrawCenter(item.Name, fontBold);
                        }
                        else if (item.Quantity == 0 && item.TotalPrice == 0)
                        {
                            // Empty divider – skip
                        }
                        else if (item.Quantity == 0 && item.TotalPrice != 0)
                        {
                            if (item.TotalPrice < 0)
                            {
                                // Deduction row: show with "-" prefix
                                DrawRow(item.Name, $"- {Math.Abs(item.TotalPrice):F2} ج.م", font);
                            }
                            else
                            {
                                // Value row: Name ......... Price
                                DrawRow(item.Name, $"{item.TotalPrice:F2} ج.م", font);
                            }
                        }
                        else if (item.Quantity > 0 && item.TotalPrice == 0)
                        {
                            // Count row: Name ......... N
                            DrawRow(item.Name, $"{item.Quantity}", font);
                        }
                        else
                        {
                            // Normal product: Name × Qty | Price
                            DrawRow($"{item.Name} × {item.Quantity:F0}", $"{item.TotalPrice:F2} ج.م", font);
                        }
                    }
                    else
                    {
                        // Regular receipt: standard format
                        var displayQuantity = receipt.IsRefund ? Math.Abs(item.Quantity) : item.Quantity;
                        var displayPrice = receipt.IsRefund ? Math.Abs(item.TotalPrice) : item.TotalPrice;
                        DrawRow($"{item.Name} × {displayQuantity:F0}", $"{displayPrice:F0} ج.م", font);
                    }
                }

                DrawDash();

                // ========== 5. TOTALS ==========
                // Check if this is a debt payment receipt
                bool isDebtPayment = receipt.ReceiptNumber.StartsWith("PAY-");

                if (isDebtPayment)
                {
                    // Debt payment: Show only 3 fields
                    DrawRow("المبلغ الكلي", $"{receipt.NetTotal:F2} ج.م", fontBold);
                    DrawRow("المدفوع", $"{receipt.AmountPaid:F2} ج.م", fontBold);
                    DrawRow("المتبقي", $"{receipt.AmountDue:F2} ج.م", fontBold);
                }
                else if (isDailyReport)
                {
                    // Show just the grand total for daily reports
                    DrawRow("الإجمالي", $"{receipt.TotalAmount:F2} ج.م", fontTotal);
                }
                else
                {
                    // For refunds, show absolute values (positive numbers)
                    var displayNetTotal = receipt.IsRefund ? Math.Abs(receipt.NetTotal) : receipt.NetTotal;
                    var displayTaxAmount = receipt.IsRefund ? Math.Abs(receipt.TaxAmount) : receipt.TaxAmount;
                    var displayTotalAmount = receipt.IsRefund ? Math.Abs(receipt.TotalAmount) : receipt.TotalAmount;

                    DrawRow("المجموع", $"{displayNetTotal:F2} ج.م", font);

                    if (Math.Abs(receipt.TaxAmount) > 0 && rs.IsTaxEnabled)
                    {
                        DrawRow($"الضريبة ({rs.TaxRate:F0}%)", $"{displayTaxAmount:F2} ج.م", font);
                    }

                    var discount = receipt.NetTotal - receipt.TotalAmount + receipt.TaxAmount;
                    if (Math.Abs(discount) > 0)
                    {
                        var displayDiscount = Math.Abs(discount);
                        DrawRow("الخصم", $"-{displayDiscount:F2} ج.م", font);
                    }

                    DrawDash();

                    // Total - bold and bigger
                    DrawRow("الإجمالي", $"{displayTotalAmount:F2} ج.م", fontTotal);
                }

                // ========== 6. PAYMENT AMOUNTS ==========
                // Amount paid and change/due (skip for debt payments - already shown above)
                if (!isDebtPayment && receipt.AmountPaid > 0)
                {
                    DrawDash();
                    DrawRow("المبلغ المدفوع", $"{receipt.AmountPaid:F2} ج.م", font);
                    if (receipt.ChangeAmount > 0)
                    {
                        DrawRow("الباقي", $"{receipt.ChangeAmount:F2} ج.م", font);
                    }
                    if (receipt.AmountDue > 0)
                    {
                        DrawRow("المتبقي على العميل", $"{receipt.AmountDue:F2} ج.م", fontBold);
                    }
                }

                // ========== 7. FOOTER ==========
                y += 2;
                if (rs.ShowThankYou)
                {
                    DrawCenter("شكراً لزيارتكم ✨", fontBold);
                }

                if (!string.IsNullOrEmpty(rs.FooterMessage))
                {
                    DrawCenter(rs.FooterMessage, fontSmall);
                }

                if (!string.IsNullOrEmpty(rs.PhoneNumber))
                {
                    DrawCenter(rs.PhoneNumber, fontSmall);
                }

                // Cleanup
                font.Dispose();
                fontBold.Dispose();
                fontHeader.Dispose();
                fontTotal.Dispose();
                fontSmall.Dispose();
                dashPen.Dispose();
            };

            printDoc.Print();
        });
    }

    /// <summary>
    /// Generates ESC/POS byte sequence for a receipt
    /// </summary>
    private byte[] GenerateDebtPaymentReceiptEscPos(ReceiptDto receipt)
    {
        var commands = new List<byte[]>();
        var arabicEncoding = Encoding.GetEncoding(1256);

        // Initialize printer
        commands.Add(new byte[] { 0x1B, 0x40 }); // ESC @ - Initialize

        // Center align
        commands.Add(new byte[] { 0x1B, 0x61, 0x01 }); // ESC a 1

        // Header
        commands.Add(new byte[] { 0x1B, 0x21, 0x38 }); // Double size + bold
        commands.Add(arabicEncoding.GetBytes("مجزر الأمانة\n"));
        commands.Add(new byte[] { 0x1B, 0x21, 0x00 }); // Reset
        commands.Add(arabicEncoding.GetBytes("\n"));
        commands.Add(arabicEncoding.GetBytes("================================\n"));
        commands.Add(arabicEncoding.GetBytes("\n"));

        // Receipt info - left align
        commands.Add(new byte[] { 0x1B, 0x61, 0x00 }); // Left align

        commands.Add(new byte[] { 0x1B, 0x21, 0x08 }); // Bold
        commands.Add(arabicEncoding.GetBytes($"فاتورة رقم: {receipt.ReceiptNumber}\n"));
        commands.Add(new byte[] { 0x1B, 0x21, 0x00 }); // Reset

        commands.Add(arabicEncoding.GetBytes($"التاريخ: {receipt.Date:dd/MM/yyyy HH:mm}\n"));
        commands.Add(arabicEncoding.GetBytes($"الكاشير: {receipt.CashierName}\n"));
        commands.Add(arabicEncoding.GetBytes($"العميل: {receipt.CustomerName}\n"));
        commands.Add(arabicEncoding.GetBytes($"الدفع: {TranslatePaymentMethod(receipt.PaymentMethod)}\n"));
        commands.Add(arabicEncoding.GetBytes("\n"));
        commands.Add(arabicEncoding.GetBytes("================================\n"));
        commands.Add(arabicEncoding.GetBytes("\n"));

        // 3 Fields only: المبلغ الكلي - المدفوع - المتبقي
        commands.Add(new byte[] { 0x1B, 0x21, 0x08 }); // Bold
        commands.Add(arabicEncoding.GetBytes(FormatLineArabic("المبلغ الكلي", $"{receipt.NetTotal:F2} ج.م", 32) + "\n"));
        commands.Add(arabicEncoding.GetBytes(FormatLineArabic("المدفوع", $"{receipt.AmountPaid:F2} ج.م", 32) + "\n"));
        commands.Add(arabicEncoding.GetBytes(FormatLineArabic("المتبقي", $"{receipt.AmountDue:F2} ج.م", 32) + "\n"));
        commands.Add(new byte[] { 0x1B, 0x21, 0x00 }); // Reset

        commands.Add(arabicEncoding.GetBytes("\n"));
        commands.Add(arabicEncoding.GetBytes("================================\n"));
        commands.Add(arabicEncoding.GetBytes("\n"));

        // Footer
        commands.Add(new byte[] { 0x1B, 0x61, 0x01 }); // Center align
        commands.Add(new byte[] { 0x1B, 0x21, 0x08 }); // Bold
        commands.Add(arabicEncoding.GetBytes("شكراً لزيارتكم\n"));
        commands.Add(new byte[] { 0x1B, 0x21, 0x00 }); // Reset
        commands.Add(arabicEncoding.GetBytes("\n"));

        // Feed and cut
        commands.Add(new byte[] { 0x1B, 0x64, 0x03 }); // ESC d 3 - Feed 3 lines
        commands.Add(new byte[] { 0x1D, 0x56, 0x00 }); // GS V 0 - Full cut

        return commands.SelectMany(x => x).ToArray();
    }

    /// <summary>
    /// Generates ESC/POS byte sequence for a receipt
    /// </summary>
    private byte[] GenerateReceiptEscPos(ReceiptDto receipt)
    {
        // Check if this is a debt payment receipt (PAY-X format)
        if (receipt.ReceiptNumber.StartsWith("PAY-"))
        {
            return GenerateDebtPaymentReceiptEscPos(receipt);
        }

        var commands = new List<byte[]>();

        // Use Windows-1256 encoding for Arabic support
        var arabicEncoding = Encoding.GetEncoding(1256);

        // Initialize printer
        commands.Add(new byte[] { 0x1B, 0x40 }); // ESC @ - Initialize

        // ============ HEADER ============
        // Center align
        commands.Add(new byte[] { 0x1B, 0x61, 0x01 }); // ESC a 1

        // Branch name - double size, bold
        commands.Add(new byte[] { 0x1B, 0x21, 0x38 }); // ESC ! 56 (double width + double height + bold)
        commands.Add(arabicEncoding.GetBytes(receipt.BranchName + "\n"));
        commands.Add(new byte[] { 0x1B, 0x21, 0x00 }); // Reset
        commands.Add(arabicEncoding.GetBytes("\n"));
        commands.Add(arabicEncoding.GetBytes("================================\n"));
        commands.Add(arabicEncoding.GetBytes("\n"));

        // Receipt info - left align
        commands.Add(new byte[] { 0x1B, 0x61, 0x00 }); // ESC a 0 (left)

        commands.Add(new byte[] { 0x1B, 0x21, 0x08 }); // Bold
        commands.Add(arabicEncoding.GetBytes($"فاتورة رقم: {receipt.ReceiptNumber}\n"));
        commands.Add(new byte[] { 0x1B, 0x21, 0x00 }); // Reset

        commands.Add(arabicEncoding.GetBytes($"التاريخ: {receipt.Date:dd/MM/yyyy}\n"));
        commands.Add(arabicEncoding.GetBytes($"الوقت: {receipt.Date:HH:mm}\n"));
        commands.Add(arabicEncoding.GetBytes($"الكاشير: {receipt.CashierName}\n"));
        commands.Add(arabicEncoding.GetBytes("\n"));
        commands.Add(arabicEncoding.GetBytes("================================\n"));
        commands.Add(arabicEncoding.GetBytes("\n"));

        // ============ ITEMS ============
        commands.Add(new byte[] { 0x1B, 0x21, 0x08 }); // Bold
        commands.Add(arabicEncoding.GetBytes("المنتجات:\n"));
        commands.Add(new byte[] { 0x1B, 0x21, 0x00 }); // Reset
        commands.Add(arabicEncoding.GetBytes("--------------------------------\n"));

        foreach (var item in receipt.Items)
        {
            // Item name - bold
            commands.Add(new byte[] { 0x1B, 0x21, 0x08 }); // Bold
            commands.Add(arabicEncoding.GetBytes(TruncateText(item.Name, 32) + "\n"));
            commands.Add(new byte[] { 0x1B, 0x21, 0x00 }); // Reset

            // Quantity
            commands.Add(arabicEncoding.GetBytes($"  الكمية: {item.Quantity}\n"));

            // Price
            commands.Add(arabicEncoding.GetBytes($"  السعر: {item.UnitPrice:F2} ج.م\n"));

            // Total
            commands.Add(new byte[] { 0x1B, 0x21, 0x08 }); // Bold
            commands.Add(arabicEncoding.GetBytes($"  الاجمالي: {item.TotalPrice:F2} ج.م\n"));
            commands.Add(new byte[] { 0x1B, 0x21, 0x00 }); // Reset
            commands.Add(arabicEncoding.GetBytes("\n"));
        }

        commands.Add(arabicEncoding.GetBytes("================================\n"));
        commands.Add(arabicEncoding.GetBytes("\n"));

        // ============ TOTALS ============
        commands.Add(new byte[] { 0x1B, 0x21, 0x08 }); // Bold
        commands.Add(arabicEncoding.GetBytes("الملخص المالي:\n"));
        commands.Add(new byte[] { 0x1B, 0x21, 0x00 }); // Reset
        commands.Add(arabicEncoding.GetBytes("--------------------------------\n"));

        // Subtotal
        commands.Add(arabicEncoding.GetBytes(FormatLineArabic("المجموع الفرعي:", $"{receipt.NetTotal:F2} ج.م", 32) + "\n"));

        // Tax
        commands.Add(arabicEncoding.GetBytes(FormatLineArabic("الضريبة (14%):", $"{receipt.TaxAmount:F2} ج.م", 32) + "\n"));

        commands.Add(arabicEncoding.GetBytes("--------------------------------\n"));

        // Total - large and bold
        commands.Add(new byte[] { 0x1B, 0x21, 0x18 }); // Double height + bold
        commands.Add(arabicEncoding.GetBytes(FormatLineArabic("الاجمالي:", $"{receipt.TotalAmount:F2} ج.م", 32) + "\n"));
        commands.Add(new byte[] { 0x1B, 0x21, 0x00 }); // Reset

        commands.Add(arabicEncoding.GetBytes("================================\n"));
        commands.Add(arabicEncoding.GetBytes("\n"));

        // ============ PAYMENT ============
        commands.Add(new byte[] { 0x1B, 0x21, 0x08 }); // Bold
        commands.Add(arabicEncoding.GetBytes($"طريقة الدفع: {TranslatePaymentMethod(receipt.PaymentMethod)}\n"));
        commands.Add(new byte[] { 0x1B, 0x21, 0x00 }); // Reset
        commands.Add(arabicEncoding.GetBytes("\n"));
        commands.Add(arabicEncoding.GetBytes("================================\n"));
        commands.Add(arabicEncoding.GetBytes("\n"));

        // ============ FOOTER ============
        // Center align
        commands.Add(new byte[] { 0x1B, 0x61, 0x01 }); // ESC a 1

        commands.Add(new byte[] { 0x1B, 0x21, 0x08 }); // Bold
        commands.Add(arabicEncoding.GetBytes("شكراً لزيارتكم\n"));
        commands.Add(arabicEncoding.GetBytes("THANK YOU!\n"));
        commands.Add(new byte[] { 0x1B, 0x21, 0x00 }); // Reset
        commands.Add(arabicEncoding.GetBytes("\n"));

        // Barcode
        try
        {
            // Barcode - CODE128
            commands.Add(new byte[] { 0x1D, 0x6B, 0x49 }); // GS k 73 (CODE128)
            var barcodeData = Encoding.ASCII.GetBytes(receipt.ReceiptNumber);
            commands.Add(new byte[] { (byte)barcodeData.Length });
            commands.Add(barcodeData);
            commands.Add(arabicEncoding.GetBytes("\n"));
        }
        catch
        {
            commands.Add(arabicEncoding.GetBytes($"*{receipt.ReceiptNumber}*\n"));
        }

        // Feed and cut
        commands.Add(new byte[] { 0x1B, 0x64, 0x03 }); // ESC d 3 - Feed 3 lines
        commands.Add(new byte[] { 0x1D, 0x56, 0x00 }); // GS V 0 - Full cut

        return commands.SelectMany(x => x).ToArray();
    }

    /// <summary>
    /// Formats a line with Arabic text - right to left
    /// </summary>
    private string FormatLineArabic(string label, string value, int totalWidth)
    {
        var spaces = totalWidth - label.Length - value.Length;
        if (spaces < 1) spaces = 1;
        return label + new string(' ', spaces) + value;
    }

    /// <summary>
    /// Translates payment method to Arabic
    /// </summary>
    private string TranslatePaymentMethod(string method)
    {
        return method.ToLower() switch
        {
            "cash" => "كاش",
            "card" => "فيزا",
            "fawry" => "فوري",
            _ => method
        };
    }

    /// <summary>
    /// Truncates text if too long
    /// </summary>
    private string TruncateText(string text, int maxLength)
    {
        if (text.Length <= maxLength)
            return text;
        return text.Substring(0, maxLength - 3) + "...";
    }

    /// <summary>
    /// Sends raw ESC/POS bytes to Windows printer
    /// </summary>
    private Task SendToPrinterAsync(string printerName, byte[] data)
    {
        return Task.Run(() =>
        {
            // Use Windows raw printer API
            var printerHandle = IntPtr.Zero;
            try
            {
                // Open printer
                if (!NativeMethods.OpenPrinter(printerName, out printerHandle, IntPtr.Zero))
                {
                    throw new Exception($"Failed to open printer: {printerName}");
                }

                // Start document
                var docInfo = new NativeMethods.DOC_INFO_1
                {
                    pDocName = "KasserPro Receipt",
                    pOutputFile = null,
                    pDataType = "RAW"
                };

                if (!NativeMethods.StartDocPrinter(printerHandle, 1, ref docInfo))
                {
                    throw new Exception("Failed to start document");
                }

                // Start page
                if (!NativeMethods.StartPagePrinter(printerHandle))
                {
                    throw new Exception("Failed to start page");
                }

                // Write data
                int bytesWritten = 0;
                if (!NativeMethods.WritePrinter(printerHandle, data, data.Length, out bytesWritten))
                {
                    throw new Exception("Failed to write to printer");
                }

                // End page and document
                NativeMethods.EndPagePrinter(printerHandle);
                NativeMethods.EndDocPrinter(printerHandle);
            }
            finally
            {
                if (printerHandle != IntPtr.Zero)
                {
                    NativeMethods.ClosePrinter(printerHandle);
                }
            }
        });
    }
}

/// <summary>
/// Native Windows printing API methods
/// </summary>
internal static class NativeMethods
{
    [System.Runtime.InteropServices.DllImport("winspool.drv", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
    public static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [System.Runtime.InteropServices.DllImport("winspool.drv", SetLastError = true)]
    public static extern bool ClosePrinter(IntPtr hPrinter);

    [System.Runtime.InteropServices.DllImport("winspool.drv", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
    public static extern bool StartDocPrinter(IntPtr hPrinter, int level, ref DOC_INFO_1 pDocInfo);

    [System.Runtime.InteropServices.DllImport("winspool.drv", SetLastError = true)]
    public static extern bool EndDocPrinter(IntPtr hPrinter);

    [System.Runtime.InteropServices.DllImport("winspool.drv", SetLastError = true)]
    public static extern bool StartPagePrinter(IntPtr hPrinter);

    [System.Runtime.InteropServices.DllImport("winspool.drv", SetLastError = true)]
    public static extern bool EndPagePrinter(IntPtr hPrinter);

    [System.Runtime.InteropServices.DllImport("winspool.drv", SetLastError = true)]
    public static extern bool WritePrinter(IntPtr hPrinter, byte[] pBytes, int dwCount, out int dwWritten);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    public struct DOC_INFO_1
    {
        public string pDocName;
        public string? pOutputFile;
        public string pDataType;
    }
}
