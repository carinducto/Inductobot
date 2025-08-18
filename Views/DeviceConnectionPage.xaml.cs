using Inductobot.Models.Device;
using Inductobot.Abstractions.Services;
using Inductobot.Services.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace Inductobot.Views;

public partial class DeviceConnectionPage : ContentPage
{
    private readonly IUasWandDiscoveryService _discoveryService;
    private readonly IUasWandDeviceService _deviceService;
    private readonly IUasWandApiService _apiService;
    private readonly INetworkInfoService _networkInfoService;
    private readonly ILogger<DeviceConnectionPage> _logger;
    private ObservableCollection<UASDeviceInfo> _devices = new();
    private CancellationTokenSource? _connectionCts;
    private CancellationTokenSource? _scanCts;
    
    // Constructor for dependency injection
    public DeviceConnectionPage(
        IUasWandDiscoveryService discoveryService,
        IUasWandDeviceService deviceService,
        IUasWandApiService apiService,
        INetworkInfoService networkInfoService,
        ILogger<DeviceConnectionPage> logger)
    {
        InitializeComponent();
        _discoveryService = discoveryService;
        _deviceService = deviceService;
        _apiService = apiService;
        _networkInfoService = networkInfoService;
        _logger = logger;
        
        _logger.LogInformation("DeviceConnectionPage DI constructor called");
        
        InitializePage();
    }

    // Parameterless constructor for XAML DataTemplate (manually resolve dependencies)
    public DeviceConnectionPage() : this(
        GetService<IUasWandDiscoveryService>(),
        GetService<IUasWandDeviceService>(),
        GetService<IUasWandApiService>(),
        GetService<INetworkInfoService>(),
        GetService<ILogger<DeviceConnectionPage>>())
    {
    }

    private static T GetService<T>() where T : class
    {
        try
        {
            var services = Application.Current?.Handler?.MauiContext?.Services;
            if (services == null)
            {
                throw new InvalidOperationException($"MauiContext.Services not available for {typeof(T).Name}");
            }

            if (services.GetService<T>() is T service)
            {
                return service;
            }
            
            throw new InvalidOperationException($"Service {typeof(T).Name} not registered or not available");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to resolve service {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    private void InitializePage()
    {
        _logger.LogInformation("DeviceConnectionPage initialization starting");
        
        InitializeUI();
        SubscribeToEvents();
        _ = UpdateNetworkInfoAsync();
        
        _logger.LogInformation("DeviceConnectionPage initialization complete. Devices collection count: {Count}", _devices.Count);
    }
    
    private void InitializeUI()
    {
        DeviceList.ItemsSource = _devices;
        UpdateDeviceCount();
    }
    
    private void SubscribeToEvents()
    {
        _logger.LogInformation("Subscribing to discovery service events");
        
        _discoveryService.DeviceDiscovered += OnDeviceDiscovered;
        _discoveryService.DeviceRemoved += OnDeviceRemoved;
        _discoveryService.ScanningStateChanged += OnScanningStateChanged;
        _deviceService.ConnectionStateChanged += OnConnectionStateChanged;
        
        _logger.LogInformation("Event subscriptions complete");
        _logger.LogInformation("DeviceDiscovered event subscriber count: {Count}", _discoveryService.GetDeviceDiscoveredSubscriberCount());
        
        // Force immediate sync after subscription
        Task.Run(async () =>
        {
            try
            {
                // Small delay to ensure subscription is fully registered
                await Task.Delay(100);
                
                // Check the subscriber count again after delay
                _logger.LogInformation("DeviceDiscovered event subscriber count after delay: {Count}", _discoveryService.GetDeviceDiscoveredSubscriberCount());
                
                // Force simulator discovery to ensure it's found
                await _discoveryService.ForceDiscoverSimulatorAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in post-subscription discovery");
            }
        });
        
        // Check for any existing discovered devices
        var existingDevices = _discoveryService.DiscoveredDevices;
        _logger.LogInformation("Found {Count} existing discovered devices", existingDevices.Count);
        
        foreach (var device in existingDevices)
        {
            _logger.LogInformation("Existing device: {DeviceName} at {IpAddress}:{Port}", 
                device.Name, device.IpAddress, device.Port);
            
            if (!_devices.Any(d => d.IpAddress == device.IpAddress && d.Port == device.Port))
            {
                // Ensure existing devices have proper initial connection state
                device.ConnectionState = ConnectionState.Disconnected;
                device.LastSeen = DateTime.Now;
                
                _devices.Add(device);
                _logger.LogInformation("Added existing device to UI: {DeviceName}", device.Name);
            }
        }
        
        _logger.LogInformation("After adding existing devices, UI collection count: {Count}", _devices.Count);
        UpdateDeviceCount();
        
        // Also force a simulator check since the hosted service may have started it already
        // Add a small delay to allow the hosted service to start
        Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("Waiting for hosted services to initialize...");
                await Task.Delay(2000); // Wait 2 seconds for services to start
                
                _logger.LogInformation("Forcing initial simulator discovery check");
                await _discoveryService.ForceDiscoverSimulatorAsync();
                
                // Wait a bit more and try again if no devices were found
                await Task.Delay(1000);
                if (_discoveryService.DiscoveredDevices.Count == 0)
                {
                    _logger.LogInformation("No devices found, trying discovery again...");
                    await _discoveryService.ForceDiscoverSimulatorAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during initial simulator discovery check");
            }
        });
    }
    
    private void OnDeviceDiscovered(object? sender, UASDeviceInfo device)
    {
        try
        {
            _logger.LogInformation("OnDeviceDiscovered called for device: {DeviceName} at {IpAddress}:{Port}", 
                device.Name, device.IpAddress, device.Port);
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    _logger.LogInformation("UI thread processing device: {DeviceName}", device.Name);
                    
                    if (!_devices.Any(d => d.IpAddress == device.IpAddress && d.Port == device.Port))
                    {
                        // Ensure new devices start with proper connection state
                        device.ConnectionState = ConnectionState.Disconnected;
                        device.LastSeen = DateTime.Now;
                        
                        _devices.Add(device);
                        _logger.LogInformation("Added device to UI collection. Total devices: {Count}", _devices.Count);
                        UpdateDeviceCount();
                    }
                    else
                    {
                        // Update existing device information while preserving connection state
                        var existingDevice = _devices.First(d => d.IpAddress == device.IpAddress && d.Port == device.Port);
                        existingDevice.Name = device.Name;
                        existingDevice.FirmwareVersion = device.FirmwareVersion;
                        existingDevice.SerialNumber = device.SerialNumber;
                        existingDevice.LastSeen = DateTime.Now;
                        existingDevice.IsOnline = device.IsOnline;
                        
                        // Only update connection state if device is not currently connected
                        if (existingDevice.ConnectionState == ConnectionState.Disconnected)
                        {
                            existingDevice.ConnectionState = device.IsOnline ? ConnectionState.Disconnected : ConnectionState.Disconnected;
                        }
                        
                        // Force UI update
                        var index = _devices.IndexOf(existingDevice);
                        if (index >= 0)
                        {
                            _devices.RemoveAt(index);
                            _devices.Insert(index, existingDevice);
                        }
                        
                        _logger.LogInformation("Updated existing device in UI collection: {DeviceName}", device.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error adding discovered device to UI");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnDeviceDiscovered event handler");
        }
    }
    
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Cancel any ongoing operations
        _connectionCts?.Cancel();
        _scanCts?.Cancel();
        _discoveryService.StopScan();
        
        // Unsubscribe from events
        _discoveryService.DeviceDiscovered -= OnDeviceDiscovered;
        _discoveryService.DeviceRemoved -= OnDeviceRemoved;
        _discoveryService.ScanningStateChanged -= OnScanningStateChanged;
        _deviceService.ConnectionStateChanged -= OnConnectionStateChanged;
        
        // Dispose cancellation tokens
        _connectionCts?.Dispose();
        _scanCts?.Dispose();
    }
    
    private async Task SafeDisplayAlert(string title, string message, string cancel)
    {
        try
        {
            if (MainThread.IsMainThread)
            {
                await DisplayAlert(title, message, cancel);
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert(title, message, cancel);
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error displaying alert: {Title} - {Message}", title, message);
        }
    }
    
    private async Task<bool> SafeDisplayAlert(string title, string message, string accept, string cancel)
    {
        try
        {
            if (MainThread.IsMainThread)
            {
                return await DisplayAlert(title, message, accept, cancel);
            }
            else
            {
                return await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    return await DisplayAlert(title, message, accept, cancel);
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error displaying confirmation alert: {Title} - {Message}", title, message);
            return false; // Default to cancel/negative response on error
        }
    }
    
    private async void OnScanNetworkClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        
        if (_discoveryService.IsScanning)
        {
            // Stop scanning
            ShowStatusToast("Stopping network scan...", ToastType.Info);
            _discoveryService.StopScan();
            _scanCts?.Cancel();
            ShowStatusToast("Network scan stopped", ToastType.Warning);
        }
        else
        {
            // Start scanning
            ShowStatusToast("Starting network device scan...", ToastType.Info);
            
            // Cancel any existing scan
            _scanCts?.Cancel();
            _scanCts = new CancellationTokenSource();
            
            try
            {
                _devices.Clear();
                
                // Start scan with user cancellation support
                var scanTask = _discoveryService.StartScanAsync(_scanCts.Token);
                
                // Also ensure we pick up any devices that were discovered but not added via events
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000); // Wait for scan to find devices
                    var discoveredDevices = _discoveryService.DiscoveredDevices;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        foreach (var device in discoveredDevices)
                        {
                            if (!_devices.Any(d => d.IpAddress == device.IpAddress && d.Port == device.Port))
                            {
                                _logger.LogInformation("Adding device found by discovery but not via events: {DeviceName}", device.Name);
                                _devices.Add(device);
                            }
                        }
                        UpdateDeviceCount();
                    });
                });
                
                // Create a timeout for very long scans
                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(2), _scanCts.Token);
                
                var completedTask = await Task.WhenAny(scanTask, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    ShowStatusToast("Network scan taking longer than expected...", ToastType.Warning);
                    
                    var userChoice = await SafeDisplayAlert("Scan Taking Long", 
                        "Network scan is taking longer than expected. Continue scanning?", 
                        "Keep Scanning", "Stop");
                    
                    if (!userChoice)
                    {
                        _discoveryService.StopScan();
                        _scanCts.Cancel();
                        ShowStatusToast("Network scan cancelled by user", ToastType.Warning);
                    }
                    else
                    {
                        ShowStatusToast("Continuing network scan...", ToastType.Info);
                        // Wait for scan to complete
                        await scanTask;
                    }
                }
                else
                {
                    await scanTask;
                }
                
                // Scan completed successfully
                var deviceCount = _devices.Count;
                ShowStatusToast($"Network scan completed - found {deviceCount} device{(deviceCount != 1 ? "s" : "")}", ToastType.Success);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Network scan cancelled by user");
                ShowStatusToast("Network scan cancelled", ToastType.Warning);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during network scan");
                ShowStatusToast($"Network scan failed: {ex.Message}", ToastType.Error);
            }
            finally
            {
                _scanCts?.Dispose();
                _scanCts = null;
            }
        }
    }
    
    private async void OnAddManualDeviceClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        const string originalButtonText = "Add";
        
        // Input validation
        if (string.IsNullOrWhiteSpace(IpAddressEntry.Text))
        {
            ShowStatusToast("Please enter an IP address", ToastType.Warning);
            IpAddressEntry.Focus();
            return;
        }
        
        if (!int.TryParse(PortEntry.Text, out int port) || port <= 0 || port > 65535)
        {
            ShowStatusToast("Please enter a valid port number (1-65535)", ToastType.Warning);
            PortEntry.Focus();
            return;
        }
        
        var ipAddress = IpAddressEntry.Text.Trim();
        
        // Validate IP address format
        if (!System.Net.IPAddress.TryParse(ipAddress, out _))
        {
            ShowStatusToast("Please enter a valid IP address format", ToastType.Warning);
            IpAddressEntry.Focus();
            return;
        }
        
        try
        {
            // Immediate visual feedback
            SetButtonLoading(button, "Adding...");
            ShowStatusToast($"Adding device {ipAddress}:{port}...", ToastType.Info);
            
            await _discoveryService.AddDeviceManuallyAsync(ipAddress, port);
            
            // Clear inputs on success
            IpAddressEntry.Text = string.Empty;
            PortEntry.Text = "80";
            
            SetButtonSuccess(button, "✅ Added");
            ShowStatusToast($"Device {ipAddress}:{port} added successfully", ToastType.Success);
            
            // Auto-reset button after success
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SetButtonNormal(button, originalButtonText);
                });
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding manual device");
            
            SetButtonError(button, "❌ Failed");
            ShowStatusToast($"Failed to add device: {ex.Message}", ToastType.Error);
            
            // Auto-reset button after error
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SetButtonNormal(button, originalButtonText);
                });
            });
        }
    }
    
    private async void OnConnectManualClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        const string originalButtonText = "Connect";
        
        // Input validation
        if (string.IsNullOrWhiteSpace(IpAddressEntry.Text))
        {
            ShowStatusToast("Please enter an IP address", ToastType.Warning);
            IpAddressEntry.Focus();
            return;
        }
        
        if (!int.TryParse(PortEntry.Text, out int port) || port <= 0 || port > 65535)
        {
            ShowStatusToast("Please enter a valid port number (1-65535)", ToastType.Warning);
            PortEntry.Focus();
            return;
        }
        
        // Validate IP address format
        var ipAddress = IpAddressEntry.Text.Trim();
        if (!System.Net.IPAddress.TryParse(ipAddress, out _))
        {
            ShowStatusToast("Please enter a valid IP address format", ToastType.Warning);
            IpAddressEntry.Focus();
            return;
        }
        
        try
        {
            // Immediate visual feedback
            SetButtonLoading(button, "Connecting...");
            ShowStatusToast($"Connecting to {ipAddress}:{port}...", ToastType.Info);
            
            await ConnectToDeviceAsync(ipAddress, port);
            
            // ConnectToDeviceAsync handles its own success/error feedback
            // Just reset the button
            SetButtonNormal(button, originalButtonText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in manual connect");
            
            SetButtonError(button, "❌ Failed");
            ShowStatusToast($"Connection failed: {ex.Message}", ToastType.Error);
            
            // Auto-reset button after error
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SetButtonNormal(button, originalButtonText);
                });
            });
        }
    }
    
    private async void OnConnectDeviceClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is UASDeviceInfo device)
        {
            const string originalButtonText = "Connect";
            
            try
            {
                // Immediate visual feedback
                SetButtonLoading(button, "Connecting...");
                ShowStatusToast($"Connecting to {device.Name}...", ToastType.Info);
                
                await ConnectToDeviceAsync(device.IpAddress, device.Port);
                
                // ConnectToDeviceAsync handles its own success/error feedback
                // Just reset the button
                SetButtonNormal(button, originalButtonText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to device from list");
                
                SetButtonError(button, "❌ Failed");
                ShowStatusToast($"Connection to {device.Name} failed: {ex.Message}", ToastType.Error);
                
                // Auto-reset button after error
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        SetButtonNormal(button, originalButtonText);
                    });
                });
            }
        }
    }
    
    private async Task ConnectToDeviceAsync(string ipAddress, int port)
    {
        // Cancel any existing connection attempt
        _connectionCts?.Cancel();
        _connectionCts = new CancellationTokenSource();
        
        try
        {
            StatusLabel.Text = "Connecting...";
            ConnectionInfoLabel.Text = $"{ipAddress}:{port}";
            
            // Update the device status to show connecting state
            var targetDevice = _devices.FirstOrDefault(d => d.IpAddress == ipAddress && d.Port == port);
            if (targetDevice != null)
            {
                targetDevice.ConnectionState = ConnectionState.Connecting;
                // Force UI refresh
                var index = _devices.IndexOf(targetDevice);
                if (index >= 0)
                {
                    _devices.RemoveAt(index);
                    _devices.Insert(index, targetDevice);
                }
            }
            
            var connectTask = _deviceService.ConnectToDeviceAsync(ipAddress, port, _connectionCts.Token);
            
            // Create timeout with user option to cancel
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(15), _connectionCts.Token);
            
            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            
            bool connected;
            if (completedTask == timeoutTask)
            {
                // Connection timed out - show status via traffic light, no interrupting dialog
                _connectionCts.Cancel();
                StatusLabel.Text = "Connection timeout";
                ShowStatusToast($"Connection to {ipAddress}:{port} timed out after 15 seconds", ToastType.Warning);
                
                // Update device status to show timeout state
                if (targetDevice != null)
                {
                    targetDevice.ConnectionState = ConnectionState.Timeout;
                    var index = _devices.IndexOf(targetDevice);
                    if (index >= 0)
                    {
                        _devices.RemoveAt(index);
                        _devices.Insert(index, targetDevice);
                    }
                }
                return;
            }
            else
            {
                // Connection task completed (either successfully or with exception)
                connected = await connectTask;
            }
            
            if (connected)
            {
                StatusLabel.Text = "Connected";
                // Traffic light indicator shows green status - no pop-up interruption needed
                
                // Automatically retrieve WiFi settings after successful connection
                try
                {
                    ShowStatusToast("Retrieving WiFi settings...", ToastType.Info);
                    _logger.LogInformation("Automatically retrieving WiFi settings after connection");
                    
                    var wifiResponse = await _apiService.GetWifiSettingsAsync();
                    if (wifiResponse.IsSuccess && wifiResponse.Data != null)
                    {
                        _logger.LogInformation("WiFi settings retrieved successfully: SSID={SSID}, Enabled={Enabled}", 
                            wifiResponse.Data.Ssid, wifiResponse.Data.Enabled);
                        ShowStatusToast("WiFi settings retrieved successfully", ToastType.Success);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to retrieve WiFi settings: {Error}", wifiResponse.Message);
                        ShowStatusToast("WiFi settings retrieval failed", ToastType.Warning);
                    }
                }
                catch (Exception wifiEx)
                {
                    _logger.LogError(wifiEx, "Error retrieving WiFi settings after connection");
                    ShowStatusToast("WiFi settings retrieval error", ToastType.Warning);
                }
                
                // Navigate to UAS-WAND control page
                await Shell.Current.GoToAsync("//DeviceControl");
            }
            else
            {
                StatusLabel.Text = "Connection failed";
                // Traffic light indicator shows red status - no pop-up interruption needed
            }
        }
        catch (OperationCanceledException)
        {
            StatusLabel.Text = "Connection cancelled";
            _logger.LogInformation("Connection cancelled by user");
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Connection error";
            _logger.LogError(ex, "Error connecting to device");
            ShowStatusToast($"Connection failed: {ex.Message}", ToastType.Error);
            
            // Update device status to show error state
            var targetDevice = _devices.FirstOrDefault(d => d.IpAddress == ipAddress && d.Port == port);
            if (targetDevice != null)
            {
                targetDevice.ConnectionState = ConnectionState.Error;
                var index = _devices.IndexOf(targetDevice);
                if (index >= 0)
                {
                    _devices.RemoveAt(index);
                    _devices.Insert(index, targetDevice);
                }
            }
        }
        finally
        {
            _connectionCts?.Dispose();
            _connectionCts = null;
        }
    }
    
    private void OnRemoveDeviceClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is UASDeviceInfo device)
        {
            const string originalButtonText = "Remove";
            
            try
            {
                // Immediate visual feedback
                SetButtonLoading(button, "Removing...");
                ShowStatusToast($"Removing {device.Name} from device list...", ToastType.Info);
                
                _discoveryService.RemoveDevice(device);
                
                SetButtonSuccess(button, "✅ Removed");
                ShowStatusToast($"{device.Name} removed successfully", ToastType.Success);
                
                // Auto-reset button after success (though it will likely be removed from UI)
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        SetButtonNormal(button, originalButtonText);
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing device");
                
                SetButtonError(button, "❌ Failed");
                ShowStatusToast($"Failed to remove {device.Name}: {ex.Message}", ToastType.Error);
                
                // Auto-reset button after error
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        SetButtonNormal(button, originalButtonText);
                    });
                });
            }
        }
    }
    
    private void OnDeviceSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is UASDeviceInfo device)
        {
            // Could show device details or auto-connect
            DeviceList.SelectedItem = null; // Clear selection
        }
    }
    
    private async void OnDeviceTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (sender is Border border && border.BindingContext is UASDeviceInfo device)
            {
                var action = await DisplayActionSheet($"{device.Name}", "Cancel", null, "Connect", "Test Connection", "Refresh", "Remove");
                
                switch (action)
                {
                    case "Connect":
                        await ConnectToDeviceAsync(device.IpAddress, device.Port);
                        break;
                    case "Test Connection":
                        try
                        {
                            var reachable = await _deviceService.TestConnectionAsync(device.IpAddress, device.Port);
                            await DisplayAlert("Connection Test", reachable ? "Device is reachable" : "Device is not reachable", "OK");
                        }
                        catch (Exception testEx)
                        {
                            _logger.LogError(testEx, "Error testing device connection");
                            await SafeDisplayAlert("Error", "Failed to test device connection", "OK");
                        }
                        break;
                    case "Refresh":
                        try
                        {
                            await _discoveryService.RefreshDeviceAsync(device);
                        }
                        catch (Exception refreshEx)
                        {
                            _logger.LogError(refreshEx, "Error refreshing device");
                            await SafeDisplayAlert("Error", "Failed to refresh device", "OK");
                        }
                        break;
                    case "Remove":
                        try
                        {
                            _discoveryService.RemoveDevice(device);
                        }
                        catch (Exception removeEx)
                        {
                            _logger.LogError(removeEx, "Error removing device");
                            await SafeDisplayAlert("Error", "Failed to remove device", "OK");
                        }
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnDeviceTapped event handler");
            try
            {
                await SafeDisplayAlert("Error", "An error occurred while processing device action", "OK");
            }
            catch (Exception alertEx)
            {
                _logger.LogError(alertEx, "Error showing device action error alert");
            }
        }
    }
    
    
    private void OnDeviceRemoved(object? sender, UASDeviceInfo device)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    var toRemove = _devices.FirstOrDefault(d => d.IpAddress == device.IpAddress && d.Port == device.Port);
                    if (toRemove != null)
                    {
                        _devices.Remove(toRemove);
                        UpdateDeviceCount();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error removing device from UI");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnDeviceRemoved event handler");
        }
    }
    
    private void OnScanningStateChanged(object? sender, bool isScanning)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    ScanIndicator.IsRunning = isScanning;
                    ScanButton.Text = isScanning ? "Stop Scan" : "Scan Network";
                    ScanButton.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating scan UI state");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnScanningStateChanged event handler");
        }
    }
    
    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    StatusLabel.Text = state.ToString();
                    
                    // Update the connected device in the discovered devices list
                    UpdateConnectedDeviceStatus(state);
                    
                    switch (state)
                    {
                        case ConnectionState.Connected:
                            StatusLabel.TextColor = Colors.Green;
                            break;
                        case ConnectionState.Connecting:
                            StatusLabel.TextColor = Colors.Orange;
                            break;
                        case ConnectionState.Error:
                            StatusLabel.TextColor = Colors.Red;
                            break;
                        default:
                            StatusLabel.TextColor = Colors.Gray;
                            ConnectionInfoLabel.Text = string.Empty;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating connection status UI");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnConnectionStateChanged event handler");
        }
    }
    
    /// <summary>
    /// Updates the connection state of the currently connected device in the discovered devices list
    /// </summary>
    private void UpdateConnectedDeviceStatus(ConnectionState state)
    {
        try
        {
            if (_deviceService?.CurrentDevice != null)
            {
                var connectedDevice = _devices.FirstOrDefault(d => 
                    d.IpAddress == _deviceService.CurrentDevice.IpAddress && 
                    d.Port == _deviceService.CurrentDevice.Port);
                
                if (connectedDevice != null)
                {
                    connectedDevice.ConnectionState = state;
                    connectedDevice.IsOnline = state == ConnectionState.Connected;
                    
                    // Update last connected time for successful connections
                    if (state == ConnectionState.Connected)
                    {
                        connectedDevice.LastConnected = DateTime.Now;
                    }
                    
                    // Force UI refresh by triggering collection change notification
                    var index = _devices.IndexOf(connectedDevice);
                    if (index >= 0)
                    {
                        _devices.RemoveAt(index);
                        _devices.Insert(index, connectedDevice);
                    }
                    
                    // Refresh the UI to show updated status
                    UpdateDeviceCount();
                }
            }
            
            // Reset all other devices to disconnected if a new connection is made
            if (state == ConnectionState.Connected && _deviceService?.CurrentDevice != null)
            {
                foreach (var device in _devices)
                {
                    if (device.IpAddress != _deviceService.CurrentDevice.IpAddress || 
                        device.Port != _deviceService.CurrentDevice.Port)
                    {
                        device.ConnectionState = ConnectionState.Disconnected;
                        device.IsOnline = false;
                    }
                }
                
                // Force refresh of all items
                var allDevices = _devices.ToList();
                _devices.Clear();
                foreach (var device in allDevices)
                {
                    _devices.Add(device);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating connected device status");
        }
    }
    
    private void UpdateDeviceCount()
    {
        try
        {
            var count = _devices?.Count ?? 0;
            DeviceCountLabel.Text = count == 1 ? "1 device" : $"{count} devices";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating device count display");
            DeviceCountLabel.Text = "0 devices"; // Fallback
        }
    }

    #region Network Information

    private async Task UpdateNetworkInfoAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                var networkInfo = _networkInfoService.GetCurrentNetworkInfo();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        if (networkInfo.IsConnected)
                        {
                            GatewayAddressLabel.Text = networkInfo.GatewayAddress ?? "Unknown";
                            IPRangeLabel.Text = networkInfo.IPRange ?? "Unknown";
                            NetworkInterfaceLabel.Text = $"{networkInfo.InterfaceName} ({networkInfo.LocalIPAddress})";
                        }
                        else
                        {
                            GatewayAddressLabel.Text = "Not Connected";
                            IPRangeLabel.Text = "Not Connected";
                            NetworkInterfaceLabel.Text = networkInfo.ErrorMessage ?? "Network Error";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating network UI elements");
                        GatewayAddressLabel.Text = "Error";
                        IPRangeLabel.Text = "Error";
                        NetworkInterfaceLabel.Text = "Error";
                    }
                });
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting network information");
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                GatewayAddressLabel.Text = "Error";
                IPRangeLabel.Text = "Error";
                NetworkInterfaceLabel.Text = "Network Error";
            });
        }
    }

    private async void OnRefreshNetworkClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        const string originalButtonText = "Refresh Network";
        
        try
        {
            // Immediate visual feedback
            SetButtonLoading(button, "Refreshing...");
            ShowStatusToast("Refreshing network information...", ToastType.Info);
            
            // Show loading state in display area
            GatewayAddressLabel.Text = "Loading...";
            IPRangeLabel.Text = "Loading...";
            NetworkInterfaceLabel.Text = "Loading...";

            await UpdateNetworkInfoAsync();
            
            SetButtonSuccess(button, "✅ Refreshed");
            ShowStatusToast("Network information updated successfully", ToastType.Success);
            
            // Auto-reset button after success
            _ = Task.Run(async () =>
            {
                await Task.Delay(1500);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SetButtonNormal(button, originalButtonText);
                });
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing network information");
            
            SetButtonError(button, "❌ Failed");
            ShowStatusToast($"Network refresh failed: {ex.Message}", ToastType.Error);
            
            // Auto-reset button after error
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SetButtonNormal(button, originalButtonText);
                });
            });
        }
    }

    #endregion

    #region Theme-Aware Color System
    
    /// <summary>
    /// Gets theme-appropriate colors that work in both Light and Dark modes
    /// </summary>
    private static class ThemeColors
    {
        // Status Colors - Theme aware
        public static Color Success => Application.Current?.RequestedTheme == AppTheme.Dark 
            ? Color.FromArgb("#4CAF50")   // Softer green for dark mode
            : Color.FromArgb("#388E3C");  // Standard green for light mode
            
        public static Color Error => Application.Current?.RequestedTheme == AppTheme.Dark 
            ? Color.FromArgb("#F44336")   // Softer red for dark mode  
            : Color.FromArgb("#D32F2F");  // Standard red for light mode
            
        public static Color Warning => Application.Current?.RequestedTheme == AppTheme.Dark 
            ? Color.FromArgb("#FF9800")   // Softer orange for dark mode
            : Color.FromArgb("#F57C00");  // Standard orange for light mode
            
        public static Color Info => Application.Current?.RequestedTheme == AppTheme.Dark 
            ? Color.FromArgb("#2196F3")   // Softer blue for dark mode
            : Color.FromArgb("#1976D2");  // Standard blue for light mode
            
        // Text Colors - High contrast for readability
        public static Color OnColorText => Colors.White; // Always white text on colored backgrounds
        
        public static Color SecondaryText => Application.Current?.RequestedTheme == AppTheme.Dark 
            ? Color.FromArgb("#B0B0B0")   // Light gray for dark mode
            : Color.FromArgb("#666666");  // Dark gray for light mode
    }

    #endregion

    #region Button State Management

    /// <summary>
    /// Sets a button to loading state with spinner and custom text
    /// </summary>
    private void SetButtonLoading(Button button, string loadingText = null)
    {
        if (button == null) return;
        
        try
        {
            button.IsEnabled = false;
            button.Text = loadingText ?? "Loading...";
            button.BackgroundColor = ThemeColors.Warning;
            button.TextColor = ThemeColors.OnColorText;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting button loading state");
        }
    }
    
    /// <summary>
    /// Sets a button to success state with green color and checkmark
    /// Note: Does not auto-reset - caller must handle reset timing
    /// </summary>
    private void SetButtonSuccess(Button button, string successText = null)
    {
        if (button == null) return;
        
        try
        {
            button.Text = successText ?? "✅ Success";
            button.BackgroundColor = ThemeColors.Success;
            button.TextColor = ThemeColors.OnColorText;
            button.IsEnabled = false; // Keep disabled until reset
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting button success state");
        }
    }
    
    /// <summary>
    /// Sets a button to error state with red color and X mark
    /// Note: Does not auto-reset - caller must handle reset timing
    /// </summary>
    private void SetButtonError(Button button, string errorText = null)
    {
        if (button == null) return;
        
        try
        {
            button.Text = errorText ?? "❌ Error";
            button.BackgroundColor = ThemeColors.Error;
            button.TextColor = ThemeColors.OnColorText;
            button.IsEnabled = false; // Keep disabled until reset
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting button error state");
        }
    }
    
    /// <summary>
    /// Resets a button to normal state
    /// </summary>
    private void SetButtonNormal(Button button, string originalText = null)
    {
        if (button == null) return;
        
        try
        {
            button.IsEnabled = true;
            
            // Clear background and text colors to restore default theme appearance
            button.ClearValue(Button.BackgroundColorProperty);
            button.ClearValue(Button.TextColorProperty);
            
            // Restore original text
            if (!string.IsNullOrEmpty(originalText))
            {
                button.Text = originalText;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting button normal state");
        }
    }
    
    /// <summary>
    /// Shows a toast-style message in the status bar with color coding
    /// </summary>
    private async void ShowStatusToast(string message, ToastType type = ToastType.Info, int durationMs = 3000)
    {
        if (StatusLabel == null) return;
        
        try
        {
            var originalText = StatusLabel.Text;
            var originalColor = StatusLabel.TextColor;
            
            // Set status with color coding
            StatusLabel.Text = GetToastIcon(type) + " " + message;
            StatusLabel.TextColor = GetToastColor(type);
            
            // Reset after duration
            await Task.Delay(durationMs);
            
            StatusLabel.Text = originalText;
            StatusLabel.TextColor = originalColor;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error showing status toast");
        }
    }
    
    /// <summary>
    /// Toast notification types
    /// </summary>
    private enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }
    
    /// <summary>
    /// Gets icon for toast type
    /// </summary>
    private string GetToastIcon(ToastType type)
    {
        return type switch
        {
            ToastType.Success => "✅",
            ToastType.Error => "❌",
            ToastType.Warning => "⚠️",
            ToastType.Info => "ℹ️",
            _ => "ℹ️"
        };
    }
    
    /// <summary>
    /// Gets theme-aware color for toast type
    /// </summary>
    private Color GetToastColor(ToastType type)
    {
        return type switch
        {
            ToastType.Success => ThemeColors.Success,
            ToastType.Error => ThemeColors.Error,
            ToastType.Warning => ThemeColors.Warning,
            ToastType.Info => ThemeColors.Info,
            _ => ThemeColors.SecondaryText
        };
    }

    #endregion

}