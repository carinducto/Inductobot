using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Inductobot.Models.Device;
using Inductobot.Models.Commands;

namespace Inductobot.Services.Simulation;

/// <summary>
/// Simulated UAS-WAND device that responds to TCP commands like a real device
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
            _tcpListener = new TcpListener(IPAddress.Any, _port);
            _tcpListener.Start();
            
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _listenerTask = AcceptClientsAsync(_cancellationTokenSource.Token);
            
            IsRunning = true;
            _logger.LogInformation("Simulated UAS-WAND device started on port {Port}", _port);
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
        _logger.LogDebug("Simulated UAS-WAND: Client connected from {Endpoint}", clientEndpoint);
        
        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                var buffer = new byte[4096];
                
                while (!cancellationToken.IsCancellationRequested && client.Connected)
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead == 0)
                        break;
                    
                    var command = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    _logger.LogDebug("Simulated UAS-WAND received command: {Command}", command);
                    
                    var response = ProcessCommand(command);
                    var responseBytes = Encoding.UTF8.GetBytes(response);
                    
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length, cancellationToken);
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
            
            return command.ToUpperInvariant() switch
            {
                var cmd when cmd.Contains("GET") && cmd.Contains("INFO") => HandleGetDeviceInfo(),
                var cmd when cmd.Contains("KEEP") && cmd.Contains("ALIVE") => HandleKeepAlive(),
                var cmd when cmd.Contains("GET") && cmd.Contains("WIFI") => HandleGetWifiSettings(),
                var cmd when cmd.Contains("SET") && cmd.Contains("WIFI") => HandleSetWifiSettings(command),
                var cmd when cmd.Contains("RESTART") && cmd.Contains("WIFI") => HandleRestartWifi(),
                var cmd when cmd.Contains("START") && cmd.Contains("SCAN") => HandleStartScan(command),
                var cmd when cmd.Contains("GET") && cmd.Contains("SCAN") => HandleGetScanStatus(),
                var cmd when cmd.Contains("STOP") && cmd.Contains("SCAN") => HandleStopScan(),
                var cmd when cmd.Contains("GET") && cmd.Contains("MEASUREMENT") => HandleGetMeasurement(),
                var cmd when cmd.Contains("GET") && cmd.Contains("LIVE") => HandleGetLiveReading(command),
                var cmd when cmd.Contains("SLEEP") => HandleSleep(),
                _ => JsonSerializer.Serialize(new { success = false, error = "Unknown command", code = "UNKNOWN_COMMAND" })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command: {Command}", command);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message, code = "PROCESSING_ERROR" });
        }
    }

    private string HandleGetDeviceInfo()
    {
        return JsonSerializer.Serialize(new
        {
            success = true,
            data = _deviceInfo
        });
    }

    private string HandleKeepAlive()
    {
        return JsonSerializer.Serialize(new
        {
            success = true,
            data = new CodedResponse { Code = 0, Message = "Device alive" }
        });
    }

    private string HandleGetWifiSettings()
    {
        return JsonSerializer.Serialize(new
        {
            success = true,
            data = _wifiConfig
        });
    }

    private string HandleSetWifiSettings(string command)
    {
        // Simulate WiFi settings update
        return JsonSerializer.Serialize(new
        {
            success = true,
            data = new CodedResponse { Code = 0, Message = "WiFi settings updated" }
        });
    }

    private string HandleRestartWifi()
    {
        return JsonSerializer.Serialize(new
        {
            success = true,
            data = new CodedResponse { Code = 0, Message = "WiFi restarted" }
        });
    }

    private string HandleStartScan(string command)
    {
        _isScanning = true;
        return JsonSerializer.Serialize(new
        {
            success = true,
            data = new ScanStatus
            {
                Status = 1, // 1 = scanning
                Progress = 0,
                Message = "Scan started",
                TotalPoints = 1000
            }
        });
    }

    private string HandleGetScanStatus()
    {
        var progress = _isScanning ? Random.Shared.Next(0, 101) : 100;
        if (progress >= 100)
            _isScanning = false;
            
        return JsonSerializer.Serialize(new
        {
            success = true,
            data = new ScanStatus
            {
                Status = _isScanning ? 1 : 0, // 1 = scanning, 0 = completed
                Progress = progress,
                Message = _isScanning ? "Scanning in progress" : "Scan completed",
                TotalPoints = 1000
            }
        });
    }

    private string HandleStopScan()
    {
        _isScanning = false;
        return JsonSerializer.Serialize(new
        {
            success = true,
            data = new CodedResponse { Code = 0, Message = "Scan stopped" }
        });
    }

    private string HandleGetMeasurement()
    {
        return JsonSerializer.Serialize(new
        {
            success = true,
            data = new
            {
                timestamp = DateTime.Now,
                measurements = new object[]
                {
                    new { sensor = "temperature", value = 23.5, unit = "°C" },
                    new { sensor = "humidity", value = 45.2, unit = "%" },
                    new { sensor = "signal", value = Random.Shared.Next(50, 100), unit = "dBm" }
                }
            }
        });
    }

    private string HandleGetLiveReading(string command)
    {
        var dataPoints = Enumerable.Range(0, 100)
            .Select(i => Random.Shared.NextDouble() * 100)
            .ToArray();
            
        return JsonSerializer.Serialize(new
        {
            success = true,
            data = new
            {
                startIndex = 0,
                numPoints = dataPoints.Length,
                data = dataPoints,
                timestamp = DateTime.Now
            }
        });
    }

    private string HandleSleep()
    {
        return JsonSerializer.Serialize(new
        {
            success = true,
            data = new CodedResponse { Code = 0, Message = "Device entering sleep mode" }
        });
    }

    private static string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var localIP = host.AddressList
                .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip));
            
            return localIP?.ToString() ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _tcpListener?.Stop();
        _cancellationTokenSource?.Dispose();
    }
}