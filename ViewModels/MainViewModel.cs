using System.Windows.Input;
using Inductobot.Models.Device;
using Inductobot.Services.Communication;
using Inductobot.Services.Core;
using Inductobot.Services.Device;

namespace Inductobot.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly ITcpDeviceService _tcpDeviceService;
    private readonly IDeviceDiscoveryService _deviceDiscoveryService;
    
    private ConnectionState _connectionState;
    private string _statusMessage = "Ready";
    private UASDeviceInfo? _selectedDevice;
    
    public ConnectionState ConnectionState
    {
        get => _connectionState;
        set => SetProperty(ref _connectionState, value);
    }
    
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }
    
    public UASDeviceInfo? SelectedDevice
    {
        get => _selectedDevice;
        set => SetProperty(ref _selectedDevice, value);
    }
    
    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand DiscoverDevicesCommand { get; }
    public ICommand NavigateToMeasurementsCommand { get; }
    public ICommand NavigateToTestingCommand { get; }
    public ICommand NavigateToSettingsCommand { get; }
    
    public MainViewModel(
        INavigationService navigationService,
        IMessagingService messagingService,
        ITcpDeviceService tcpDeviceService,
        IDeviceDiscoveryService deviceDiscoveryService)
        : base(navigationService, messagingService)
    {
        _tcpDeviceService = tcpDeviceService;
        _deviceDiscoveryService = deviceDiscoveryService;
        
        Title = "Inductobot Dashboard";
        
        ConnectCommand = new Command(async () => await ConnectToDeviceAsync(), () => !IsBusy && SelectedDevice != null);
        DisconnectCommand = new Command(async () => await DisconnectFromDeviceAsync(), () => !IsBusy && _tcpDeviceService.IsConnected);
        DiscoverDevicesCommand = new Command(async () => await DiscoverDevicesAsync(), () => !IsBusy);
        NavigateToMeasurementsCommand = new Command(async () => await NavigateToMeasurementsAsync(), () => _tcpDeviceService.IsConnected);
        NavigateToTestingCommand = new Command(async () => await NavigateToTestingAsync(), () => _tcpDeviceService.IsConnected);
        NavigateToSettingsCommand = new Command(async () => await NavigateToSettingsAsync());
        
        // Subscribe to connection state changes
        _tcpDeviceService.ConnectionStateChanged += OnConnectionStateChanged;
    }
    
    public override async Task InitializeAsync(Dictionary<string, object>? parameters = null)
    {
        await base.InitializeAsync(parameters);
        ConnectionState = _tcpDeviceService.ConnectionState;
    }
    
    private async Task ConnectToDeviceAsync()
    {
        if (SelectedDevice == null)
            return;
        
        await ExecuteAsync(async () =>
        {
            StatusMessage = $"Connecting to {SelectedDevice.Name}...";
            var result = await _tcpDeviceService.ConnectAsync(SelectedDevice);
            
            if (result)
            {
                StatusMessage = $"Connected to {SelectedDevice.Name}";
                MessagingService.Send(new DeviceConnectedMessage(SelectedDevice.DeviceId, SelectedDevice.Name));
            }
            else
            {
                StatusMessage = "Connection failed";
            }
        }, "Connecting to device...");
    }
    
    private async Task DisconnectFromDeviceAsync()
    {
        await ExecuteAsync(async () =>
        {
            await _tcpDeviceService.DisconnectAsync();
            StatusMessage = "Disconnected";
            
            if (SelectedDevice != null)
            {
                MessagingService.Send(new DeviceDisconnectedMessage(SelectedDevice.DeviceId));
            }
        });
    }
    
    private async Task DiscoverDevicesAsync()
    {
        await ExecuteAsync(async () =>
        {
            StatusMessage = "Discovering devices...";
            await _deviceDiscoveryService.StartScanAsync();
            
            var devices = _deviceDiscoveryService.DiscoveredDevices;
            if (devices.Any())
            {
                SelectedDevice = devices.FirstOrDefault();
                StatusMessage = $"Found {devices.Count} device(s)";
            }
            else
            {
                StatusMessage = "No devices found";
            }
        }, "Scanning for devices...");
    }
    
    private async Task NavigateToMeasurementsAsync()
    {
        try
        {
            await NavigationService.NavigateToAsync("measurements", 
                new Dictionary<string, object> { { "deviceId", SelectedDevice?.DeviceId ?? "" } });
        }
        catch (Exception ex)
        {
            MessagingService.Send(new ErrorMessage("Navigation Failed", $"Navigation operation failed: {ex.Message}", ex));
        }
    }
    
    private async Task NavigateToTestingAsync()
    {
        try
        {
            await NavigationService.NavigateToAsync("testing",
                new Dictionary<string, object> { { "deviceId", SelectedDevice?.DeviceId ?? "" } });
        }
        catch (Exception ex)
        {
            MessagingService.Send(new ErrorMessage("Navigation Failed", $"Navigation operation failed: {ex.Message}", ex));
        }
    }
    
    private async Task NavigateToSettingsAsync()
    {
        try
        {
            await NavigationService.NavigateToAsync("settings");
        }
        catch (Exception ex)
        {
            MessagingService.Send(new ErrorMessage("Navigation Failed", $"Navigation operation failed: {ex.Message}", ex));
        }
    }
    
    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        ConnectionState = state;
        StatusMessage = state switch
        {
            ConnectionState.Connected => "Connected",
            ConnectionState.Connecting => "Connecting...",
            ConnectionState.Disconnected => "Disconnected",
            ConnectionState.Error => "Connection error",
            _ => state.ToString()
        };
        
        // Update command availability
        ((Command)ConnectCommand).ChangeCanExecute();
        ((Command)DisconnectCommand).ChangeCanExecute();
        ((Command)NavigateToMeasurementsCommand).ChangeCanExecute();
        ((Command)NavigateToTestingCommand).ChangeCanExecute();
    }
}