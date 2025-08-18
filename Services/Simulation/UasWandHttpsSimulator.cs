using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Inductobot.Models.Device;
using Inductobot.Models.Commands;
using System.Security.Cryptography.X509Certificates;

namespace Inductobot.Services.Simulation;

/// <summary>
/// HTTPS-based UAS-WAND simulator that matches the real device protocol.
/// Uses HTTPS with Basic Authentication (username: "test", password: "0000").
/// Provides standard HTTP endpoints that return JSON responses.
/// </summary>
public class UasWandHttpsSimulator : IDisposable
{
    private readonly ILogger<UasWandHttpsSimulator> _logger;
    private HttpListener? _httpListener;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _listenerTask;
    private readonly int _port;
    private readonly JsonSerializerOptions _jsonOptions;
    
    // Simulated device state
    private readonly UASDeviceInfo _deviceInfo;
    private bool _isScanning = false;
    private WifiConfiguration _wifiConfig;
    
    // Basic Auth credentials (matches real UAS devices)
    private const string ValidUsername = "test";
    private const string ValidPassword = "0000";
    
    public bool IsRunning { get; private set; }
    public int Port => _port;
    public string BaseUrl => $"http://127.0.0.1:{_port}";

    public UasWandHttpsSimulator(ILogger<UasWandHttpsSimulator> logger, int port = 8080)
    {
        _logger = logger;
        _port = port;
        
        // Initialize JSON options to match HTTP API service expectations - MUST use camelCase!
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        
        // Initialize simulated device info
        _deviceInfo = new UASDeviceInfo
        {
            DeviceId = "SIM-001",
            Name = "UAS-WAND_Simulator",
            IpAddress = "127.0.0.1",
            Port = _port,
            FirmwareVersion = "3.9.0-sim",
            SerialNumber = "SIMULATOR001",
            IsOnline = true,
            LastSeen = DateTime.Now
        };
        
        _wifiConfig = new WifiConfiguration
        {
            Ssid = "SimulatedNetwork",
            Password = "admin", // Add password for proper WiFi configuration
            Enabled = true,
            Channel = 6,
            IpAddress = _deviceInfo.IpAddress
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            _logger.LogWarning("HTTPS UAS-WAND simulator is already running on port {Port}", _port);
            return;
        }

        try
        {
            // Create HTTP listener (HTTP for now, can add HTTPS later)
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _httpListener.Prefixes.Add($"http://localhost:{_port}/");
            
            _logger.LogDebug("🔐 Attempting to start HttpListener on port {Port}...", _port);
            _httpListener.Start();
            _logger.LogDebug("✅ HttpListener started successfully on port {Port}", _port);
            
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _listenerTask = ProcessRequestsAsync(_cancellationTokenSource.Token);
            
            IsRunning = true;
            _logger.LogInformation("HTTPS UAS-WAND simulator started on {BaseUrl} (Basic Auth: {Username}/{Password})", 
                BaseUrl, ValidUsername, ValidPassword);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start HTTPS UAS-WAND simulator on port {Port}", _port);
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (!IsRunning)
            return;

        try
        {
            _cancellationTokenSource?.Cancel();
            _httpListener?.Stop();
            
            if (_listenerTask != null)
            {
                await _listenerTask;
            }
            
            IsRunning = false;
            _logger.LogInformation("HTTPS UAS-WAND simulator stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping HTTPS UAS-WAND simulator");
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _listenerTask = null;
        }
    }

    private async Task ProcessRequestsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _httpListener?.IsListening == true)
        {
            try
            {
                var context = await _httpListener.GetContextAsync();
                _ = Task.Run(async () => await HandleRequestAsync(context, cancellationToken), cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                // Expected when stopping the listener
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting HTTP request");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;
        
        try
        {
            _logger.LogDebug("HTTPS UAS-WAND received: {Method} {Url}", request.HttpMethod, request.Url?.PathAndQuery);
            
            // Check Basic Authentication
            if (!IsAuthenticated(request))
            {
                _logger.LogWarning("HTTPS UAS-WAND: Unauthorized request from {RemoteEndpoint}", request.RemoteEndPoint);
                
                response.StatusCode = 401;
                response.Headers.Add("WWW-Authenticate", "Basic realm=\"UAS-WAND\"");
                
                var errorResponse = CreateErrorResponse("Unauthorized", "UNAUTHORIZED");
                await SendJsonResponseAsync(response, errorResponse);
                return;
            }
            
            // Route the request
            var jsonResponse = ProcessHttpRequest(request);
            await SendJsonResponseAsync(response, jsonResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling HTTPS request");
            
            response.StatusCode = 500;
            var errorResponse = CreateErrorResponse("Internal server error", "SERVER_ERROR");
            await SendJsonResponseAsync(response, errorResponse);
        }
    }

    private bool IsAuthenticated(HttpListenerRequest request)
    {
        var authHeader = request.Headers["Authorization"];
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic "))
        {
            return false;
        }

        try
        {
            var encodedCredentials = authHeader.Substring(6); // Remove "Basic "
            var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
            var parts = credentials.Split(':', 2);
            
            if (parts.Length != 2)
                return false;
                
            return parts[0] == ValidUsername && parts[1] == ValidPassword;
        }
        catch
        {
            return false;
        }
    }

    private string ProcessHttpRequest(HttpListenerRequest request)
    {
        // Simulate processing delay
        Thread.Sleep(50);
        
        var method = request.HttpMethod?.ToUpperInvariant();
        var path = request.Url?.AbsolutePath?.ToLowerInvariant();
        var query = request.Url?.Query;
        
        _logger.LogDebug("Processing: {Method} {Path}{Query}", method, path, query);
        
        return (method, path) switch
        {
            ("GET", "/info") => HandleGetDeviceInfo(),
            ("GET", "/ping") => HandlePing(),
            ("GET", "/wifi") => HandleGetWifiSettings(),
            ("POST", "/wifi") => HandleSetWifiSettings(request),
            ("POST", "/wifi/restart") => HandleRestartWifi(),
            ("POST", "/scan") => HandleStartScan(request),
            ("GET", "/scan") => HandleGetScanStatus(),
            ("POST", "/scan/stop") => HandleStopScan(),
            ("GET", "/measurement") => HandleGetMeasurement(),
            ("GET", var p) when p?.StartsWith("/live") == true => HandleGetLiveReading(request),
            ("POST", "/sleep") => HandleSleep(),
            _ => CreateErrorResponse($"Unknown command: {method} {path}", "UNKNOWN_COMMAND")
        };
    }

    private async Task SendJsonResponseAsync(HttpListenerResponse response, string json)
    {
        response.StatusCode = 200;
        response.ContentType = "application/json";
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        
        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = bytes.Length;
        
        await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        response.OutputStream.Close();
    }

    /// <summary>
    /// Creates a properly formatted ApiResponse that matches the HTTP API service expectations.
    /// CRITICAL: Must create actual ApiResponse<T> instance to ensure proper serialization/deserialization.
    /// </summary>
    private string CreateApiResponse<T>(T data, bool isSuccess = true, string message = "", string errorCode = "")
    {
        // Create actual ApiResponse<T> instance instead of anonymous object
        // This ensures the property names match exactly when serialized/deserialized
        var responseWrapper = new ApiResponse<T>
        {
            IsSuccess = isSuccess,
            Data = data,
            Message = message,
            ErrorCode = errorCode,
            Timestamp = DateTime.UtcNow,
            Extensions = new Dictionary<string, object>()
        };
        
        var json = JsonSerializer.Serialize(responseWrapper, _jsonOptions);
        _logger.LogDebug("HTTP CreateApiResponse - Serialized JSON: {Json}", json);
        return json;
    }

    private string CreateErrorResponse(string message, string errorCode)
    {
        return CreateApiResponse<object?>(null, false, message, errorCode);
    }

    // HTTP endpoint handlers (same logic as TCP version but adapted for HTTP)
    
    private string HandleGetDeviceInfo()
    {
        return CreateApiResponse(_deviceInfo);
    }

    private string HandlePing()
    {
        return CreateApiResponse(new CodedResponse { Code = 0, Message = "pong" });
    }

    private string HandleGetWifiSettings()
    {
        _logger.LogDebug("HTTP HandleGetWifiSettings - Current WiFi config: SSID={Ssid}, Password={Password}, Enabled={Enabled}, Channel={Channel}, IP={IP}", 
            _wifiConfig.Ssid, _wifiConfig.Password, _wifiConfig.Enabled, _wifiConfig.Channel, _wifiConfig.IpAddress);
            
        var response = CreateApiResponse(_wifiConfig);
        _logger.LogDebug("HTTP HandleGetWifiSettings - Sending response: {Response}", response);
        return response;
    }

    private string HandleSetWifiSettings(HttpListenerRequest request)
    {
        // Read request body for WiFi settings
        _logger.LogDebug("WiFi settings update requested");
        return CreateApiResponse(new CodedResponse { Code = 0, Message = "WiFi settings updated" });
    }

    private string HandleRestartWifi()
    {
        return CreateApiResponse(new CodedResponse { Code = 0, Message = "WiFi restarted" });
    }

    private string HandleStartScan(HttpListenerRequest request)
    {
        _logger.LogDebug("Scan start requested");
        _isScanning = true;
        return CreateApiResponse(new ScanStatus
        {
            Status = 1, // 1 = scanning
            Progress = 0,
            Message = "Scan started",
            TotalPoints = 1000
        });
    }

    private string HandleGetScanStatus()
    {
        var progress = _isScanning ? Random.Shared.Next(0, 101) : 100;
        if (progress >= 100)
            _isScanning = false;
            
        return CreateApiResponse(new ScanStatus
        {
            Status = _isScanning ? 1 : 0, // 1 = scanning, 0 = completed
            Progress = progress,
            Message = _isScanning ? "Scanning in progress" : "Scan completed",
            TotalPoints = 1000
        });
    }

    private string HandleStopScan()
    {
        _isScanning = false;
        return CreateApiResponse(new CodedResponse { Code = 0, Message = "Scan stopped" });
    }

    private string HandleGetMeasurement()
    {
        return CreateApiResponse(new
        {
            timestamp = DateTime.UtcNow,
            measurements = new object[]
            {
                new { sensor = "temperature", value = 23.5, unit = "°C" },
                new { sensor = "humidity", value = 45.2, unit = "%" },
                new { sensor = "signal", value = Random.Shared.Next(50, 100), unit = "dBm" }
            }
        });
    }

    private string HandleGetLiveReading(HttpListenerRequest request)
    {
        // Parse query parameters from URL (e.g., /live?startIndex=0&numPoints=100)
        var startIndex = 0;
        var numPoints = 100;
        
        var queryString = request.Url?.Query;
        if (!string.IsNullOrEmpty(queryString) && queryString.StartsWith("?"))
        {
            queryString = queryString.Substring(1);
            var queryParams = queryString.Split('&');
            
            foreach (var param in queryParams)
            {
                var keyValue = param.Split('=');
                if (keyValue.Length == 2)
                {
                    switch (keyValue[0].ToLowerInvariant())
                    {
                        case "startindex":
                            int.TryParse(keyValue[1], out startIndex);
                            break;
                        case "numpoints":
                            int.TryParse(keyValue[1], out numPoints);
                            break;
                    }
                }
            }
        }
        
        if (numPoints <= 0) numPoints = 100;
        
        var dataPoints = Enumerable.Range(startIndex, numPoints)
            .Select(i => Random.Shared.NextDouble() * 100)
            .ToArray();
            
        return CreateApiResponse(new
        {
            startIndex = startIndex,
            numPoints = dataPoints.Length,
            data = dataPoints,
            timestamp = DateTime.UtcNow
        });
    }

    private string HandleSleep()
    {
        return CreateApiResponse(new CodedResponse { Code = 0, Message = "Device entering sleep mode" });
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _httpListener?.Close();
        _cancellationTokenSource?.Dispose();
    }
}