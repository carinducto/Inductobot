using Inductobot.Abstractions.Services;
using Inductobot.Models.Device;
using Inductobot.Services.Api;
using Microsoft.Extensions.Logging;

namespace Inductobot.Services.Business;

/// <summary>
/// HTTP-based UAS-WAND device service with detailed connection progress reporting.
/// Provides user-friendly status messages and error handling for better UX.
/// </summary>
public class UasWandHttpDeviceService : IUasWandDeviceService
{
    private readonly UasWandHttpApiService _apiService;
    private readonly IConfigurationService _config;
    private readonly ILogger<UasWandHttpDeviceService> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    
    private ConnectionState _connectionState = ConnectionState.Disconnected;
    private UASDeviceInfo? _currentDevice;
    private string _lastConnectionError = "";
    
    public bool IsConnected => _connectionState == ConnectionState.Connected;
    public ConnectionState ConnectionState => _connectionState;
    public UASDeviceInfo? CurrentDevice => _currentDevice;
    public string LastConnectionError => _lastConnectionError;
    
    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    public event EventHandler<string>? ConnectionProgressChanged;
    
    public UasWandHttpDeviceService(UasWandHttpApiService apiService, IConfigurationService config, ILogger<UasWandHttpDeviceService> logger)
    {
        _apiService = apiService;
        _config = config;
        _logger = logger;
    }
    
    public async Task<bool> ConnectToDeviceAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
    {
        if (!await _connectionLock.WaitAsync(100, cancellationToken))
        {
            ReportProgress("⏳ Another connection attempt is in progress...");
            return false;
        }
        
        try
        {
            return await ConnectInternalAsync(ipAddress, port, cancellationToken);
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    
    private async Task<bool> ConnectInternalAsync(string ipAddress, int port, CancellationToken cancellationToken)
    {
        _lastConnectionError = "";
        
        try
        {
            ReportProgress($"🔍 Connecting to UAS-WAND at {ipAddress}:{port}...");
            UpdateConnectionState(ConnectionState.Connecting);
            
            // Step 1: Set up API service base URL
            ReportProgress("⚙️ Configuring HTTP client...");
            var baseUrl = $"http://{ipAddress}:{port}";
            _apiService.SetBaseUrl(baseUrl);
            _logger.LogInformation("Attempting to connect to UAS-WAND at {BaseUrl}", baseUrl);
            
            // Step 2: Test basic connectivity with timeout
            ReportProgress("🌐 Testing network connectivity...");
            using var networkTest = new CancellationTokenSource(_config.GetConnectionTimeout());
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, networkTest.Token);
            
            try
            {
                var testResult = await TestNetworkConnectivity(ipAddress, port, combinedCts.Token);
                if (!testResult.IsReachable)
                {
                    _lastConnectionError = $"Cannot reach device at {ipAddress}:{port}. {testResult.ErrorMessage}";
                    ReportProgress($"❌ {_lastConnectionError}");
                    UpdateConnectionState(ConnectionState.Error);
                    return false;
                }
                
                ReportProgress("✅ Network connectivity confirmed");
            }
            catch (OperationCanceledException) when (networkTest.Token.IsCancellationRequested)
            {
                _lastConnectionError = $"Network connectivity test timed out after 5 seconds";
                ReportProgress($"⏱️ {_lastConnectionError}");
                UpdateConnectionState(ConnectionState.Error);
                return false;
            }
            
            // Step 3: Test HTTP endpoint availability  
            ReportProgress("🔗 Testing HTTP endpoint...");
            using var httpTest = new CancellationTokenSource(_config.GetConnectionTimeout());
            using var httpCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, httpTest.Token);
            
            try
            {
                var pingResult = await _apiService.KeepAliveAsync(httpCts.Token);
                if (!pingResult.IsSuccess)
                {
                    _lastConnectionError = GetDetailedErrorMessage(pingResult.ErrorCode, pingResult.Message, ipAddress, port);
                    ReportProgress($"❌ {_lastConnectionError}");
                    UpdateConnectionState(ConnectionState.Error);
                    return false;
                }
                
                ReportProgress("✅ HTTP endpoint responding");
            }
            catch (OperationCanceledException) when (httpTest.Token.IsCancellationRequested)
            {
                _lastConnectionError = "HTTP endpoint test timed out after 10 seconds. Device may not be running UAS-WAND software.";
                ReportProgress($"⏱️ {_lastConnectionError}");
                UpdateConnectionState(ConnectionState.Error);
                return false;
            }
            
            // Step 4: Get device information to validate connection
            ReportProgress("📋 Retrieving device information...");
            using var deviceInfoTest = new CancellationTokenSource(_config.GetConnectionTimeout());
            using var deviceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deviceInfoTest.Token);
            
            try
            {
                var deviceInfoResult = await _apiService.GetDeviceInfoAsync(deviceCts.Token);
                if (!deviceInfoResult.IsSuccess || deviceInfoResult.Data == null)
                {
                    _lastConnectionError = GetDetailedErrorMessage(deviceInfoResult.ErrorCode, deviceInfoResult.Message, ipAddress, port);
                    ReportProgress($"❌ {_lastConnectionError}");
                    UpdateConnectionState(ConnectionState.Error);
                    return false;
                }
                
                _currentDevice = deviceInfoResult.Data;
                ReportProgress($"✅ Connected to {_currentDevice.Name} (FW: {_currentDevice.FirmwareVersion})");
            }
            catch (OperationCanceledException) when (deviceInfoTest.Token.IsCancellationRequested)
            {
                _lastConnectionError = "Device information retrieval timed out after 15 seconds. Connection may be unstable.";
                ReportProgress($"⏱️ {_lastConnectionError}");
                UpdateConnectionState(ConnectionState.Error);
                return false;
            }
            
            // Step 5: Connection successful
            UpdateConnectionState(ConnectionState.Connected);
            _logger.LogInformation("Successfully connected to UAS-WAND device: {DeviceName} at {IpAddress}:{Port}", 
                _currentDevice?.Name, ipAddress, port);
            
            ReportProgress($"🎉 Successfully connected to {_currentDevice?.Name ?? "UAS-WAND"}!");
            
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _lastConnectionError = "Connection cancelled by user";
            ReportProgress($"⚠️ {_lastConnectionError}");
            UpdateConnectionState(ConnectionState.Disconnected);
            return false;
        }
        catch (Exception ex)
        {
            _lastConnectionError = $"Unexpected error during connection: {ex.Message}";
            _logger.LogError(ex, "Unexpected error connecting to UAS-WAND at {IpAddress}:{Port}", ipAddress, port);
            ReportProgress($"💥 {_lastConnectionError}");
            UpdateConnectionState(ConnectionState.Error);
            return false;
        }
    }
    
    private async Task<(bool IsReachable, string ErrorMessage)> TestNetworkConnectivity(string ipAddress, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var tcpClient = new System.Net.Sockets.TcpClient();
            await tcpClient.ConnectAsync(ipAddress, port, cancellationToken);
            
            if (tcpClient.Connected)
            {
                return (true, "");
            }
            else
            {
                return (false, "TCP connection failed");
            }
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            return ex.SocketErrorCode switch
            {
                System.Net.Sockets.SocketError.ConnectionRefused => 
                    (false, "Connection refused. Check if device is powered on and port is correct."),
                System.Net.Sockets.SocketError.TimedOut => 
                    (false, "Connection timed out. Check network connection and device IP address."),
                System.Net.Sockets.SocketError.HostUnreachable => 
                    (false, "Host unreachable. Check if device is on the same network."),
                System.Net.Sockets.SocketError.NetworkUnreachable => 
                    (false, "Network unreachable. Check your network configuration."),
                _ => (false, $"Network error: {ex.Message}")
            };
        }
        catch (Exception ex)
        {
            return (false, $"Network test failed: {ex.Message}");
        }
    }
    
    private string GetDetailedErrorMessage(string? errorCode, string? message, string ipAddress, int port)
    {
        return errorCode switch
        {
            "UNAUTHORIZED" => 
                "Authentication failed. UAS-WAND devices require username 'test' and password '0000'.",
            "TIMEOUT" => 
                "Request timed out. Device may be busy or network connection is slow.",
            "HTTP_REQUEST_ERROR" => 
                $"HTTP communication failed. Ensure device at {ipAddress}:{port} is running UAS-WAND software.",
            "HTTP_ERROR" => 
                $"Device returned HTTP error. Check if {ipAddress}:{port} is a UAS-WAND device.",
            "NO_BASE_URL" => 
                "Internal error: API service not configured properly.",
            "PARSE_ERROR" => 
                "Device response format is invalid. Device may not be a UAS-WAND or firmware is incompatible.",
            "UNEXPECTED_ERROR" => 
                $"Unexpected error during communication: {message}",
            _ => !string.IsNullOrEmpty(message) 
                ? $"Connection failed: {message}"
                : $"Unknown error connecting to {ipAddress}:{port}"
        };
    }
    
    public async Task DisconnectAsync()
    {
        await _connectionLock.WaitAsync();
        
        try
        {
            ReportProgress("🔌 Disconnecting from UAS-WAND...");
            UpdateConnectionState(ConnectionState.Disconnecting);
            
            _currentDevice = null;
            _lastConnectionError = "";
            
            UpdateConnectionState(ConnectionState.Disconnected);
            ReportProgress("✅ Disconnected from UAS-WAND");
            
            _logger.LogInformation("Disconnected from UAS-WAND device");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disconnect");
            ReportProgress($"⚠️ Disconnect completed with warnings: {ex.Message}");
            UpdateConnectionState(ConnectionState.Disconnected);
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    
    public async Task<bool> ConnectToDeviceAsync(UASDeviceInfo device, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(device.IpAddress))
        {
            ReportProgress("❌ Device IP address is not specified");
            return false;
        }
        
        return await ConnectToDeviceAsync(device.IpAddress, device.Port, cancellationToken);
    }
    
    public async Task<bool> TestConnectionAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
    {
        try
        {
            var baseUrl = $"http://{ipAddress}:{port}";
            
            // Create a temporary HTTP API service for testing
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var testLogger = loggerFactory.CreateLogger<UasWandHttpApiService>();
            using var testApiService = new UasWandHttpApiService(testLogger);
            testApiService.SetBaseUrl(baseUrl);
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_config.GetConnectionTimeout());
            
            var result = await testApiService.KeepAliveAsync(cts.Token);
            return result.IsSuccess;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<ConnectionHealth> GetConnectionHealthAsync()
    {
        var health = new ConnectionHealth
        {
            IsHealthy = IsConnected,
            ConnectionDuration = TimeSpan.Zero, // TODO: Track actual connection duration
            PacketsSent = 0, // TODO: Track packets sent
            PacketsReceived = 0, // TODO: Track packets received
            PacketsLost = 0
        };
        
        if (IsConnected && _apiService != null)
        {
            try
            {
                var startTime = DateTime.Now;
                using var cts = new CancellationTokenSource(_config.GetKeepAliveInterval());
                var pingResult = await _apiService.KeepAliveAsync(cts.Token);
                health.LastResponseTime = DateTime.Now - startTime;
                
                health.PacketsSent = 1;
                if (pingResult.IsSuccess)
                {
                    health.PacketsReceived = 1;
                    health.IsHealthy = true;
                }
                else
                {
                    health.PacketsLost = 1;
                    health.IsHealthy = false;
                    health.Issues.Add($"Keep-alive failed: {pingResult.Message}");
                }
            }
            catch (Exception ex)
            {
                health.IsHealthy = false;
                health.PacketsLost = 1;
                health.Issues.Add($"Health check failed: {ex.Message}");
            }
        }
        else if (!IsConnected)
        {
            health.Issues.Add("Not connected to UAS-WAND device");
        }
        
        return health;
    }
    
    private void UpdateConnectionState(ConnectionState newState)
    {
        if (_connectionState != newState)
        {
            var oldState = _connectionState;
            _connectionState = newState;
            
            _logger.LogDebug("UAS-WAND connection state changed: {OldState} -> {NewState}", oldState, newState);
            
            try
            {
                ConnectionStateChanged?.Invoke(this, newState);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invoking ConnectionStateChanged event");
            }
        }
    }
    
    private void ReportProgress(string message)
    {
        _logger.LogInformation("UAS-WAND Connection: {Message}", message);
        
        try
        {
            ConnectionProgressChanged?.Invoke(this, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking ConnectionProgressChanged event");
        }
    }
    
    public void Dispose()
    {
        try
        {
            DisconnectAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during dispose");
        }
        
        _connectionLock?.Dispose();
        _apiService?.Dispose();
    }
}