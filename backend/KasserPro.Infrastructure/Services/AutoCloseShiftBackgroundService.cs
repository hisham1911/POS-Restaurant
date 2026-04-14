namespace KasserPro.Infrastructure.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using KasserPro.Infrastructure.Data;
using KasserPro.Domain.Enums;
using KasserPro.Application.Services.Interfaces;

/// <summary>
/// Background service that automatically closes shifts that have been open for more than 12 hours.
/// Runs every hour to check for shifts that need to be auto-closed.
/// </summary>
public class AutoCloseShiftBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutoCloseShiftBackgroundService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1); // Check every hour

    public AutoCloseShiftBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AutoCloseShiftBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Auto-Close Shift Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AutoCloseOldShiftsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while auto-closing shifts");
            }

            // Wait for the next check interval
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Auto-Close Shift Background Service stopped");
    }

    private async Task AutoCloseOldShiftsAsync(CancellationToken cancellationToken)
    {
        // Check if auto-close is enabled
        var isEnabled = _configuration.GetValue<bool>("ShiftAutoClose:Enabled", true);
        if (!isEnabled)
        {
            _logger.LogDebug("Auto-close is disabled in configuration");
            return;
        }

        var hoursThreshold = _configuration.GetValue<int>("ShiftAutoClose:HoursThreshold", 12);
        var cutoffTime = DateTime.UtcNow.AddHours(-hoursThreshold);

        _logger.LogInformation("Checking for shifts opened before {CutoffTime} (threshold: {Hours} hours)", 
            cutoffTime, hoursThreshold);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Find all open shifts that have been open for more than the threshold
        var shiftsToClose = await context.Shifts
            .Include(s => s.User)
            .Include(s => s.Orders)
                .ThenInclude(o => o.Payments)
            .Where(s => !s.IsClosed && s.OpenedAt <= cutoffTime)
            .ToListAsync(cancellationToken);

        if (shiftsToClose.Count == 0)
        {
            _logger.LogDebug("No shifts found that need auto-closing");
            return;
        }

        _logger.LogInformation("Found {Count} shift(s) to auto-close", shiftsToClose.Count);

        foreach (var shift in shiftsToClose)
        {
            try
            {
                var hoursOpen = (DateTime.UtcNow - shift.OpenedAt).TotalHours;
                
                _logger.LogInformation(
                    "Auto-closing shift {ShiftId} for user {UserName} (Tenant: {TenantId}, Branch: {BranchId}) - Open for {Hours:F1} hours",
                    shift.Id, shift.User?.Name ?? "Unknown", shift.TenantId, shift.BranchId, hoursOpen);

                // Calculate totals from completed orders
                var completedOrders = shift.Orders
                    .Where(o => o.Status == OrderStatus.Completed)
                    .ToList();

                var allPayments = completedOrders
                    .SelectMany(o => o.Payments ?? Enumerable.Empty<Domain.Entities.Payment>())
                    .ToList();

                var totalCash = Math.Round(
                    allPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount), 2);
                var totalCard = Math.Round(
                    allPayments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount), 2);

                // Set closing values
                shift.ClosingBalance = shift.OpeningBalance + totalCash;
                shift.ExpectedBalance = shift.OpeningBalance + totalCash;
                shift.Difference = 0; // No difference since we're using expected balance
                shift.TotalCash = totalCash;
                shift.TotalCard = totalCard;
                shift.TotalOrders = completedOrders.Count;
                shift.ClosedAt = DateTime.UtcNow;
                shift.IsClosed = true;
                shift.IsForceClosed = true;
                shift.ForceClosedByUserId = null; // System auto-close
                shift.ForceClosedByUserName = "النظام";
                shift.ForceClosedAt = DateTime.UtcNow;
                shift.ForceCloseReason = $"إغلاق تلقائي - تجاوزت {hoursThreshold} ساعة";
                shift.Notes = string.IsNullOrEmpty(shift.Notes)
                    ? shift.ForceCloseReason
                    : $"{shift.Notes}\n{shift.ForceCloseReason}";

                context.Shifts.Update(shift);
                await context.SaveChangesAsync(cancellationToken);

                // P3: Record ShiftClose transaction in cash register
                try
                {
                    var transactionNumber = await GenerateTransactionNumberAsync(context, shift.TenantId, shift.BranchId);
                    
                    var cashTransaction = new Domain.Entities.CashRegisterTransaction
                    {
                        TenantId = shift.TenantId,
                        BranchId = shift.BranchId,
                        TransactionNumber = transactionNumber,
                        Type = CashRegisterTransactionType.ShiftClose,
                        Amount = shift.ClosingBalance,
                        BalanceBefore = shift.OpeningBalance,
                        BalanceAfter = shift.ClosingBalance,
                        TransactionDate = DateTime.UtcNow,
                        Description = "إغلاق تلقائي للوردية",
                        ReferenceType = "Shift",
                        ReferenceId = shift.Id,
                        ShiftId = shift.Id,
                        UserId = shift.UserId,
                        UserName = shift.User?.Name ?? "Unknown"
                    };

                    context.CashRegisterTransactions.Add(cashTransaction);
                    await context.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation(
                        "Recorded ShiftClose cash register transaction for shift {ShiftId} - Amount: {Amount}",
                        shift.Id, shift.ClosingBalance);
                }
                catch (Exception cashRegEx)
                {
                    _logger.LogError(cashRegEx, 
                        "Failed to record cash register transaction for auto-closed shift {ShiftId}", 
                        shift.Id);
                    // Don't fail the entire auto-close if cash register transaction fails
                }

                _logger.LogInformation(
                    "Successfully auto-closed shift {ShiftId} - Total Cash: {TotalCash}, Total Card: {TotalCard}, Orders: {TotalOrders}",
                    shift.Id, totalCash, totalCard, completedOrders.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-close shift {ShiftId}", shift.Id);
            }
        }

        _logger.LogInformation("Auto-close process completed. Closed {Count} shift(s)", shiftsToClose.Count);
    }

    /// <summary>
    /// P3: Generate transaction number for cash register transaction
    /// </summary>
    private async Task<string> GenerateTransactionNumberAsync(AppDbContext context, int tenantId, int branchId)
    {
        var today = DateTime.UtcNow.Date;
        var todayTransactionCount = await context.CashRegisterTransactions
            .Where(t => t.TenantId == tenantId && t.BranchId == branchId && t.CreatedAt >= today)
            .CountAsync();

        var entropy = Guid.NewGuid().ToString("N")[..8];
        return $"CR-{branchId:D3}-{DateTime.UtcNow:yyyyMMdd}-{(todayTransactionCount + 1):D4}-{entropy}";
    }
}
