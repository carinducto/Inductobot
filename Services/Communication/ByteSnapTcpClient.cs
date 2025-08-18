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
#pragma warning disable CS0067 // Event is declared but never used
    public event EventHandler<byte[]>? DataReceived;
#pragma warning restore CS0067
    
    public ConnectionState ConnectionState { get; private set; } = ConnectionState.Disconnected;
    public UASDeviceInfo? CurrentDevice { get; private set; }
    public bool IsConnected 
    { 
        get
        {
            try
            {
                return ConnectionState == ConnectionState.Connected && _tcpClient?.Connected == true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception checking TCP client connection state");
                return false;
            }
        }
    }
    
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
            if (_tcpClient != null)
            {
                _tcpClient.ReceiveTimeout = 5000;
                _tcpClient.SendTimeout = 5000;
            }
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            
            await _tcpClient!.ConnectAsync(ipAddress, port, cts.Token);
            
            if (_tcpClient?.Connected == true)
            {
                _stream = _tcpClient?.GetStream();
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
    
    public Task DisconnectAsync()
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
        
        return Task.CompletedTask;
    }
    
    public async Task<ApiResponse<T>> SendCommandAsync<T>(string endpoint, HttpMethod method, object? payload = null, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _stream == null)
        {
            return ApiResponse<T>.Failure("Not connected to device", "NOT_CONNECTED");
        }
        
        try
        {
            // Add timeout to prevent deadlock
            using var lockCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lockCts.CancelAfter(TimeSpan.FromSeconds(30));
            await _sendLock.WaitAsync(lockCts.Token);
            
            var request = new
            {
                endpoint,
                method = method.Method,
                payload = payload != null ? SafeSerialize(payload) : null
            };
            
            var requestJson = SafeSerialize(request);
            if (requestJson == null)
            {
                return ApiResponse<T>.Failure("Failed to serialize request", "SERIALIZATION_ERROR");
            }
            
            byte[] requestBytes;
            try
            {
                requestBytes = Encoding.UTF8.GetBytes(requestJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to encode request JSON to UTF8");
                return ApiResponse<T>.Failure("Failed to encode request", "ENCODING_ERROR");
            }
            
            if (_stream == null)
            {
                return ApiResponse<T>.Failure("Stream is not available", "STREAM_NULL");
            }
            
            try
            {
                // Send length prefix (4 bytes)
                var lengthBytes = BitConverter.GetBytes(requestBytes.Length);
                await _stream.WriteAsync(lengthBytes, cancellationToken);
                
                // Send request
                await _stream.WriteAsync(requestBytes, cancellationToken);
                await _stream.FlushAsync(cancellationToken);
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogError(ex, "Stream was disposed during write operation");
                return ApiResponse<T>.Failure("Connection closed during write", "STREAM_DISPOSED");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Stream write operation failed");
                return ApiResponse<T>.Failure("Invalid stream operation", "STREAM_INVALID");
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "I/O error during stream write");
                return ApiResponse<T>.Failure("Network I/O error", "IO_ERROR");
            }
            
            // Read response length with timeout
            var responseLengthBytes = new byte[4];
            try
            {
                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                readCts.CancelAfter(TimeSpan.FromSeconds(10));
                await _stream.ReadExactlyAsync(responseLengthBytes, readCts.Token);
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogError(ex, "Stream was disposed during read operation");
                return ApiResponse<T>.Failure("Connection closed during read", "STREAM_DISPOSED");
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "I/O error during response length read");
                return ApiResponse<T>.Failure("Network I/O error during read", "IO_ERROR");
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogError(ex, "Response length read timed out");
                return ApiResponse<T>.Failure("Response read timeout", "READ_TIMEOUT");
            }
            
            int responseLength;
            try
            {
                responseLength = BitConverter.ToInt32(responseLengthBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert response length bytes");
                return ApiResponse<T>.Failure("Invalid response length format", "LENGTH_CONVERSION_ERROR");
            }
            
            // Validate response length to prevent memory issues
            if (responseLength > 10 * 1024 * 1024) // 10MB limit
            {
                _logger.LogError("Response too large: {ResponseLength} bytes", responseLength);
                return ApiResponse<T>.Failure($"Response too large: {responseLength} bytes", "RESPONSE_TOO_LARGE");
            }
            
            if (responseLength < 0)
            {
                _logger.LogError("Invalid negative response length: {ResponseLength}", responseLength);
                return ApiResponse<T>.Failure($"Invalid response length: {responseLength}", "INVALID_LENGTH");
            }
            
            // Read response with timeout
            var responseBytes = new byte[responseLength];
            try
            {
                using var readCts2 = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                readCts2.CancelAfter(TimeSpan.FromSeconds(30));
                await _stream.ReadExactlyAsync(responseBytes, readCts2.Token);
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogError(ex, "Stream was disposed during response read");
                return ApiResponse<T>.Failure("Connection closed during response read", "STREAM_DISPOSED");
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "I/O error during response read");
                return ApiResponse<T>.Failure("Network I/O error during response read", "IO_ERROR");
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogError(ex, "Response read timed out");
                return ApiResponse<T>.Failure("Response read timeout", "READ_TIMEOUT");
            }
            
            string responseJson;
            try
            {
                responseJson = Encoding.UTF8.GetString(responseBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decode response bytes to UTF8");
                return ApiResponse<T>.Failure("Failed to decode response", "DECODING_ERROR");
            }
            var response = SafeDeserialize<ApiResponse<T>>(responseJson);
            
            return response ?? ApiResponse<T>.Failure("Failed to deserialize response", "DESERIALIZATION_ERROR");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Command cancelled by user: {Endpoint}", endpoint);
            return ApiResponse<T>.Failure("Operation cancelled", "CANCELLED");
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Command timeout: {Endpoint}", endpoint);
            return ApiResponse<T>.Failure("Operation timed out", "TIMEOUT");
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "Network error sending command to endpoint: {Endpoint}", endpoint);
            UpdateConnectionState(ConnectionState.Error);
            return ApiResponse<T>.Failure("Network connection error", "NETWORK_ERROR");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error sending command to endpoint: {Endpoint}", endpoint);
            UpdateConnectionState(ConnectionState.Error);
            return ApiResponse<T>.Failure("Communication error", "IO_ERROR");
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(ex, "Connection disposed while sending command: {Endpoint}", endpoint);
            UpdateConnectionState(ConnectionState.Disconnected);
            return ApiResponse<T>.Failure("Connection was closed", "CONNECTION_CLOSED");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation sending command to endpoint: {Endpoint}", endpoint);
            return ApiResponse<T>.Failure($"Invalid operation: {ex.Message}", "INVALID_OPERATION");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending command to endpoint: {Endpoint}", endpoint);
            return ApiResponse<T>.Failure("An unexpected error occurred", "UNEXPECTED_ERROR");
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
    
    public async Task<ApiResponse<CodedResponse>> RestartWifiAsync(CancellationToken cancellationToken = default)
    {
        return await SendCommandAsync<CodedResponse>("/wifi/restart", HttpMethod.Post, null, cancellationToken);
    }
    
    public async Task<byte[]> SendSerialDataAsync(byte[] serialData, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _stream == null)
        {
            throw new InvalidOperationException("Not connected to device");
        }
        
        if (serialData == null)
        {
            throw new ArgumentNullException(nameof(serialData));
        }
        
        try
        {
            // Add timeout to prevent deadlock
            using var lockCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lockCts.CancelAfter(TimeSpan.FromSeconds(30));
            await _sendLock.WaitAsync(lockCts.Token);
            
            try
            {
                // Send serial command marker
                var marker = Encoding.UTF8.GetBytes("SERIAL:");
                await _stream.WriteAsync(marker, cancellationToken);
                
                // Send length prefix
                var lengthBytes = BitConverter.GetBytes(serialData.Length);
                await _stream.WriteAsync(lengthBytes, cancellationToken);
                
                // Send serial data
                await _stream.WriteAsync(serialData, cancellationToken);
                await _stream.FlushAsync(cancellationToken);
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogError(ex, "Stream was disposed during serial write operation");
                throw new InvalidOperationException("Connection closed during write", ex);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "I/O error during serial write");
                throw new InvalidOperationException("Network I/O error during write", ex);
            }
            
            // Read response length with timeout
            var responseLengthBytes = new byte[4];
            int responseLength;
            try
            {
                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                readCts.CancelAfter(TimeSpan.FromSeconds(10));
                await _stream.ReadExactlyAsync(responseLengthBytes, readCts.Token);
                responseLength = BitConverter.ToInt32(responseLengthBytes);
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogError(ex, "Stream was disposed during response read");
                throw new InvalidOperationException("Connection closed during read", ex);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "I/O error during response read");
                throw new InvalidOperationException("Network I/O error during read", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read or convert response length");
                throw new InvalidOperationException("Failed to read response length", ex);
            }
            
            // Validate response length to prevent memory issues
            if (responseLength > 10 * 1024 * 1024) // 10MB limit
            {
                throw new InvalidOperationException($"Response too large: {responseLength} bytes");
            }
            
            // Read response with timeout
            var responseBytes = new byte[responseLength];
            using var readCts2 = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readCts2.CancelAfter(TimeSpan.FromSeconds(30));
            await _stream.ReadExactlyAsync(responseBytes, readCts2.Token);
            
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
    
    private string? SafeSerialize(object obj)
    {
        try
        {
            return JsonSerializer.Serialize(obj);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON serialization error");
            return null;
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "JSON serialization not supported for object type");
            return null;
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument for JSON serialization");
            return null;
        }
    }
    
    private T? SafeDeserialize<T>(string json) where T : class
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Cannot deserialize null or empty JSON string");
                return null;
            }
            
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, $"JSON deserialization error. JSON: {json?.Substring(0, Math.Min(100, json.Length))}...");
            return null;
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "JSON deserialization not supported for type {Type}", typeof(T).Name);
            return null;
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument for JSON deserialization");
            return null;
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
            _logger.LogWarning(ex, "Exception during dispose disconnect");
        }
        
        try
        {
            _sendLock?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception disposing send lock");
        }
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

// WifiConfiguration moved to Inductobot.Models.Device namespace

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