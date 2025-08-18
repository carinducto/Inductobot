using Inductobot.Abstractions.Communication;
using Inductobot.Abstractions.Services;
using Inductobot.Services.Api;
using Inductobot.Services.Business;
using Inductobot.Services.Communication;
using Inductobot.Services.Discovery;
using Inductobot.ViewModels;
using Inductobot.Models.Device;

namespace Inductobot.Extensions;

/// <summary>
/// Extension methods for configuring UAS-WAND services in DI container
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add all UAS-WAND services to the service collection with proper modular separation
    /// </summary>
    public static IServiceCollection AddUasWandServices(this IServiceCollection services)
    {
        // HTTP API Service Layer (matches real UAS-WAND protocol)
        services.AddSingleton<UasWandHttpApiService>();
        services.AddSingleton<IUasWandApiService>(provider => provider.GetRequiredService<UasWandHttpApiService>());
        
        // Business Logic Layer (HTTP-based with detailed progress reporting)
        services.AddSingleton<IUasWandDeviceService, UasWandHttpDeviceService>();
        
        // Discovery Service (parallel to device service)
        services.AddSingleton<IUasWandDiscoveryService, UasWandDiscoveryService>();
        
        // UI Layer (depends only on high-level services)
        // NOTE: Singleton ensures connection state persists across page navigation
        services.AddSingleton<UasWandControlViewModel>();
        
        return services;
    }
    
    /// <summary>
    /// Add UAS-WAND services for testing with mock implementations
    /// </summary>
    public static IServiceCollection AddUasWandServicesForTesting(this IServiceCollection services)
    {
        // Register mock implementations for testing
        services.AddSingleton<IUasWandTransport, MockUasWandTransport>();
        services.AddSingleton<IUasWandApiService, MockUasWandApiService>();
        services.AddSingleton<IUasWandDeviceService, MockUasWandDeviceService>();
        services.AddSingleton<IUasWandDiscoveryService, MockUasWandDiscoveryService>();
        
        services.AddSingleton<UasWandControlViewModel>();
        
        return services;
    }
}

// Mock implementations for testing (basic stubs)
public class MockUasWandTransport : IUasWandTransport
{
    public Models.Device.ConnectionState ConnectionState => Models.Device.ConnectionState.Connected;
    public bool IsConnected => true;
    public Models.Device.UASDeviceInfo? CurrentDevice => null;
    public event EventHandler<Models.Device.ConnectionState>? ConnectionStateChanged;
    
    public Task<bool> ConnectAsync(string address, int port, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<bool> ConnectAsync(Models.Device.UASDeviceInfo device, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task DisconnectAsync() => Task.CompletedTask;
    public Task<byte[]> SendRawDataAsync(byte[] data, CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<byte>());
    public Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default) => Task.FromResult("OK");
    public void Dispose() { }
}

public class MockUasWandApiService : IUasWandApiService
{
    public Task<Models.Commands.ApiResponse<Models.Device.UASDeviceInfo>> GetDeviceInfoAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Models.Commands.ApiResponse<Models.Device.UASDeviceInfo>.Success(new Models.Device.UASDeviceInfo { Name = "UAS-WAND_MockDevice" }));
    
    public Task<Models.Commands.ApiResponse<Models.Commands.CodedResponse>> KeepAliveAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Models.Commands.ApiResponse<Models.Commands.CodedResponse>.Success(new Models.Commands.CodedResponse()));
    
    public Task<Models.Commands.ApiResponse<Models.Device.WifiConfiguration>> GetWifiSettingsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Models.Commands.ApiResponse<Models.Device.WifiConfiguration>.Success(new Models.Device.WifiConfiguration()));
    
    public Task<Models.Commands.ApiResponse<Models.Commands.CodedResponse>> SetWifiSettingsAsync(Models.Device.WifiSettings settings, CancellationToken cancellationToken = default)
        => Task.FromResult(Models.Commands.ApiResponse<Models.Commands.CodedResponse>.Success(new Models.Commands.CodedResponse()));
    
    public Task<Models.Commands.ApiResponse<Models.Commands.CodedResponse>> RestartWifiAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Models.Commands.ApiResponse<Models.Commands.CodedResponse>.Success(new Models.Commands.CodedResponse()));
    
    public Task<Models.Commands.ApiResponse<Models.Commands.CodedResponse>> SleepAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Models.Commands.ApiResponse<Models.Commands.CodedResponse>.Success(new Models.Commands.CodedResponse()));
    
    public Task<Models.Commands.ApiResponse<Models.Commands.ScanStatus>> StartScanAsync(Models.Commands.ScanTask task, CancellationToken cancellationToken = default)
        => Task.FromResult(Models.Commands.ApiResponse<Models.Commands.ScanStatus>.Success(new Models.Commands.ScanStatus()));
    
    public Task<Models.Commands.ApiResponse<Models.Commands.ScanStatus>> GetScanStatusAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Models.Commands.ApiResponse<Models.Commands.ScanStatus>.Success(new Models.Commands.ScanStatus()));
    
    public Task<Models.Commands.ApiResponse<Models.Measurements.LiveReadingData>> GetLiveReadingAsync(int startIndex, int numPoints, CancellationToken cancellationToken = default)
        => Task.FromResult(Models.Commands.ApiResponse<Models.Measurements.LiveReadingData>.Success(new Models.Measurements.LiveReadingData()));
    
    public Task<Models.Commands.ApiResponse<Models.Measurements.MeasurementData>> GetMeasurementAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Models.Commands.ApiResponse<Models.Measurements.MeasurementData>.Success(new Models.Measurements.MeasurementData()));
}

public class MockUasWandDeviceService : IUasWandDeviceService
{
    public Models.Device.ConnectionState ConnectionState => Models.Device.ConnectionState.Connected;
    public bool IsConnected => true;
    public Models.Device.UASDeviceInfo? CurrentDevice => null;
    public event EventHandler<Models.Device.ConnectionState>? ConnectionStateChanged;
    public event EventHandler<string>? ConnectionProgressChanged;
    
    public Task<bool> ConnectToDeviceAsync(string ipAddress, int port, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<bool> ConnectToDeviceAsync(Models.Device.UASDeviceInfo device, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task DisconnectAsync() => Task.CompletedTask;
    public Task<bool> TestConnectionAsync(string ipAddress, int port, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<ConnectionHealth> GetConnectionHealthAsync() => Task.FromResult(new ConnectionHealth { IsHealthy = true });
}

public class MockUasWandDiscoveryService : IUasWandDiscoveryService
{
    public IReadOnlyList<Models.Device.UASDeviceInfo> DiscoveredDevices => new List<Models.Device.UASDeviceInfo>();
    public bool IsScanning => false;
    public event EventHandler<Models.Device.UASDeviceInfo>? DeviceDiscovered;
    public event EventHandler<Models.Device.UASDeviceInfo>? DeviceRemoved;
    public event EventHandler<bool>? ScanningStateChanged;
    
    public Task StartScanAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void StopScan() { }
    public Task<bool> TestConnectionAsync(Models.Device.UASDeviceInfo device, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task ForceDiscoverSimulatorAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RefreshDeviceAsync(Models.Device.UASDeviceInfo device, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> AddDeviceManuallyAsync(string ipAddress, int port, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public void RemoveDevice(Models.Device.UASDeviceInfo device) { }
    public void ClearDevices() { }
    public int GetDeviceDiscoveredSubscriberCount() => DeviceDiscovered?.GetInvocationList()?.Length ?? 0;
}