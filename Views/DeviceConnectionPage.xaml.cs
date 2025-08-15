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
    private readonly INetworkInfoService _networkInfoService;
    private readonly ILogger<DeviceConnectionPage> _logger;
    private ObservableCollection<UASDeviceInfo> _devices = new();
    private CancellationTokenSource? _connectionCts;
    private CancellationTokenSource? _scanCts;
    
    // Constructor for dependency injection
    public DeviceConnectionPage(
        IUasWandDiscoveryService discoveryService,
        IUasWandDeviceService deviceService,
        INetworkInfoService networkInfoService,
        ILogger<DeviceConnectionPage> logger)
    {
        InitializeComponent();
        _discoveryService = discoveryService;
        _deviceService = deviceService;
        _networkInfoService = networkInfoService;
        _logger = logger;
        
        _logger.LogInformation("DeviceConnectionPage DI constructor called");
        
        InitializePage();
    }

    // Parameterless constructor for XAML DataTemplate (manually resolve dependencies)
    public DeviceConnectionPage() : this(
        GetService<IUasWandDiscoveryService>(),
        GetService<IUasWandDeviceService>(),
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
                        _devices.Add(device);
                        _logger.LogInformation("Added device to UI collection. Total devices: {Count}", _devices.Count);
                        UpdateDeviceCount();
                    }
                    else
                    {
                        _logger.LogInformation("Device already exists in UI collection: {DeviceName}", device.Name);
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
        if (_discoveryService.IsScanning)
        {
            _discoveryService.StopScan();
            _scanCts?.Cancel();
        }
        else
        {
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
                    var userChoice = await DisplayAlert("Scan Taking Long", 
                        "Network scan is taking longer than expected. Continue scanning?", 
                        "Keep Scanning", "Stop");
                    
                    if (!userChoice)
                    {
                        _discoveryService.StopScan();
                        _scanCts.Cancel();
                    }
                    else
                    {
                        // Wait for scan to complete
                        await scanTask;
                    }
                }
                else
                {
                    await scanTask;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Network scan cancelled by user");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during network scan");
                await DisplayAlert("Scan Error", $"Network scan failed: {ex.Message}", "OK");
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
        try
        {
            if (string.IsNullOrWhiteSpace(IpAddressEntry.Text))
            {
                await SafeDisplayAlert("Error", "Please enter an IP address", "OK");
                return;
            }
            
            if (!int.TryParse(PortEntry.Text, out int port) || port <= 0 || port > 65535)
            {
                await SafeDisplayAlert("Error", "Please enter a valid port number (1-65535)", "OK");
                return;
            }
            
            var ipAddress = IpAddressEntry.Text.Trim();
            
            // Validate IP address format
            if (!System.Net.IPAddress.TryParse(ipAddress, out _))
            {
                await SafeDisplayAlert("Error", "Please enter a valid IP address format", "OK");
                return;
            }
            
            await _discoveryService.AddDeviceManuallyAsync(ipAddress, port);
            
            IpAddressEntry.Text = string.Empty;
            PortEntry.Text = "80";
            
            await SafeDisplayAlert("Success", $"Device {ipAddress}:{port} added to device list", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding manual device");
            await SafeDisplayAlert("Error", "Failed to add device. Please try again.", "OK");
        }
    }
    
    private async void OnConnectManualClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(IpAddressEntry.Text))
        {
            await DisplayAlert("Error", "Please enter an IP address", "OK");
            return;
        }
        
        if (!int.TryParse(PortEntry.Text, out int port) || port <= 0 || port > 65535)
        {
            await DisplayAlert("Error", "Please enter a valid port number (1-65535)", "OK");
            return;
        }
        
        await ConnectToDeviceAsync(IpAddressEntry.Text.Trim(), port);
    }
    
    private async void OnConnectDeviceClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is UASDeviceInfo device)
        {
            await ConnectToDeviceAsync(device.IpAddress, device.Port);
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
            
            var connectTask = _deviceService.ConnectToDeviceAsync(ipAddress, port, _connectionCts.Token);
            
            // Create timeout with user option to cancel
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(15), _connectionCts.Token);
            
            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            
            bool connected;
            if (completedTask == timeoutTask)
            {
                var userChoice = await SafeDisplayAlert("Connection Timeout", 
                    $"Connection to {ipAddress}:{port} is taking longer than expected. Continue waiting?", 
                    "Keep Trying", "Cancel");
                
                if (!userChoice)
                {
                    _connectionCts.Cancel();
                    StatusLabel.Text = "Connection cancelled";
                    return;
                }
                
                // Wait for connection to complete if user chose to continue
                connected = await connectTask;
            }
            else
            {
                // Connection task completed (either successfully or with exception)
                connected = await connectTask;
            }
            
            if (connected)
            {
                StatusLabel.Text = "Connected";
                await SafeDisplayAlert("Success", $"Connected to device at {ipAddress}:{port}", "OK");
                
                // Navigate to device control page
                await Shell.Current.GoToAsync("//DeviceControl");
            }
            else
            {
                StatusLabel.Text = "Connection failed";
                await SafeDisplayAlert("Error", $"Failed to connect to device at {ipAddress}:{port}", "OK");
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
            await SafeDisplayAlert("Error", "Connection failed. Please check device connectivity and try again.", "OK");
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
            _discoveryService.RemoveDevice(device);
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
        try
        {
            // Show loading state
            GatewayAddressLabel.Text = "Loading...";
            IPRangeLabel.Text = "Loading...";
            NetworkInterfaceLabel.Text = "Loading...";

            await UpdateNetworkInfoAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing network information");
            await SafeDisplayAlert("Error", $"Failed to refresh network information: {ex.Message}", "OK");
        }
    }

    #endregion


}