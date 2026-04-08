using System.Windows;
using System.Windows.Media;
using KasserPro.BridgeApp.Models;
using KasserPro.BridgeApp.Services;
using Serilog;

namespace KasserPro.BridgeApp.Views;

/// <summary>
/// Settings window for configuring application
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly ISettingsManager _settingsManager;
    private readonly IPrinterService _printerService;
    private readonly ISignalRClientService _signalRClient;
    private AppSettings? _currentSettings;

    public SettingsWindow(
        ISettingsManager settingsManager,
        IPrinterService printerService,
        ISignalRClientService signalRClient)
    {
        InitializeComponent();

        _settingsManager = settingsManager;
        _printerService = printerService;
        _signalRClient = signalRClient;

        Loaded += SettingsWindow_Loaded;

        // Subscribe to connection state changes
        _signalRClient.OnConnectionStateChanged += OnConnectionStateChanged;
    }

    private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Load current settings
            _currentSettings = await _settingsManager.GetSettingsAsync();

            BackendUrlTextBox.Text = _currentSettings.BackendUrl;
            ApiKeyTextBox.Text = _currentSettings.ApiKey;
            DeviceIdTextBox.Text = _currentSettings.DeviceId;

            // Load available printers
            await LoadPrintersAsync();

            // Set current printer
            if (!string.IsNullOrEmpty(_currentSettings.DefaultPrinterName))
            {
                PrinterComboBox.SelectedItem = _currentSettings.DefaultPrinterName;
            }

            // Update connection status
            UpdateConnectionStatus(_signalRClient.IsConnected);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load settings");
            MessageBox.Show(
                $"Failed to load settings: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private async Task LoadPrintersAsync()
    {
        try
        {
            var printers = await _printerService.GetAvailablePrintersAsync();
            PrinterComboBox.Items.Clear();

            foreach (var printer in printers)
            {
                PrinterComboBox.Items.Add(printer);
            }

            if (printers.Count == 0)
            {
                MessageBox.Show(
                    "No printers found on this system",
                    "Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load printers");
            MessageBox.Show(
                $"Failed to load printers: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private async void RefreshPrintersButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadPrintersAsync();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(BackendUrlTextBox.Text))
            {
                MessageBox.Show(
                    "عنوان الخادم مطلوب\n\nBackend URL is required",
                    "خطأ في التحقق - Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            // Validate URL format
            if (!Uri.TryCreate(BackendUrlTextBox.Text, UriKind.Absolute, out _))
            {
                MessageBox.Show(
                    "صيغة عنوان الخادم غير صحيحة\n\nInvalid Backend URL format",
                    "خطأ في التحقق - Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            var apiKey = ApiKeyTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = !string.IsNullOrWhiteSpace(_currentSettings?.ApiKey)
                    ? _currentSettings.ApiKey
                    : Guid.NewGuid().ToString("N");

                ApiKeyTextBox.Text = apiKey;
            }

            // Update settings
            _currentSettings!.BackendUrl = BackendUrlTextBox.Text.TrimEnd('/');
            _currentSettings.ApiKey = apiKey;
            _currentSettings.DefaultPrinterName = PrinterComboBox.SelectedItem?.ToString() ?? "";

            // Save settings (only connection + printer)
            await _settingsManager.SaveSettingsAsync(_currentSettings);

            MessageBox.Show(
                "تم حفظ الإعدادات بنجاح!\n\nجاري إعادة الاتصال بالخادم...\n\nSettings saved successfully!\n\nReconnecting to backend...",
                "نجح - Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );

            // Reconnect with new settings
            await _signalRClient.DisconnectAsync();
            await Task.Delay(500);
            await _signalRClient.ConnectAsync();

            Close();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings");
            MessageBox.Show(
                $"Failed to save settings: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateConnectionStatus(e.IsConnected);
        });
    }

    private void UpdateConnectionStatus(bool isConnected)
    {
        if (isConnected)
        {
            ConnectionStatusTextBlock.Text = "متصل - Connected";
            ConnectionStatusTextBlock.Foreground = new SolidColorBrush(Colors.Green);
        }
        else
        {
            ConnectionStatusTextBlock.Text = "غير متصل - Disconnected";
            ConnectionStatusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _signalRClient.OnConnectionStateChanged -= OnConnectionStateChanged;
        base.OnClosed(e);
    }
}
