using Inductobot.Models.Device;
using Inductobot.Services.Communication;
using Inductobot.Services.Device;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace Inductobot.Views;

public partial class DeviceConnectionPage : ContentPage
{
    private readonly IDeviceDiscoveryService _discoveryService;
    private readonly ByteSnapTcpClient _tcpClient;
    private readonly ILogger<DeviceConnectionPage> _logger;
    private ObservableCollection<UASDeviceInfo> _devices = new();
    
    public DeviceConnectionPage(
        IDeviceDiscoveryService discoveryService,
        ByteSnapTcpClient tcpClient,
        ILogger<DeviceConnectionPage> logger)
    {
        InitializeComponent();
        _discoveryService = discoveryService;
        _tcpClient = tcpClient;
        _logger = logger;
        
        InitializeUI();
        SubscribeToEvents();
    }
    
    private void InitializeUI()
    {
        DeviceList.ItemsSource = _devices;
        UpdateDeviceCount();
    }
    
    private void SubscribeToEvents()
    {
        _discoveryService.DeviceDiscovered += OnDeviceDiscovered;
        _discoveryService.DeviceRemoved += OnDeviceRemoved;
        _discoveryService.ScanningStateChanged += OnScanningStateChanged;
        _tcpClient.ConnectionStateChanged += OnConnectionStateChanged;
    }
    
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _discoveryService.DeviceDiscovered -= OnDeviceDiscovered;
        _discoveryService.DeviceRemoved -= OnDeviceRemoved;
        _discoveryService.ScanningStateChanged -= OnScanningStateChanged;
        _tcpClient.ConnectionStateChanged -= OnConnectionStateChanged;
    }
    
    private async void OnScanNetworkClicked(object sender, EventArgs e)
    {
        if (_discoveryService.IsScanning)
        {
            _discoveryService.StopScan();
        }
        else
        {
            _devices.Clear();
            await _discoveryService.StartScanAsync();
        }
    }
    
    private void OnAddManualDeviceClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(IpAddressEntry.Text))
        {
            DisplayAlert("Error", "Please enter an IP address", "OK");
            return;
        }
        
        if (!int.TryParse(PortEntry.Text, out int port) || port <= 0 || port > 65535)
        {
            DisplayAlert("Error", "Please enter a valid port number (1-65535)", "OK");
            return;
        }
        
        _discoveryService.AddManualDevice(IpAddressEntry.Text.Trim(), port);
        
        IpAddressEntry.Text = string.Empty;
        PortEntry.Text = "80";
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
        try
        {
            StatusLabel.Text = "Connecting...";
            ConnectionInfoLabel.Text = $"{ipAddress}:{port}";
            
            var connected = await _tcpClient.ConnectAsync(ipAddress, port);
            
            if (connected)
            {
                await DisplayAlert("Success", $"Connected to device at {ipAddress}:{port}", "OK");
                
                // Navigate to device control page
                await Shell.Current.GoToAsync("//DeviceControl");
            }
            else
            {
                await DisplayAlert("Error", $"Failed to connect to device at {ipAddress}:{port}", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to device");
            await DisplayAlert("Error", $"Connection error: {ex.Message}", "OK");
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
        if (sender is Frame frame && frame.BindingContext is UASDeviceInfo device)
        {
            var action = await DisplayActionSheet($"{device.Name}", "Cancel", null, "Connect", "Test Connection", "Refresh", "Remove");
            
            switch (action)
            {
                case "Connect":
                    await ConnectToDeviceAsync(device.IpAddress, device.Port);
                    break;
                case "Test Connection":
                    var reachable = await _discoveryService.TestConnectionAsync(device);
                    await DisplayAlert("Connection Test", reachable ? "Device is reachable" : "Device is not reachable", "OK");
                    break;
                case "Refresh":
                    await _discoveryService.RefreshDeviceAsync(device);
                    break;
                case "Remove":
                    _discoveryService.RemoveDevice(device);
                    break;
            }
        }
    }
    
    private void OnDeviceDiscovered(object? sender, UASDeviceInfo device)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_devices.Any(d => d.IpAddress == device.IpAddress && d.Port == device.Port))
            {
                _devices.Add(device);
                UpdateDeviceCount();
            }
        });
    }
    
    private void OnDeviceRemoved(object? sender, UASDeviceInfo device)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var toRemove = _devices.FirstOrDefault(d => d.IpAddress == device.IpAddress && d.Port == device.Port);
            if (toRemove != null)
            {
                _devices.Remove(toRemove);
                UpdateDeviceCount();
            }
        });
    }
    
    private void OnScanningStateChanged(object? sender, bool isScanning)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ScanIndicator.IsRunning = isScanning;
            ScanButton.Text = isScanning ? "Stop Scan" : "Scan Network";
            ScanButton.IsEnabled = true;
        });
    }
    
    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        MainThread.BeginInvokeOnMainThread(() =>
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
        });
    }
    
    private void UpdateDeviceCount()
    {
        DeviceCountLabel.Text = _devices.Count == 1 ? "1 device" : $"{_devices.Count} devices";
    }
}