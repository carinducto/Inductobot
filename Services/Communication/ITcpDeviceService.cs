using Inductobot.Models.Commands;
using Inductobot.Models.Device;

namespace Inductobot.Services.Communication;

public interface ITcpDeviceService
{
    event EventHandler<ConnectionState>? ConnectionStateChanged;
    event EventHandler<string>? DataReceived;
    
    ConnectionState ConnectionState { get; }
    UASDeviceInfo? CurrentDevice { get; }
    bool IsConnected { get; }
    
    Task<bool> ConnectAsync(string ipAddress, int port, CancellationToken cancellationToken = default);
    Task<bool> ConnectAsync(UASDeviceInfo device, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task<CommandResponse> SendCommandAsync(CommandRequest request, CancellationToken cancellationToken = default);
    Task<byte[]> SendRawDataAsync(byte[] data, CancellationToken cancellationToken = default);
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
    void Dispose();
}