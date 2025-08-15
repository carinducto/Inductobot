using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Inductobot.Models.Device;
using Inductobot.Models.Commands;

namespace Inductobot.Services.Simulation;

/// <summary>
/// Represents an HTTP-style command received over TCP
/// </summary>
public class HttpCommand
{
    public string Endpoint { get; set; } = "";
    public string Method { get; set; } = "";
    public object? Payload { get; set; }
}

/// <summary>
/// Simulated UAS-WAND device that responds to HTTP-style JSON commands over TCP.
/// Protocol: Expects {"endpoint": "/path", "method": "GET|POST", "payload": {...}} format.
/// Security: Only accepts connections from localhost (127.0.0.1) for safety.
/// </summary>
public class SimulatedUasWandDevice : IDisposable
{
    private readonly ILogger<SimulatedUasWandDevice> _logger;
    private TcpListener? _tcpListener;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _listenerTask;
    private readonly int _port;
    
    // Simulated device state
    private readonly UASDeviceInfo _deviceInfo;
    private bool _isScanning = false;
    private WifiConfiguration _wifiConfig;
    
    public bool IsRunning { get; private set; }
    public int Port => _port;
    public IPAddress IPAddress { get; private set; }

    public SimulatedUasWandDevice(ILogger<SimulatedUasWandDevice> logger, int port = 8080)
    {
        _logger = logger;
        _port = port;
        IPAddress = IPAddress.Any;
        
        // Initialize simulated device info
        _deviceInfo = new UASDeviceInfo
        {
            DeviceId = "SIM-001",
            Name = "UAS-WAND_Simulator",
            IpAddress = GetLocalIPAddress(),
            Port = _port,
            FirmwareVersion = "3.9.0-sim",
            SerialNumber = "SIMULATOR001",
            IsOnline = true,
            LastSeen = DateTime.Now
        };
        
        _wifiConfig = new WifiConfiguration
        {
            Ssid = "SimulatedNetwork",
            Enabled = true,
            Channel = 6,
            IpAddress = _deviceInfo.IpAddress
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            _logger.LogWarning("Simulated UAS-WAND device is already running on port {Port}", _port);
            return;
        }

        try
        {
            _tcpListener = new TcpListener(IPAddress.Loopback, _port);
            _tcpListener.Start();
            
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _listenerTask = AcceptClientsAsync(_cancellationTokenSource.Token);
            
            IsRunning = true;
            _logger.LogInformation("Simulated UAS-WAND device started on localhost:{Port} (local connections only)", _port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start simulated UAS-WAND device on port {Port}", _port);
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
            _tcpListener?.Stop();
            
            if (_listenerTask != null)
            {
                await _listenerTask;
            }
            
            IsRunning = false;
            _logger.LogInformation("Simulated UAS-WAND device stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping simulated UAS-WAND device");
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _listenerTask = null;
        }
    }

    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _tcpListener != null)
        {
            try
            {
                var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                _ = Task.Run(async () => await HandleClientAsync(tcpClient, cancellationToken), cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                // Expected when stopping the listener
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting client connection");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var clientEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
        _logger.LogDebug("Simulated UAS-WAND: Client connected from {Endpoint} (localhost only)", clientEndpoint);
        
        // Verify connection is from localhost for security
        if (client.Client.RemoteEndPoint is IPEndPoint ipEndpoint)
        {
            if (!IPAddress.IsLoopback(ipEndpoint.Address))
            {
                _logger.LogWarning("Simulated UAS-WAND: Rejecting non-localhost connection from {Address}", ipEndpoint.Address);
                client.Close();
                return;
            }
        }
        
        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                while (!cancellationToken.IsCancellationRequested && client.Connected)
                {
                    // Read data length prefix (4 bytes)
                    var lengthBytes = new byte[4];
                    var bytesRead = await stream.ReadAsync(lengthBytes, 0, 4, cancellationToken);
                    if (bytesRead == 0)
                        break;
                    
                    if (bytesRead != 4)
                    {
                        _logger.LogWarning("Incomplete length prefix received from client {Endpoint}", clientEndpoint);
                        break;
                    }
                    
                    var dataLength = BitConverter.ToInt32(lengthBytes);
                    if (dataLength < 0 || dataLength > 10 * 1024 * 1024)
                    {
                        _logger.LogWarning("Invalid data length {Length} from client {Endpoint}", dataLength, clientEndpoint);
                        break;
                    }
                    
                    // Read the actual command data
                    var commandBytes = new byte[dataLength];
                    var totalRead = 0;
                    while (totalRead < dataLength)
                    {
                        var remaining = dataLength - totalRead;
                        var chunkRead = await stream.ReadAsync(commandBytes, totalRead, remaining, cancellationToken);
                        if (chunkRead == 0)
                            break;
                        totalRead += chunkRead;
                    }
                    
                    if (totalRead != dataLength)
                    {
                        _logger.LogWarning("Incomplete command data received from client {Endpoint}", clientEndpoint);
                        break;
                    }
                    
                    var command = Encoding.UTF8.GetString(commandBytes).Trim();
                    _logger.LogInformation("Simulated UAS-WAND received command: {Command}", command);
                    
                    var response = ProcessCommand(command);
                    _logger.LogInformation("Simulated UAS-WAND sending response: {Response}", response);
                    var responseBytes = Encoding.UTF8.GetBytes(response);
                    
                    // Send response length prefix
                    var responseLengthBytes = BitConverter.GetBytes(responseBytes.Length);
                    await stream.WriteAsync(responseLengthBytes, 0, 4, cancellationToken);
                    
                    // Send response data
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length, cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                    
                    _logger.LogDebug("Simulated UAS-WAND sent response: {Response}", response);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client {Endpoint}", clientEndpoint);
        }
        
        _logger.LogDebug("Simulated UAS-WAND: Client {Endpoint} disconnected", clientEndpoint);
    }

    private string ProcessCommand(string command)
    {
        try
        {
            // Simulate processing delay
            Thread.Sleep(50);
            
            // Parse and process HTTP-style JSON command
            var httpCommand = JsonSerializer.Deserialize<HttpCommand>(command);
            if (httpCommand != null)
            {
                return ProcessHttpCommand(httpCommand);
            }
            
            // Invalid command format
            return JsonSerializer.Serialize(new 
            { 
                isSuccess = false, 
                data = (object?)null,
                message = "Invalid command format. Expected HTTP-style JSON command.",
                errorCode = "INVALID_FORMAT",
                timestamp = DateTime.UtcNow,
                extensions = new { }
            });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON command received: {Command}", command);
            return JsonSerializer.Serialize(new 
            { 
                isSuccess = false, 
                data = (object?)null,
                message = "Invalid JSON format in command",
                errorCode = "JSON_PARSE_ERROR",
                timestamp = DateTime.UtcNow,
                extensions = new { }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command: {Command}", command);
            return JsonSerializer.Serialize(new 
            { 
                isSuccess = false, 
                data = (object?)null,
                message = ex.Message,
                errorCode = "PROCESSING_ERROR",
                timestamp = DateTime.UtcNow,
                extensions = new { }
            });
        }
    }

    private string ProcessHttpCommand(HttpCommand httpCommand)
    {
        try
        {
            _logger.LogDebug("Processing HTTP command: {Method} {Endpoint}", httpCommand.Method, httpCommand.Endpoint);
            
            return (httpCommand.Method.ToUpperInvariant(), httpCommand.Endpoint.ToLowerInvariant()) switch
            {
                ("GET", "/info") => HandleGetDeviceInfo(),
                ("GET", "/ping") => HandlePing(),
                ("GET", "/wifi") => HandleGetWifiSettings(),
                ("POST", "/wifi") => HandleSetWifiSettings(httpCommand.Payload),
                ("POST", "/wifi/restart") => HandleRestartWifi(),
                ("POST", "/scan") => HandleStartScan(httpCommand.Payload),
                ("GET", "/scan") => HandleGetScanStatus(),
                ("POST", "/scan/stop") => HandleStopScan(),
                ("GET", "/measurement") => HandleGetMeasurement(),
                ("GET", var endpoint) when endpoint.StartsWith("/live") => HandleGetLiveReading(httpCommand.Endpoint),
                ("POST", "/sleep") => HandleSleep(),
                _ => JsonSerializer.Serialize(new 
                { 
                    isSuccess = false, 
                    data = (object?)null,
                    message = $"Unknown command: {httpCommand.Method} {httpCommand.Endpoint}",
                    errorCode = "UNKNOWN_COMMAND",
                    timestamp = DateTime.UtcNow,
                    extensions = new { }
                })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing HTTP command: {Method} {Endpoint}", httpCommand.Method, httpCommand.Endpoint);
            return JsonSerializer.Serialize(new 
            { 
                isSuccess = false, 
                data = (object?)null,
                message = ex.Message,
                errorCode = "PROCESSING_ERROR",
                timestamp = DateTime.UtcNow,
                extensions = new { }
            });
        }
    }

    private string HandleGetDeviceInfo()
    {
        return JsonSerializer.Serialize(new
        {
            isSuccess = true,
            data = _deviceInfo,
            message = "",
            errorCode = "",
            timestamp = DateTime.UtcNow,
            extensions = new { }
        });
    }

    private string HandleKeepAlive()
    {
        return JsonSerializer.Serialize(new
        {
            isSuccess = true,
            data = new CodedResponse { Code = 0, Message = "Device alive" },
            message = "",
            errorCode = "",
            timestamp = DateTime.UtcNow,
            extensions = new { }
        });
    }

    private string HandleGetWifiSettings()
    {
        return JsonSerializer.Serialize(new
        {
            isSuccess = true,
            data = _wifiConfig,
            message = "",
            errorCode = "",
            timestamp = DateTime.UtcNow,
            extensions = new { }
        });
    }

    private string HandleSetWifiSettings(object? payload)
    {
        // Simulate WiFi settings update
        _logger.LogDebug("WiFi settings update requested with payload: {Payload}", payload);
        return JsonSerializer.Serialize(new
        {
            isSuccess = true,
            data = new CodedResponse { Code = 0, Message = "WiFi settings updated" },
            message = "",
            errorCode = "",
            timestamp = DateTime.UtcNow,
            extensions = new { }
        });
    }

    private string HandleRestartWifi()
    {
        return JsonSerializer.Serialize(new
        {
            isSuccess = true,
            data = new CodedResponse { Code = 0, Message = "WiFi restarted" },
            message = "",
            errorCode = "",
            timestamp = DateTime.UtcNow,
            extensions = new { }
        });
    }

    private string HandleStartScan(object? payload)
    {
        _logger.LogDebug("Scan start requested with payload: {Payload}", payload);
        _isScanning = true;
        return JsonSerializer.Serialize(new
        {
            isSuccess = true,
            data = new ScanStatus
            {
                Status = 1, // 1 = scanning
                Progress = 0,
                Message = "Scan started",
                TotalPoints = 1000
            },
            message = "",
            errorCode = "",
            timestamp = DateTime.UtcNow,
            extensions = new { }
        });
    }

    private string HandleGetScanStatus()
    {
        var progress = _isScanning ? Random.Shared.Next(0, 101) : 100;
        if (progress >= 100)
            _isScanning = false;
            
        return JsonSerializer.Serialize(new
        {
            isSuccess = true,
            data = new ScanStatus
            {
                Status = _isScanning ? 1 : 0, // 1 = scanning, 0 = completed
                Progress = progress,
                Message = _isScanning ? "Scanning in progress" : "Scan completed",
                TotalPoints = 1000
            },
            message = "",
            errorCode = "",
            timestamp = DateTime.UtcNow,
            extensions = new { }
        });
    }

    private string HandleStopScan()
    {
        _isScanning = false;
        return JsonSerializer.Serialize(new
        {
            isSuccess = true,
            data = new CodedResponse { Code = 0, Message = "Scan stopped" },
            message = "",
            errorCode = "",
            timestamp = DateTime.UtcNow,
            extensions = new { }
        });
    }

    private string HandleGetMeasurement()
    {
        return JsonSerializer.Serialize(new
        {
            isSuccess = true,
            data = new
            {
                timestamp = DateTime.UtcNow,
                measurements = new object[]
                {
                    new { sensor = "temperature", value = 23.5, unit = "°C" },
                    new { sensor = "humidity", value = 45.2, unit = "%" },
                    new { sensor = "signal", value = Random.Shared.Next(50, 100), unit = "dBm" }
                }
            },
            message = "",
            errorCode = "",
            timestamp = DateTime.UtcNow,
            extensions = new { }
        });
    }

    private string HandleGetLiveReading(string endpoint)
    {
        // Parse query parameters from endpoint (e.g., /live?startIndex=0&numPoints=100)
        var startIndex = 0;
        var numPoints = 100;
        
        if (endpoint.Contains('?'))
        {
            var queryString = endpoint.Split('?')[1];
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
        
        var dataPoints = Enumerable.Range(startIndex, numPoints)
            .Select(i => Random.Shared.NextDouble() * 100)
            .ToArray();
            
        return JsonSerializer.Serialize(new
        {
            isSuccess = true,
            data = new
            {
                startIndex = startIndex,
                numPoints = dataPoints.Length,
                data = dataPoints,
                timestamp = DateTime.UtcNow
            },
            message = "",
            errorCode = "",
            timestamp = DateTime.UtcNow,
            extensions = new { }
        });
    }

    private string HandlePing()
    {
        return JsonSerializer.Serialize(new
        {
            isSuccess = true,
            data = new CodedResponse { Code = 0, Message = "pong" },
            message = "",
            errorCode = "",
            timestamp = DateTime.UtcNow,
            extensions = new { }
        });
    }

    private string HandleSleep()
    {
        return JsonSerializer.Serialize(new
        {
            isSuccess = true,
            data = new CodedResponse { Code = 0, Message = "Device entering sleep mode" },
            message = "",
            errorCode = "",
            timestamp = DateTime.UtcNow,
            extensions = new { }
        });
    }

    private static string GetLocalIPAddress()
    {
        // Always return localhost for security - simulator only accepts local connections
        return "127.0.0.1";
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _tcpListener?.Stop();
        _cancellationTokenSource?.Dispose();
    }
}