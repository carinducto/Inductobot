using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Inductobot.Models.Commands;
using Inductobot.Models.Device;
using Inductobot.Models.Measurements;
using Microsoft.Extensions.Logging;

namespace Inductobot.Services.Communication;

public class ByteSnapTcpClient : IDisposable
{
    private readonly ILogger<ByteSnapTcpClient> _logger;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    
    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    public event EventHandler<byte[]>? DataReceived;
    
    public ConnectionState ConnectionState { get; private set; } = ConnectionState.Disconnected;
    public UASDeviceInfo? CurrentDevice { get; private set; }
    public bool IsConnected => ConnectionState == ConnectionState.Connected && _tcpClient?.Connected == true;
    
    public ByteSnapTcpClient(ILogger<ByteSnapTcpClient> logger)
    {
        _logger = logger;
    }
    
    public async Task<bool> ConnectAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
    {
        try
        {
            if (IsConnected)
            {
                await DisconnectAsync();
            }
            
            UpdateConnectionState(ConnectionState.Connecting);
            
            _tcpClient = new TcpClient();
            _tcpClient.ReceiveTimeout = 5000;
            _tcpClient.SendTimeout = 5000;
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            
            await _tcpClient.ConnectAsync(ipAddress, port, cts.Token);
            
            if (_tcpClient.Connected)
            {
                _stream = _tcpClient.GetStream();
                CurrentDevice = new UASDeviceInfo 
                { 
                    DeviceId = Guid.NewGuid().ToString(),
                    IpAddress = ipAddress, 
                    Port = port, 
                    Name = $"Device_{ipAddress}:{port}" 
                };
                UpdateConnectionState(ConnectionState.Connected);
                _logger.LogInformation($"Connected to device at {ipAddress}:{port}");
                return true;
            }
            
            UpdateConnectionState(ConnectionState.Disconnected);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to connect to device at {ipAddress}:{port}");
            UpdateConnectionState(ConnectionState.Error);
            return false;
        }
    }
    
    public async Task DisconnectAsync()
    {
        try
        {
            _stream?.Close();
            _tcpClient?.Close();
            
            _stream = null;
            _tcpClient = null;
            CurrentDevice = null;
            
            UpdateConnectionState(ConnectionState.Disconnected);
            _logger.LogInformation("Disconnected from device");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect");
        }
    }
    
    public async Task<ApiResponse<T>> SendCommandAsync<T>(string endpoint, HttpMethod method, object? payload = null, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _stream == null)
        {
            return ApiResponse<T>.Failure("Not connected to device", "NOT_CONNECTED");
        }
        
        try
        {
            await _sendLock.WaitAsync(cancellationToken);
            
            var request = new
            {
                endpoint,
                method = method.Method,
                payload = payload != null ? JsonSerializer.Serialize(payload) : null
            };
            
            var requestJson = JsonSerializer.Serialize(request);
            var requestBytes = Encoding.UTF8.GetBytes(requestJson);
            
            // Send length prefix (4 bytes)
            var lengthBytes = BitConverter.GetBytes(requestBytes.Length);
            await _stream.WriteAsync(lengthBytes, cancellationToken);
            
            // Send request
            await _stream.WriteAsync(requestBytes, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
            
            // Read response length
            var responseLengthBytes = new byte[4];
            await _stream.ReadExactlyAsync(responseLengthBytes, cancellationToken);
            var responseLength = BitConverter.ToInt32(responseLengthBytes);
            
            // Read response
            var responseBytes = new byte[responseLength];
            await _stream.ReadExactlyAsync(responseBytes, cancellationToken);
            
            var responseJson = Encoding.UTF8.GetString(responseBytes);
            var response = JsonSerializer.Deserialize<ApiResponse<T>>(responseJson);
            
            return response ?? ApiResponse<T>.Failure("Invalid response", "INVALID_RESPONSE");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error sending command to endpoint: {endpoint}");
            return ApiResponse<T>.Failure(ex.Message, "COMMAND_ERROR");
        }
        finally
        {
            _sendLock.Release();
        }
    }
    
    public async Task<ApiResponse<ScanStatus>> StartScanAsync(ScanTask task, CancellationToken cancellationToken = default)
    {
        return await SendCommandAsync<ScanStatus>("/scan", HttpMethod.Post, new { scan = (int)task }, cancellationToken);
    }
    
    public async Task<ApiResponse<ScanStatus>> GetScanStatusAsync(CancellationToken cancellationToken = default)
    {
        return await SendCommandAsync<ScanStatus>("/scan", HttpMethod.Get, null, cancellationToken);
    }
    
    public async Task<ApiResponse<LiveReadingData>> GetLiveReadingAsync(int startIndex, int numPoints, CancellationToken cancellationToken = default)
    {
        return await SendCommandAsync<LiveReadingData>($"/live?startIndex={startIndex}&numPoints={numPoints}", HttpMethod.Get, null, cancellationToken);
    }
    
    public async Task<ApiResponse<MeasurementData>> GetMeasurementAsync(CancellationToken cancellationToken = default)
    {
        return await SendCommandAsync<MeasurementData>("/measurement", HttpMethod.Get, null, cancellationToken);
    }
    
    public async Task<ApiResponse<UASDeviceInfo>> GetDeviceInfoAsync(CancellationToken cancellationToken = default)
    {
        return await SendCommandAsync<UASDeviceInfo>("/info", HttpMethod.Get, null, cancellationToken);
    }
    
    public async Task<ApiResponse<WifiConfiguration>> GetWifiSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await SendCommandAsync<WifiConfiguration>("/wifi", HttpMethod.Get, null, cancellationToken);
    }
    
    public async Task<ApiResponse<CodedResponse>> SetWifiSettingsAsync(WifiSettings settings, CancellationToken cancellationToken = default)
    {
        return await SendCommandAsync<CodedResponse>("/wifi", HttpMethod.Post, settings, cancellationToken);
    }
    
    public async Task<ApiResponse<CodedResponse>> KeepAliveAsync(CancellationToken cancellationToken = default)
    {
        return await SendCommandAsync<CodedResponse>("/ping", HttpMethod.Get, null, cancellationToken);
    }
    
    public async Task<ApiResponse<CodedResponse>> SleepAsync(CancellationToken cancellationToken = default)
    {
        return await SendCommandAsync<CodedResponse>("/sleep", HttpMethod.Post, null, cancellationToken);
    }
    
    public async Task<byte[]> SendSerialDataAsync(byte[] serialData, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _stream == null)
        {
            throw new InvalidOperationException("Not connected to device");
        }
        
        try
        {
            await _sendLock.WaitAsync(cancellationToken);
            
            // Send serial command marker
            var marker = Encoding.UTF8.GetBytes("SERIAL:");
            await _stream.WriteAsync(marker, cancellationToken);
            
            // Send length prefix
            var lengthBytes = BitConverter.GetBytes(serialData.Length);
            await _stream.WriteAsync(lengthBytes, cancellationToken);
            
            // Send serial data
            await _stream.WriteAsync(serialData, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
            
            // Read response length
            var responseLengthBytes = new byte[4];
            await _stream.ReadExactlyAsync(responseLengthBytes, cancellationToken);
            var responseLength = BitConverter.ToInt32(responseLengthBytes);
            
            // Read response
            var responseBytes = new byte[responseLength];
            await _stream.ReadExactlyAsync(responseBytes, cancellationToken);
            
            return responseBytes;
        }
        finally
        {
            _sendLock.Release();
        }
    }
    
    private void UpdateConnectionState(ConnectionState newState)
    {
        if (ConnectionState != newState)
        {
            ConnectionState = newState;
            ConnectionStateChanged?.Invoke(this, newState);
        }
    }
    
    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        _sendLock?.Dispose();
    }
}

public enum ScanTask
{
    Stop = 0,
    Start = 1,
    Pause = 2,
    Resume = 3
}

public class ScanStatus
{
    public int Status { get; set; }
    public string? Message { get; set; }
    public int Progress { get; set; }
    public int TotalPoints { get; set; }
}

public class WifiConfiguration
{
    public string? Ssid { get; set; }
    public string? Password { get; set; }
    public bool Enabled { get; set; }
    public int Channel { get; set; }
    public string? IpAddress { get; set; }
}

public class WifiSettings
{
    public string? Ssid { get; set; }
    public string? Password { get; set; }
    public bool Enable { get; set; }
}

public class CodedResponse
{
    public int Code { get; set; }
    public string? Message { get; set; }
    public bool Success => Code == 0;
}