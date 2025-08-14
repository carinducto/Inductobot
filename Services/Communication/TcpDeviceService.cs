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
            
            // Set timeout for connection
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _connectionCts.Token);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            
            await _tcpClient.ConnectAsync(device.IpAddress, device.Port, cts.Token);
            
            if (_tcpClient.Connected)
            {
                _stream = _tcpClient.GetStream();
                UpdateConnectionState(ConnectionState.Connected);
                _logger.LogInformation($"Connected to device at {device.IpAddress}:{device.Port}");
                
                // Start background read task
                _ = Task.Run(() => ReadDataAsync(_connectionCts.Token), _connectionCts.Token);
                
                return true;
            }
            
            UpdateConnectionState(ConnectionState.Disconnected);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to connect to device at {device.IpAddress}:{device.Port}");
            UpdateConnectionState(ConnectionState.Error);
            return false;
        }
    }
    
    public async Task DisconnectAsync()
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
    }
    
    public async Task<CommandResponse> SendCommandAsync(CommandRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _stream == null)
        {
            return CommandResponse.CreateError(request.CommandId, "Not connected to device", ResponseCode.ServiceUnavailable);
        }
        
        try
        {
            await _sendLock.WaitAsync(cancellationToken);
            
            // Convert command to bytes (customize based on your protocol)
            var commandBytes = Encoding.UTF8.GetBytes(request.Command);
            
            // Send command
            await _stream.WriteAsync(commandBytes, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
            
            // Read response (customize based on your protocol)
            var buffer = new byte[1024];
            var bytesRead = await _stream.ReadAsync(buffer, cancellationToken);
            
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
            await _sendLock.WaitAsync(cancellationToken);
            
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
        
        try
        {
            while (!cancellationToken.IsCancellationRequested && _stream != null && IsConnected)
            {
                var bytesRead = await _stream.ReadAsync(buffer, cancellationToken);
                
                if (bytesRead > 0)
                {
                    var data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    DataReceived?.Invoke(this, data);
                }
                else
                {
                    // Connection closed
                    break;
                }
            }
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Read operation cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading data from device");
            UpdateConnectionState(ConnectionState.Error);
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