using Inductobot.Abstractions.Communication;
using Inductobot.Abstractions.Services;
using Inductobot.Models.Device;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Inductobot.Services.Business;

/// <summary>
/// High-level business logic service for UAS-WAND device management
/// </summary>
public class UasWandDeviceService : IUasWandDeviceService, IDisposable
{
    private readonly IUasWandTransport _transport;
    private readonly IUasWandApiService _apiService;
    private readonly IConfigurationService _config;
    private readonly ILogger<UasWandDeviceService> _logger;
    private readonly Stopwatch _connectionTimer = new();
    
    private int _packetsSent = 0;
    private int _packetsReceived = 0;
    private int _packetsLost = 0;
    private DateTime? _lastResponseTime;
    
    public ConnectionState ConnectionState => _transport.ConnectionState;
    public bool IsConnected => _transport.IsConnected;
    public UASDeviceInfo? CurrentDevice => _transport.CurrentDevice;
    
    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    public event EventHandler<string>? ConnectionProgressChanged;
    
    public UasWandDeviceService(
        IUasWandTransport transport,
        IUasWandApiService apiService,
        IConfigurationService config,
        ILogger<UasWandDeviceService> logger)
    {
        _transport = transport;
        _apiService = apiService;
        _config = config;
        _logger = logger;
        
        _transport.ConnectionStateChanged += OnTransportConnectionStateChanged;
    }
    
    public async Task<bool> ConnectToDeviceAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to connect to UAS-WAND device at {IpAddress}:{Port}", ipAddress, port);
        
        var connected = await _transport.ConnectAsync(ipAddress, port, cancellationToken);
        
        if (connected)
        {
            _connectionTimer.Restart();
            ResetConnectionStats();
            
            // Validate connection by getting device info
            try
            {
                var deviceInfo = await _apiService.GetDeviceInfoAsync(cancellationToken);
                if (deviceInfo.IsSuccess)
                {
                    _packetsReceived++;
                    _lastResponseTime = DateTime.Now;
                    _logger.LogInformation("Successfully connected and validated UAS-WAND device: {DeviceName}", 
                        deviceInfo.Data?.Name ?? "Unknown");
                    return true;
                }
                else
                {
                    _packetsLost++;
                    _logger.LogWarning("Connected to device but validation failed: {Error}", deviceInfo.Message);
                }
            }
            catch (Exception ex)
            {
                _packetsLost++;
                _logger.LogError(ex, "Device validation failed after connection");
            }
        }
        
        _logger.LogWarning("Failed to connect to UAS-WAND device at {IpAddress}:{Port}", ipAddress, port);
        return false;
    }
    
    public async Task<bool> ConnectToDeviceAsync(UASDeviceInfo device, CancellationToken cancellationToken = default)
    {
        return await ConnectToDeviceAsync(device.IpAddress, device.Port, cancellationToken);
    }
    
    public async Task DisconnectAsync()
    {
        _logger.LogInformation("Disconnecting from UAS-WAND device");
        
        _connectionTimer.Stop();
        await _transport.DisconnectAsync();
        
        _logger.LogInformation("Disconnected from UAS-WAND device");
    }
    
    public async Task<bool> TestConnectionAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Testing connection to UAS-WAND device at {IpAddress}:{Port}", ipAddress, port);
        
        // For ESP32 UAS devices, test HTTPS connectivity with TLS 1.2 compatibility
        var httpsResult = await TestHttpsConnectionAsync(ipAddress, port, cancellationToken);
        if (httpsResult)
        {
            return true;
        }
        
        // Fallback to TCP transport for legacy compatibility
        return await TestTcpConnectionAsync(ipAddress, port, cancellationToken);
    }
    
    private async Task<bool> TestHttpsConnectionAsync(string ipAddress, int port, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Testing HTTPS connection to ESP32 UAS device at {IpAddress}:{Port}", ipAddress, port);
            
            // Create temporary HTTPS API service for testing with ESP32 TLS 1.2 compatibility
            var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
            var testLogger = loggerFactory.CreateLogger<Api.UasWandHttpApiService>();
            using var httpsApiService = new Api.UasWandHttpApiService(testLogger);
            
            // Use HTTPS with ESP32 TLS compatibility
            var httpsBaseUrl = $"https://{ipAddress}:{port}";
            httpsApiService.SetBaseUrl(httpsBaseUrl);
            
            var response = await httpsApiService.GetDeviceInfoAsync(cancellationToken);
            if (response.IsSuccess)
            {
                _logger.LogInformation("HTTPS connection successful to ESP32 UAS device at {IpAddress}:{Port}", ipAddress, port);
                return true;
            }
            
            _logger.LogDebug("HTTPS connection failed for ESP32 UAS device at {IpAddress}:{Port}: {ErrorCode} - {Message}", 
                ipAddress, port, response.ErrorCode, response.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "HTTPS connection test failed for ESP32 UAS device at {IpAddress}:{Port}", ipAddress, port);
            return false;
        }
    }
    
    private async Task<bool> TestTcpConnectionAsync(string ipAddress, int port, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Testing TCP connection to {IpAddress}:{Port}", ipAddress, port);
            
            // Create a temporary transport for testing  
            var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
            var testLogger = loggerFactory.CreateLogger<Communication.UasWandTcpTransport>();
            using var testTransport = new Communication.UasWandTcpTransport(testLogger, _config);
            
            _logger.LogDebug("Attempting to connect to {IpAddress}:{Port}...", ipAddress, port);
            var connected = await testTransport.ConnectAsync(ipAddress, port, cancellationToken);
            _logger.LogDebug("Connect result for {IpAddress}:{Port}: {Connected}", ipAddress, port, connected);
            
            if (connected)
            {
                // Send a simple ping command
                try
                {
                    _logger.LogDebug("Sending ping command to {IpAddress}:{Port}...", ipAddress, port);
                    var response = await testTransport.SendCommandAsync("ping", cancellationToken);
                    _logger.LogDebug("Ping response from {IpAddress}:{Port}: {Response}", ipAddress, port, response);
                    _logger.LogInformation("Connection test successful for {IpAddress}:{Port}", ipAddress, port);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Connection test failed during ping for {IpAddress}:{Port}", ipAddress, port);
                }
            }
            else
            {
                _logger.LogDebug("Failed to connect to {IpAddress}:{Port}", ipAddress, port);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Connection test failed for {IpAddress}:{Port}", ipAddress, port);
        }
        
        return false;
    }
    
    public async Task<ConnectionHealth> GetConnectionHealthAsync()
    {
        var health = new ConnectionHealth
        {
            IsHealthy = IsConnected,
            ConnectionDuration = _connectionTimer.Elapsed,
            PacketsSent = _packetsSent,
            PacketsReceived = _packetsReceived,
            PacketsLost = _packetsLost,
            LastResponseTime = _lastResponseTime.HasValue ? DateTime.Now - _lastResponseTime.Value : null
        };
        
        // Check connection health with a keep-alive
        if (IsConnected)
        {
            try
            {
                _packetsSent++;
                var response = await _apiService.KeepAliveAsync();
                
                if (response.IsSuccess)
                {
                    _packetsReceived++;
                    _lastResponseTime = DateTime.Now;
                    health.IsHealthy = true;
                }
                else
                {
                    _packetsLost++;
                    health.IsHealthy = false;
                    health.Issues.Add($"Keep-alive failed: {response.Message}");
                }
            }
            catch (Exception ex)
            {
                _packetsLost++;
                health.IsHealthy = false;
                health.Issues.Add($"Keep-alive exception: {ex.Message}");
            }
        }
        else
        {
            health.Issues.Add("Not connected to device");
        }
        
        // Add health warnings
        if (health.PacketLossRate > 0.1) // > 10% packet loss
        {
            health.Issues.Add($"High packet loss rate: {health.PacketLossRate:P1}");
        }
        
        if (health.LastResponseTime.HasValue && health.LastResponseTime.Value.TotalSeconds > 30)
        {
            health.Issues.Add($"No response for {health.LastResponseTime.Value.TotalSeconds:F1} seconds");
        }
        
        return health;
    }
    
    private void OnTransportConnectionStateChanged(object? sender, ConnectionState state)
    {
        try
        {
            ConnectionStateChanged?.Invoke(this, state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking ConnectionStateChanged event");
        }
    }
    
    private void ResetConnectionStats()
    {
        _packetsSent = 0;
        _packetsReceived = 0;
        _packetsLost = 0;
        _lastResponseTime = null;
    }
    
    public void Dispose()
    {
        try
        {
            _transport.ConnectionStateChanged -= OnTransportConnectionStateChanged;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error unsubscribing from transport events");
        }
        
        _connectionTimer?.Stop();
    }
}