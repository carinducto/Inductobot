using Inductobot.Abstractions.Communication;
using Inductobot.Abstractions.Services;
using Inductobot.Models.Commands;
using Inductobot.Models.Device;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Text;

namespace Inductobot.Services.Communication;

/// <summary>
/// TCP transport implementation for UAS-WAND devices
/// </summary>
public class UasWandTcpTransport : IUasWandTransport
{
    private readonly ILogger<UasWandTcpTransport> _logger;
    private readonly IConfigurationService _config;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private ConnectionState _connectionState = ConnectionState.Disconnected;
    
    public ConnectionState ConnectionState => _connectionState;
    
    public bool IsConnected
    {
        get
        {
            try
            {
                return _connectionState == ConnectionState.Connected && _tcpClient?.Connected == true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception checking TCP client connection state");
                return false;
            }
        }
    }
    
    public UASDeviceInfo? CurrentDevice { get; private set; }
    
    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    
    public UasWandTcpTransport(ILogger<UasWandTcpTransport> logger, IConfigurationService config)
    {
        _logger = logger;
        _config = config;
    }
    
    public async Task<bool> ConnectAsync(string address, int port, CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            await DisconnectAsync();
        }
        
        UpdateConnectionState(ConnectionState.Connecting);
        
        try
        {
            _tcpClient = new TcpClient();
            if (_tcpClient != null)
            {
                var timeoutMs = (int)_config.GetConnectionTimeout().TotalMilliseconds;
                _tcpClient.ReceiveTimeout = timeoutMs;
                _tcpClient.SendTimeout = timeoutMs;
                _tcpClient.NoDelay = true;
            }
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_config.GetConnectionTimeout());
            
            await _tcpClient!.ConnectAsync(address, port, cts.Token);
            
            if (_tcpClient?.Connected == true)
            {
                _stream = _tcpClient?.GetStream();
                CurrentDevice = new UASDeviceInfo
                {
                    DeviceId = Guid.NewGuid().ToString(),
                    IpAddress = address,
                    Port = port,
                    Name = $"UAS-WAND_{address}:{port}",
                    IsOnline = true,
                    LastSeen = DateTime.Now
                };
                
                UpdateConnectionState(ConnectionState.Connected);
                _logger.LogInformation("Successfully connected to UAS-WAND device at {Address}:{Port}", address, port);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to UAS-WAND device at {Address}:{Port}", address, port);
            UpdateConnectionState(ConnectionState.Error);
            await DisconnectAsync();
        }
        
        return false;
    }
    
    public async Task<bool> ConnectAsync(UASDeviceInfo device, CancellationToken cancellationToken = default)
    {
        return await ConnectAsync(device.IpAddress, device.Port, cancellationToken);
    }
    
    public async Task DisconnectAsync()
    {
        UpdateConnectionState(ConnectionState.Disconnecting);
        
        try
        {
            _stream?.Close();
            _stream?.Dispose();
            _stream = null;
            
            _tcpClient?.Close();
            _tcpClient?.Dispose();
            _tcpClient = null;
            
            CurrentDevice = null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception during disconnect cleanup");
        }
        finally
        {
            UpdateConnectionState(ConnectionState.Disconnected);
            _logger.LogInformation("Disconnected from UAS-WAND device");
        }
    }
    
    public async Task<byte[]> SendRawDataAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _stream == null)
        {
            throw new InvalidOperationException("Not connected to UAS-WAND device");
        }
        
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }
        
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_config.GetConnectionTimeout());
            await _sendLock.WaitAsync(cts.Token);
            
            try
            {
                // Send data length prefix
                var lengthBytes = BitConverter.GetBytes(data.Length);
                await _stream.WriteAsync(lengthBytes, cancellationToken);
                
                // Send data
                await _stream.WriteAsync(data, cancellationToken);
                await _stream.FlushAsync(cancellationToken);
                
                // Read response length
                var responseLengthBytes = new byte[4];
                await _stream.ReadExactlyAsync(responseLengthBytes, cancellationToken);
                var responseLength = BitConverter.ToInt32(responseLengthBytes);
                
                if (responseLength < 0 || responseLength > 10 * 1024 * 1024)
                {
                    throw new InvalidOperationException($"Invalid response length: {responseLength}");
                }
                
                // Read response data
                var responseBytes = new byte[responseLength];
                await _stream.ReadExactlyAsync(responseBytes, cancellationToken);
                
                return responseBytes;
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogError(ex, "Stream was disposed during UAS-WAND communication");
                throw new InvalidOperationException("Connection closed during communication", ex);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "I/O error during UAS-WAND communication");
                throw new InvalidOperationException("Network I/O error", ex);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }
    
    public async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(command))
        {
            throw new ArgumentException("Command cannot be null or empty", nameof(command));
        }
        
        byte[] commandBytes;
        try
        {
            commandBytes = Encoding.UTF8.GetBytes(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encode command to UTF-8: {Command}", command);
            throw new InvalidOperationException("Failed to encode command", ex);
        }
        
        var responseBytes = await SendRawDataAsync(commandBytes, cancellationToken);
        
        try
        {
            return Encoding.UTF8.GetString(responseBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decode response from UTF-8");
            throw new InvalidOperationException("Failed to decode response", ex);
        }
    }
    
    private void UpdateConnectionState(ConnectionState newState)
    {
        if (_connectionState != newState)
        {
            _connectionState = newState;
            _logger.LogDebug("UAS-WAND connection state changed to: {State}", newState);
            
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