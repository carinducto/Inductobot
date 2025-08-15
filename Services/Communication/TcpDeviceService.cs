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
                if (_tcpClient != null)
                {
                    _tcpClient.NoDelay = true;
                    _tcpClient.ReceiveTimeout = 30000; // 30 seconds
                    _tcpClient.SendTimeout = 30000;    // 30 seconds
                }
                
                // Set timeout for connection with exponential backoff
                var timeoutMs = 10000 + (attempt - 1) * 5000; // 10s, 15s, 20s
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _connectionCts.Token);
                cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
                
                _logger.LogInformation($"Connection attempt {attempt}/{maxRetries} to {device.IpAddress}:{device.Port}");
                
                await _tcpClient!.ConnectAsync(device.IpAddress, device.Port, cts.Token);
                
                if (_tcpClient?.Connected == true)
                {
                    _stream = _tcpClient?.GetStream();
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
            // Cancel connection operations
            try
            {
                _connectionCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // CTS already disposed - this is fine
            }
            
            // Close stream safely
            try
            {
                _stream?.Close();
                _stream?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing network stream");
            }
            
            // Close TCP client safely
            try
            {
                _tcpClient?.Close();
                _tcpClient?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing TCP client");
            }
            
            // Clean up references
            _stream = null;
            _tcpClient = null;
            
            // Dispose CTS safely
            try
            {
                _connectionCts?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed - this is fine
            }
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
            byte[] commandBytes;
            try
            {
                commandBytes = Encoding.UTF8.GetBytes(request.Command ?? string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to encode command to UTF8: {Command}", request.Command);
                return CommandResponse.CreateError(request.CommandId, "Failed to encode command", ResponseCode.BadRequest);
            }
            
            // Send command
            try
            {
                await _stream.WriteAsync(commandBytes, commandCts.Token);
                await _stream.FlushAsync(commandCts.Token);
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogError(ex, "Stream was disposed during command write");
                return CommandResponse.CreateError(request.CommandId, "Connection closed during write", ResponseCode.ServiceUnavailable);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "I/O error during command write");
                return CommandResponse.CreateError(request.CommandId, "Network I/O error during write", ResponseCode.ServiceUnavailable);
            }
            
            // Read response (customize based on your protocol)
            var buffer = new byte[1024];
            int bytesRead;
            try
            {
                bytesRead = await _stream.ReadAsync(buffer, commandCts.Token);
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogError(ex, "Stream was disposed during response read");
                return CommandResponse.CreateError(request.CommandId, "Connection closed during read", ResponseCode.ServiceUnavailable);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "I/O error during response read");
                return CommandResponse.CreateError(request.CommandId, "Network I/O error during read", ResponseCode.ServiceUnavailable);
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogError(ex, "Response read timed out");
                return CommandResponse.CreateError(request.CommandId, "Response read timeout", ResponseCode.Timeout);
            }
            
            if (bytesRead > 0)
            {
                string response;
                try
                {
                    response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to decode response bytes to UTF8");
                    return CommandResponse.CreateError(request.CommandId, "Failed to decode response", ResponseCode.Error);
                }
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
        
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }
        
        try
        {
            // Add timeout to prevent deadlock
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            await _sendLock.WaitAsync(cts.Token);
            
            try
            {
                await _stream.WriteAsync(data, cancellationToken);
                await _stream.FlushAsync(cancellationToken);
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogError(ex, "Stream was disposed during raw data write");
                throw new InvalidOperationException("Connection closed during write", ex);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "I/O error during raw data write");
                throw new InvalidOperationException("Network I/O error during write", ex);
            }
            
            var buffer = new byte[4096];
            int bytesRead;
            try
            {
                bytesRead = await _stream.ReadAsync(buffer, cancellationToken);
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogError(ex, "Stream was disposed during raw data read");
                throw new InvalidOperationException("Connection closed during read", ex);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "I/O error during raw data read");
                throw new InvalidOperationException("Network I/O error during read", ex);
            }
            
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
                        try
                        {
                            var data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            SafeInvokeDataReceived(data);
                        }
                        catch (ArgumentException ex)
                        {
                            _logger.LogWarning(ex, "Invalid UTF8 data received from device");
                            // Continue reading despite invalid UTF8 data
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing received data");
                        }
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
    
    private void SafeInvokeDataReceived(string data)
    {
        try
        {
            DataReceived?.Invoke(this, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking DataReceived event");
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