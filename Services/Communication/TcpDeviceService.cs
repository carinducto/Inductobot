using System.Net.Sockets;
using System.Text;
using Inductobot.Models.Commands;
using Inductobot.Models.Device;
using Microsoft.Extensions.Logging;

namespace Inductobot.Services.Communication;

public class TcpDeviceService : ITcpDeviceService, IDisposable
{
    private readonly ILogger<TcpDeviceService> _logger;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private CancellationTokenSource? _connectionCts;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    
    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    public event EventHandler<string>? DataReceived;
    
    public ConnectionState ConnectionState { get; private set; } = ConnectionState.Disconnected;
    public UASDeviceInfo? CurrentDevice { get; private set; }
    public bool IsConnected => ConnectionState == ConnectionState.Connected && _tcpClient?.Connected == true;
    
    public TcpDeviceService(ILogger<TcpDeviceService> logger)
    {
        _logger = logger;
    }
    
    public async Task<bool> ConnectAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
    {
        var device = new UASDeviceInfo
        {
            IpAddress = ipAddress,
            Port = port,
            Name = $"Device_{ipAddress}:{port}",
            DeviceId = Guid.NewGuid().ToString()
        };
        
        return await ConnectAsync(device, cancellationToken);
    }
    
    public async Task<bool> ConnectAsync(UASDeviceInfo device, CancellationToken cancellationToken = default)
    {
        const int maxRetries = 3;
        const int baseDelayMs = 1000;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (IsConnected)
                {
                    await DisconnectAsync();
                }
                
                UpdateConnectionState(ConnectionState.Connecting);
                CurrentDevice = device;
                
                _connectionCts = new CancellationTokenSource();
                _tcpClient = new TcpClient();
                
                // Configure TCP client for better reliability
                _tcpClient.NoDelay = true;
                _tcpClient.ReceiveTimeout = 30000; // 30 seconds
                _tcpClient.SendTimeout = 30000;    // 30 seconds
                
                // Set timeout for connection with exponential backoff
                var timeoutMs = 10000 + (attempt - 1) * 5000; // 10s, 15s, 20s
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _connectionCts.Token);
                cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
                
                _logger.LogInformation($"Connection attempt {attempt}/{maxRetries} to {device.IpAddress}:{device.Port}");
                
                await _tcpClient.ConnectAsync(device.IpAddress, device.Port, cts.Token);
                
                if (_tcpClient.Connected)
                {
                    _stream = _tcpClient.GetStream();
                    UpdateConnectionState(ConnectionState.Connected);
                    _logger.LogInformation($"Connected to device at {device.IpAddress}:{device.Port} on attempt {attempt}");
                    
                    // Start background read task
                    _ = Task.Run(() => ReadDataAsync(_connectionCts.Token), _connectionCts.Token);
                    
                    return true;
                }
                
                _logger.LogWarning($"Connection attempt {attempt} failed - not connected");
                
                if (attempt < maxRetries)
                {
                    var delay = baseDelayMs * attempt; // Progressive delay
                    _logger.LogInformation($"Retrying connection in {delay}ms...");
                    await Task.Delay(delay, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Connection cancelled by user");
                UpdateConnectionState(ConnectionState.Disconnected);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Connection attempt {attempt}/{maxRetries} failed: {ex.Message}");
                
                if (attempt == maxRetries)
                {
                    _logger.LogError(ex, $"All connection attempts failed to device at {device.IpAddress}:{device.Port}");
                    UpdateConnectionState(ConnectionState.Error);
                    return false;
                }
                
                // Progressive delay between retries
                var delay = baseDelayMs * attempt;
                await Task.Delay(delay, cancellationToken);
            }
        }
        
        UpdateConnectionState(ConnectionState.Error);
        return false;
    }
    
    public Task DisconnectAsync()
    {
        try
        {
            _connectionCts?.Cancel();
            _stream?.Close();
            _tcpClient?.Close();
            
            _stream = null;
            _tcpClient = null;
            _connectionCts = null;
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
    
    public async Task<CommandResponse> SendCommandAsync(CommandRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _stream == null)
        {
            return CommandResponse.CreateError(request.CommandId, "Not connected to device", ResponseCode.ServiceUnavailable);
        }
        
        try
        {
            // Add timeout to prevent deadlock
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            await _sendLock.WaitAsync(cts.Token);
            
            // Apply command-specific timeout if specified
            using var commandCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (request.TimeoutMs > 0)
            {
                commandCts.CancelAfter(TimeSpan.FromMilliseconds(request.TimeoutMs));
            }
            else
            {
                commandCts.CancelAfter(TimeSpan.FromSeconds(10)); // Default 10 second timeout
            }
            
            // Convert command to bytes (customize based on your protocol)
            var commandBytes = Encoding.UTF8.GetBytes(request.Command);
            
            // Send command
            await _stream.WriteAsync(commandBytes, commandCts.Token);
            await _stream.FlushAsync(commandCts.Token);
            
            // Read response (customize based on your protocol)
            var buffer = new byte[1024];
            var bytesRead = await _stream.ReadAsync(buffer, commandCts.Token);
            
            if (bytesRead > 0)
            {
                var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                return CommandResponse.CreateSuccess(request.CommandId, response);
            }
            
            return CommandResponse.CreateError(request.CommandId, "No response received", ResponseCode.Timeout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error sending command: {request.Command}");
            
            // Check if it's a connection error and update state
            if (ex is SocketException or IOException or ObjectDisposedException)
            {
                _logger.LogWarning("Connection lost during command execution");
                UpdateConnectionState(ConnectionState.Error);
                return CommandResponse.CreateError(request.CommandId, "Connection lost", ResponseCode.ConnectionError);
            }
            
            return CommandResponse.CreateError(request.CommandId, ex.Message, ResponseCode.Error);
        }
        finally
        {
            _sendLock.Release();
        }
    }
    
    public async Task<byte[]> SendRawDataAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _stream == null)
        {
            throw new InvalidOperationException("Not connected to device");
        }
        
        try
        {
            // Add timeout to prevent deadlock
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            await _sendLock.WaitAsync(cts.Token);
            
            await _stream.WriteAsync(data, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
            
            var buffer = new byte[4096];
            var bytesRead = await _stream.ReadAsync(buffer, cancellationToken);
            
            var response = new byte[bytesRead];
            Array.Copy(buffer, response, bytesRead);
            return response;
        }
        finally
        {
            _sendLock.Release();
        }
    }
    
    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            return false;
        
        try
        {
            // Send a simple ping command (customize based on your protocol)
            var pingRequest = new CommandRequest
            {
                Command = "PING",
                Type = CommandType.Query,
                TimeoutMs = 1000
            };
            
            var response = await SendCommandAsync(pingRequest, cancellationToken);
            return response.Success;
        }
        catch
        {
            return false;
        }
    }
    
    private async Task ReadDataAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var consecutiveErrors = 0;
        const int maxConsecutiveErrors = 3;
        
        try
        {
            while (!cancellationToken.IsCancellationRequested && _stream != null && IsConnected)
            {
                try
                {
                    // Add timeout to read operations to prevent hanging
                    using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    readCts.CancelAfter(TimeSpan.FromSeconds(30));
                    
                    var bytesRead = await _stream.ReadAsync(buffer, readCts.Token);
                    
                    if (bytesRead > 0)
                    {
                        consecutiveErrors = 0; // Reset error counter on successful read
                        var data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        DataReceived?.Invoke(this, data);
                    }
                    else
                    {
                        // Connection closed gracefully
                        _logger.LogInformation("Device connection closed gracefully");
                        UpdateConnectionState(ConnectionState.Disconnected);
                        break;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Read operation cancelled by user request");
                    break;
                }
                catch (Exception ex)
                {
                    consecutiveErrors++;
                    _logger.LogWarning(ex, $"Error reading data from device (consecutive errors: {consecutiveErrors})");
                    
                    if (consecutiveErrors >= maxConsecutiveErrors)
                    {
                        _logger.LogError("Too many consecutive read errors, marking connection as failed");
                        UpdateConnectionState(ConnectionState.Error);
                        break;
                    }
                    
                    // Brief delay before retrying
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Read operation cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error in read loop");
            UpdateConnectionState(ConnectionState.Error);
        }
        finally
        {
            _logger.LogDebug("Read data task completed");
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