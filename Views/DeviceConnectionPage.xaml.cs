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
    private readonly IUasWandComPortService _comPortService;
    private readonly INetworkInfoService _networkInfoService;
    private readonly ILogger<DeviceConnectionPage> _logger;
    private ObservableCollection<UASDeviceInfo> _devices = new();
    private ObservableCollection<UASDeviceInfo> _filteredDevices = new();
    private ObservableCollection<ComPortInfo> _comPorts = new();
    private bool _showUasOnly = true;
    private CancellationTokenSource? _connectionCts;
    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _comPortScanCts;
    private ConnectionMode _currentConnectionMode = ConnectionMode.WiFiHttp;
    
    // Constructor for dependency injection
    public DeviceConnectionPage(
        IUasWandDiscoveryService discoveryService,
        IUasWandDeviceService deviceService,
        IUasWandApiService apiService,
        IUasWandComPortService comPortService,
        INetworkInfoService networkInfoService,
        ILogger<DeviceConnectionPage> logger)
    {
        InitializeComponent();
        _discoveryService = discoveryService;
        _deviceService = deviceService;
        _apiService = apiService;
        _comPortService = comPortService;
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
        GetService<IUasWandComPortService>(),
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
        InitializeConnectionMode();
        _ = UpdateNetworkInfoAsync();
        
        _logger.LogInformation("DeviceConnectionPage initialization complete. Devices collection count: {Count}", _devices.Count);
    }

    private void InitializeConnectionMode()
    {
        // Initialize with WiFi/HTTP mode by default
        _currentConnectionMode = ConnectionMode.WiFiHttp;
        UpdateUIForConnectionMode();
        _logger.LogInformation("Initialized with connection mode: {Mode}", _currentConnectionMode);
    }
    
    private void InitializeUI()
    {
        DeviceList.ItemsSource = _filteredDevices;
        ComPortList.ItemsSource = _comPorts;
        UpdateDeviceCount();
    }
    
    private void SubscribeToEvents()
    {
        _logger.LogInformation("Subscribing to discovery service events");
        
        _discoveryService.DeviceDiscovered += OnDeviceDiscovered;
        _discoveryService.DeviceRemoved += OnDeviceRemoved;
        _discoveryService.ScanningStateChanged += OnScanningStateChanged;
        _discoveryService.ScanProgressChanged += OnScanProgressChanged;
        _deviceService.ConnectionStateChanged += OnConnectionStateChanged;
        _comPortService.ComPortDiscovered += OnComPortDiscovered;
        _comPortService.ComPortRemoved += OnComPortRemoved;
        _comPortService.ConnectionStateChanged += OnComPortConnectionStateChanged;
        _comPortService.ScanProgressChanged += OnComPortScanProgressChanged;
        
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
                
                // Apply filter to existing device
                if (!_showUasOnly || IsUasDevice(device))
                {
                    _filteredDevices.Add(device);
                }
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
                        
                        // Apply filter to new device
                        if (!_showUasOnly || IsUasDevice(device))
                        {
                            _filteredDevices.Add(device);
                        }
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
        _discoveryService.ScanProgressChanged -= OnScanProgressChanged;
        _deviceService.ConnectionStateChanged -= OnConnectionStateChanged;
        _comPortService.ComPortDiscovered -= OnComPortDiscovered;
        _comPortService.ComPortRemoved -= OnComPortRemoved;
        _comPortService.ConnectionStateChanged -= OnComPortConnectionStateChanged;
        _comPortService.ScanProgressChanged -= OnComPortScanProgressChanged;
        
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
                _filteredDevices.Clear();
                
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
                                
                                // Apply filter to post-scan device
                                if (!_showUasOnly || IsUasDevice(device))
                                {
                                    _filteredDevices.Add(device);
                                }
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
            ShowStatusToast($"Adding device {ipAddress}:{port} (HTTP port 80 recommended for ESP32)...", ToastType.Info);
            
            await _discoveryService.AddDeviceManuallyAsync(ipAddress, port);
            
            // Clear IP input on success, keep port for easier multiple adds  
            IpAddressEntry.Text = string.Empty;
            // Keep the same port for adding multiple devices on same port
            
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
    
    private void OnQuickPortClicked(object sender, EventArgs e)
    {
        if (sender is Button button)
        {
            PortEntry.Text = button.Text;
            var portNum = button.Text;
            var protocol = portNum == "80" || portNum == "8080" ? "HTTP" : "HTTPS";
            ShowStatusToast($"Port set to {portNum} ({protocol})", ToastType.Info);
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
            var protocol = port == 80 || port == 8080 || port == 5000 ? "HTTP" : "HTTPS";
            ShowStatusToast($"Connecting to {ipAddress}:{port} via {protocol}...", ToastType.Info);
            
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
                // UI will automatically update via INotifyPropertyChanged
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
                    // UI will automatically update via INotifyPropertyChanged
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
                
                // Automatically retrieve WiFi settings after successful authentication
                try
                {
                    // Verify device is still connected and authenticated before retrieving WiFi settings
                    if (_deviceService?.CurrentDevice == null || !_deviceService.IsConnected)
                    {
                        _logger.LogWarning("Device disconnected or not authenticated - skipping WiFi auto-retrieval");
                        ShowStatusToast("Device disconnected - WiFi retrieval skipped", ToastType.Warning);
                    }
                    else
                    {
                        // First verify authentication by attempting a device info call
                        _logger.LogInformation("Verifying authentication before WiFi auto-retrieval");
                        var authTest = await _apiService.GetDeviceInfoAsync();
                        
                        if (!authTest.IsSuccess)
                        {
                            if (authTest.ErrorCode == "UNAUTHORIZED")
                            {
                                _logger.LogWarning("Authentication failed - cannot auto-retrieve WiFi settings: {Error}", authTest.Message);
                                ShowStatusToast("Authentication required for WiFi settings", ToastType.Warning);
                            }
                            else
                            {
                                _logger.LogWarning("Device communication failed - skipping WiFi auto-retrieval: {Error}", authTest.Message);
                                ShowStatusToast("Device communication failed", ToastType.Warning);
                            }
                        }
                        else
                        {
                            // Authentication verified - proceed with WiFi retrieval
                            ShowStatusToast("Retrieving WiFi settings...", ToastType.Info);
                            _logger.LogInformation("Authentication verified - automatically retrieving WiFi settings");
                            
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
                // UI will automatically update via INotifyPropertyChanged
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
                        
                        // Also remove from filtered collection
                        var toRemoveFiltered = _filteredDevices.FirstOrDefault(d => d.IpAddress == device.IpAddress && d.Port == device.Port);
                        if (toRemoveFiltered != null)
                        {
                            _filteredDevices.Remove(toRemoveFiltered);
                        }
                        
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
                    
                    // Show/hide progress frame based on scanning state
                    ScanProgressFrame.IsVisible = isScanning;
                    
                    // Reset progress when scanning stops
                    if (!isScanning)
                    {
                        ScanProgressBar.Progress = 0;
                        ScanPercentLabel.Text = "0%";
                        ScanStatusLabel.Text = "Scanning...";
                        ScanDetailsLabel.IsVisible = false;
                    }
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
    
    private void OnScanProgressChanged(object? sender, Models.Discovery.ScanProgress progress)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    // Update progress bar and percentage
                    ScanProgressBar.Progress = progress.PercentComplete / 100.0;
                    ScanPercentLabel.Text = $"{progress.PercentComplete}%";
                    
                    // Update status text
                    ScanStatusLabel.Text = progress.CurrentStep;
                    
                    // Show details if debugging info is available
                    if (!string.IsNullOrEmpty(progress.DebugInfo))
                    {
                        var detailsText = "";
                        
                        if (progress.TotalSubnets > 0)
                        {
                            detailsText = $"Subnets: {progress.SubnetsScanned}/{progress.TotalSubnets}";
                        }
                        
                        if (progress.TotalHosts > 0)
                        {
                            detailsText += string.IsNullOrEmpty(detailsText) ? "" : " | ";
                            detailsText += $"Hosts: {progress.HostsScanned}/{progress.TotalHosts}";
                        }
                        
                        if (progress.UasDevicesFound > 0)
                        {
                            detailsText += string.IsNullOrEmpty(detailsText) ? "" : " | ";
                            detailsText += $"Devices: {progress.UasDevicesFound}";
                        }
                        
                        if (!string.IsNullOrEmpty(detailsText))
                        {
                            ScanDetailsLabel.Text = detailsText;
                            ScanDetailsLabel.IsVisible = true;
                        }
                        else
                        {
                            ScanDetailsLabel.Text = progress.DebugInfo;
                            ScanDetailsLabel.IsVisible = true;
                        }
                    }
                    else
                    {
                        ScanDetailsLabel.IsVisible = false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating scan progress UI");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnScanProgressChanged event handler");
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
                    
                    // UI will automatically update via INotifyPropertyChanged
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
                
                // Devices will automatically update via INotifyPropertyChanged
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
            var count = _filteredDevices?.Count ?? 0;
            DeviceCountLabel.Text = count == 1 ? "1 device" : $"{count} devices";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating device count display");
            DeviceCountLabel.Text = "0 devices"; // Fallback
        }
    }
    
    private void OnFilterSwitchToggled(object sender, ToggledEventArgs e)
    {
        try
        {
            _showUasOnly = e.Value;
            ApplyDeviceFilter();
            _logger.LogDebug("Device filter changed to: {FilterMode}", _showUasOnly ? "UAS Only" : "All Devices");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling filter switch toggle");
        }
    }
    
    private void ApplyDeviceFilter()
    {
        try
        {
            _filteredDevices.Clear();
            
            var devicesToShow = _showUasOnly 
                ? _devices.Where(IsUasDevice)
                : _devices;
            
            foreach (var device in devicesToShow)
            {
                _filteredDevices.Add(device);
            }
            
            UpdateDeviceCount();
            _logger.LogDebug("Applied device filter. Showing {Count} of {Total} devices", 
                _filteredDevices.Count, _devices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying device filter");
        }
    }
    
    private static bool IsUasDevice(UASDeviceInfo device)
    {
        // Only consider a device as UAS if its name contains "UAS" (case insensitive)
        // Do NOT use port number or device type to determine if it's a UAS device
        var deviceName = device.Name?.ToLower() ?? "";
        
        // Check for UAS in the device name or if it's a simulator
        var isUas = deviceName.Contains("uas") || deviceName.Contains("simulator");
                   
        System.Diagnostics.Debug.WriteLine($"IsUasDevice: '{device.Name}' -> {isUas}");
        
        return isUas;
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

    #region Connection Mode Management

    private async void OnWiFiModeSelected(object sender, TappedEventArgs e)
    {
        if (_currentConnectionMode == ConnectionMode.WiFiHttp)
            return;

        await SwitchToConnectionModeAsync(ConnectionMode.WiFiHttp);
    }

    private async void OnUsbModeSelected(object sender, TappedEventArgs e)
    {
        if (_currentConnectionMode == ConnectionMode.UsbComPort)
            return;

        await SwitchToConnectionModeAsync(ConnectionMode.UsbComPort);
    }

    private async Task SwitchToConnectionModeAsync(ConnectionMode newMode)
    {
        try
        {
            _logger.LogInformation("Switching connection mode from {CurrentMode} to {NewMode}", 
                _currentConnectionMode, newMode);

            // Disconnect any existing connections before switching modes
            if (_currentConnectionMode != ConnectionMode.None)
            {
                if (_currentConnectionMode == ConnectionMode.WiFiHttp && _deviceService.IsConnected)
                {
                    await _deviceService.DisconnectAsync();
                }
                else if (_currentConnectionMode == ConnectionMode.UsbComPort && _comPortService.IsConnected)
                {
                    await _comPortService.DisconnectAsync();
                }
            }

            // Cancel any ongoing operations
            _scanCts?.Cancel();
            _comPortScanCts?.Cancel();

            _currentConnectionMode = newMode;
            UpdateUIForConnectionMode();

            _logger.LogInformation("Successfully switched to connection mode: {Mode}", newMode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch connection mode to {Mode}", newMode);
            ShowStatusToast("Failed to switch connection mode", ToastType.Error);
        }
    }

    private void UpdateUIForConnectionMode()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            switch (_currentConnectionMode)
            {
                case ConnectionMode.WiFiHttp:
                    // Update mode selector UI
                    WiFiModeBorder.Stroke = Color.FromArgb("#2196F3");
                    WiFiModeIndicator.Color = Color.FromArgb("#2196F3");
                    UsbModeBorder.Stroke = Color.FromArgb("#E0E0E0");
                    UsbModeIndicator.Color = Colors.Transparent;

                    // Update sections visibility
                    WiFiSection.IsVisible = true;
                    UsbSection.IsVisible = false;

                    // Update status and description
                    ActiveModeFrame.IsVisible = true;
                    ActiveModeLabel.Text = "📡 WiFi/Network Mode Active - HTTP/HTTPS API";
                    ModeDescriptionLabel.Text = "Connected devices will use network communication";
                    break;

                case ConnectionMode.UsbComPort:
                    // Update mode selector UI
                    UsbModeBorder.Stroke = Color.FromArgb("#2196F3");
                    UsbModeIndicator.Color = Color.FromArgb("#2196F3");
                    WiFiModeBorder.Stroke = Color.FromArgb("#E0E0E0");
                    WiFiModeIndicator.Color = Colors.Transparent;

                    // Update sections visibility
                    WiFiSection.IsVisible = false;
                    UsbSection.IsVisible = true;

                    // Update status and description
                    ActiveModeFrame.IsVisible = true;
                    ActiveModeLabel.Text = "🔌 USB/Serial Mode Active - COM Port API";
                    ModeDescriptionLabel.Text = "Connected devices will use USB serial communication";
                    break;

                case ConnectionMode.None:
                default:
                    // Reset UI to neutral state
                    WiFiModeBorder.Stroke = Color.FromArgb("#E0E0E0");
                    WiFiModeIndicator.Color = Colors.Transparent;
                    UsbModeBorder.Stroke = Color.FromArgb("#E0E0E0");
                    UsbModeIndicator.Color = Colors.Transparent;

                    WiFiSection.IsVisible = false;
                    UsbSection.IsVisible = false;

                    ActiveModeFrame.IsVisible = false;
                    ModeDescriptionLabel.Text = "Select how you want to connect to UAS-WAND devices";
                    break;
            }

            _logger.LogDebug("UI updated for connection mode: {Mode}", _currentConnectionMode);
        });
    }

    #endregion

    #region Navigation Functions

    private async void OnConnectAndNavigateToWiFiClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is UASDeviceInfo device)
        {
            try
            {
                _logger.LogInformation("Connecting to WiFi device and navigating to control page: {DeviceName}", device.Name);
                
                button.IsEnabled = false;
                button.Text = "Connecting...";
                
                var success = await _deviceService.ConnectToDeviceAsync(device.IpAddress, device.Port);
                
                if (success)
                {
                    _logger.LogInformation("Successfully connected to WiFi device, navigating to control page");
                    await Shell.Current.GoToAsync("wifi-control");
                }
                else
                {
                    ShowStatusToast("Failed to connect to device", ToastType.Error);
                    _logger.LogWarning("Failed to connect to WiFi device {DeviceName}", device.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to WiFi device");
                ShowStatusToast("Connection error", ToastType.Error);
            }
            finally
            {
                button.IsEnabled = true;
                button.Text = "Connect";
            }
        }
    }

    private async void OnConnectManualAndNavigateToWiFiClicked(object sender, EventArgs e)
    {
        try
        {
            var ipAddress = IpAddressEntry?.Text?.Trim();
            var portText = PortEntry?.Text?.Trim();
            
            if (string.IsNullOrEmpty(ipAddress) || string.IsNullOrEmpty(portText))
            {
                ShowStatusToast("Please enter IP address and port", ToastType.Warning);
                return;
            }
            
            if (!int.TryParse(portText, out var port) || port <= 0 || port > 65535)
            {
                ShowStatusToast("Please enter a valid port number", ToastType.Warning);
                return;
            }
            
            _logger.LogInformation("Manually connecting to WiFi device and navigating: {IP}:{Port}", ipAddress, port);
            
            var button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = false;
                button.Text = "Connecting...";
            }
            
            var success = await _deviceService.ConnectToDeviceAsync(ipAddress, port);
            
            if (success)
            {
                _logger.LogInformation("Successfully connected to manual WiFi device, navigating to control page");
                await Shell.Current.GoToAsync("wifi-control");
            }
            else
            {
                ShowStatusToast($"Failed to connect to {ipAddress}:{port}", ToastType.Error);
                _logger.LogWarning("Failed to connect to manual WiFi device {IP}:{Port}", ipAddress, port);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error with manual WiFi connection");
            ShowStatusToast("Manual connection error", ToastType.Error);
        }
        finally
        {
            if (sender is Button button)
            {
                button.IsEnabled = true;
                button.Text = "Connect";
            }
        }
    }

    private async void OnConnectAndNavigateToComPortClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is ComPortInfo port)
        {
            try
            {
                _logger.LogInformation("Connecting to COM port and navigating to control page: {PortName}", port.PortName);
                
                button.IsEnabled = false;
                button.Text = "Connecting...";
                
                var success = await _comPortService.ConnectAsync(port.PortName);
                
                if (success)
                {
                    _logger.LogInformation("Successfully connected to COM port, navigating to control page");
                    await Shell.Current.GoToAsync("comport-control");
                }
                else
                {
                    ShowStatusToast($"Failed to connect to {port.PortName}", ToastType.Error);
                    _logger.LogWarning("Failed to connect to COM port {PortName}", port.PortName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to COM port");
                ShowStatusToast("COM port connection error", ToastType.Error);
            }
            finally
            {
                button.IsEnabled = true;
                button.Text = "Connect";
            }
        }
    }

    #endregion

    #region COM Port Functions

    private async void OnScanComPortsClicked(object sender, EventArgs e)
    {
        try
        {
            ComPortScanIndicator.IsRunning = true;
            ScanComPortsButton.IsEnabled = false;
            ComPortScanProgressFrame.IsVisible = true;
            
            _comPortScanCts?.Cancel();
            _comPortScanCts = new CancellationTokenSource();
            
            _logger.LogInformation("Scanning for COM ports...");
            ShowStatusToast("Scanning for COM ports...", ToastType.Info);
            
            var ports = await _comPortService.ScanForUasComPortsAsync(_comPortScanCts.Token);
            
            _comPorts.Clear();
            foreach (var port in ports)
            {
                _comPorts.Add(port);
            }
            
            var uasPortCount = ports.Count(p => p.IsUasDevice);
            ShowStatusToast($"Found {ports.Count} COM ports ({uasPortCount} UAS devices)", ToastType.Success);
            _logger.LogInformation("COM port scan complete: {TotalPorts} ports, {UasPorts} UAS devices", 
                ports.Count, uasPortCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning COM ports");
            ShowStatusToast("Failed to scan COM ports", ToastType.Error);
        }
        finally
        {
            ComPortScanIndicator.IsRunning = false;
            ScanComPortsButton.IsEnabled = true;
            
            // Hide progress frame after a short delay to let user see completion
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000); // Show completion for 2 seconds
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ComPortScanProgressFrame.IsVisible = false;
                });
            });
        }
    }
    
    private async void OnComPortSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ComPortInfo port)
        {
            _logger.LogDebug("COM port selected: {PortName}", port.PortName);
        }
    }
    
    private async void OnConnectComPortClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is ComPortInfo port)
        {
            try
            {
                button.IsEnabled = false;
                _logger.LogInformation("Connecting to COM port {PortName}...", port.PortName);
                ShowStatusToast($"Connecting to {port.PortName}...", ToastType.Info);
                
                var success = await _comPortService.ConnectAsync(port.PortName);
                
                if (success)
                {
                    ShowStatusToast($"Connected to {port.PortName}", ToastType.Success);
                    _logger.LogInformation("Successfully connected to COM port {PortName}", port.PortName);
                }
                else
                {
                    ShowStatusToast($"Failed to connect to {port.PortName}", ToastType.Error);
                    _logger.LogWarning("Failed to connect to COM port {PortName}", port.PortName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to COM port");
                ShowStatusToast("Connection error", ToastType.Error);
            }
            finally
            {
                button.IsEnabled = true;
            }
        }
    }
    
    private async void OnDisconnectComPortClicked(object sender, EventArgs e)
    {
        try
        {
            await _comPortService.DisconnectAsync();
            // ComPortStatusFrame.IsVisible = false; // Moved to dedicated COM port control page
            ShowStatusToast("Disconnected from COM port", ToastType.Info);
            _logger.LogInformation("Disconnected from COM port");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting COM port");
            ShowStatusToast("Disconnection error", ToastType.Error);
        }
    }
    
    private async void OnReadConfigClicked(object sender, EventArgs e)
    {
        try
        {
            // ReadConfigButton.IsEnabled = false; // Moved to dedicated COM port control page
            ShowStatusToast("Reading device configuration...", ToastType.Info);
            
            var config = await _comPortService.ReadConfigurationAsync();
            
            if (config != null)
            {
                var message = $"Device: {config.DeviceName ?? "Unknown"}\n" +
                             $"Mode: {config.Mode}\n" +
                             $"Sampling Rate: {config.SamplingRate} Hz\n" +
                             $"Gain: {config.Gain}";
                
                await DisplayAlert("Device Configuration", message, "OK");
                _logger.LogInformation("Successfully read device configuration");
            }
            else
            {
                ShowStatusToast("Failed to read configuration", ToastType.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading configuration");
            ShowStatusToast("Read error", ToastType.Error);
        }
        finally
        {
            // ReadConfigButton.IsEnabled = true; // Moved to dedicated COM port control page
        }
    }
    
    private async void OnWriteConfigClicked(object sender, EventArgs e)
    {
        try
        {
            // For now, show a simple dialog - later can expand to full configuration UI
            var result = await DisplayAlert("Write Configuration", 
                "This will write a test configuration to the device. Continue?", "Yes", "No");
            
            if (result)
            {
                // WriteConfigButton.IsEnabled = false; // Moved to dedicated COM port control page
                ShowStatusToast("Writing configuration...", ToastType.Info);
                
                var config = new DeviceConfiguration
                {
                    DeviceName = "UAS-WAND-001",
                    SamplingRate = 1000,
                    Mode = MeasurementMode.Continuous,
                    Gain = 1
                };
                
                var success = await _comPortService.ConfigureDeviceAsync(config);
                
                if (success)
                {
                    ShowStatusToast("Configuration written successfully", ToastType.Success);
                    _logger.LogInformation("Successfully wrote device configuration");
                }
                else
                {
                    ShowStatusToast("Failed to write configuration", ToastType.Error);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing configuration");
            ShowStatusToast("Write error", ToastType.Error);
        }
        finally
        {
            // WriteConfigButton.IsEnabled = true; // Moved to dedicated COM port control page
        }
    }
    
    private async void OnSendCommandClicked(object sender, EventArgs e)
    {
        try
        {
            var command = await DisplayPromptAsync("Send Command", 
                "Enter command to send:", "ID", keyboard: Keyboard.Text);
            
            if (!string.IsNullOrEmpty(command))
            {
                // SendCommandButton.IsEnabled = false; // Moved to dedicated COM port control page
                ShowStatusToast($"Sending: {command}", ToastType.Info);
                
                var response = await _comPortService.SendCommandAsync(command);
                
                if (!string.IsNullOrEmpty(response))
                {
                    await DisplayAlert("Command Response", response, "OK");
                    _logger.LogInformation("Command sent successfully. Response: {Response}", response);
                }
                else
                {
                    ShowStatusToast("No response received", ToastType.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending command");
            ShowStatusToast("Command error", ToastType.Error);
        }
        finally
        {
            // SendCommandButton.IsEnabled = true; // Moved to dedicated COM port control page
        }
    }
    
    private void OnComPortDiscovered(object? sender, ComPortInfo port)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_comPorts.Any(p => p.PortName == port.PortName))
            {
                _comPorts.Add(port);
                _logger.LogInformation("COM port discovered: {PortName}", port.PortName);
            }
        });
    }
    
    private void OnComPortRemoved(object? sender, ComPortInfo port)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var existingPort = _comPorts.FirstOrDefault(p => p.PortName == port.PortName);
            if (existingPort != null)
            {
                _comPorts.Remove(existingPort);
                _logger.LogInformation("COM port removed: {PortName}", port.PortName);
            }
        });
    }
    
    private void OnComPortConnectionStateChanged(object? sender, bool isConnected)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // COM port connection state changes are now handled by the dedicated COM port control page
            if (isConnected && _comPortService.ConnectedPort != null)
            {
                _logger.LogInformation("COM port connected: {PortName}", _comPortService.ConnectedPort.PortName);
            }
        });
    }
    
    private void OnComPortScanProgressChanged(object? sender, (int current, int total, string status) progress)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    var (current, total, status) = progress;
                    
                    // Update progress bar and percentage
                    if (total > 0)
                    {
                        var percentage = (double)current / total * 100;
                        ComPortScanProgressBar.Progress = percentage / 100.0;
                        ComPortScanPercentLabel.Text = $"{percentage:F0}%";
                    }
                    else
                    {
                        ComPortScanProgressBar.Progress = 0;
                        ComPortScanPercentLabel.Text = "0%";
                    }
                    
                    // Update status text
                    ComPortScanStatusLabel.Text = status;
                    
                    // Show details
                    if (total > 0)
                    {
                        ComPortScanDetailsLabel.Text = $"Ports scanned: {current}/{total}";
                        ComPortScanDetailsLabel.IsVisible = true;
                    }
                    else
                    {
                        ComPortScanDetailsLabel.IsVisible = false;
                    }
                    
                    // Show/hide progress frame based on scan state
                    var isScanning = current < total && total > 0;
                    ComPortScanProgressFrame.IsVisible = isScanning || current == total;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating COM port scan progress UI");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnComPortScanProgressChanged event handler");
        }
    }

    #endregion

}