using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Inductobot.Abstractions.Services;
using System.Net;
using System.Net.Sockets;

namespace Inductobot.Services.Simulation;

/// <summary>
/// Background service that runs the UAS-WAND device simulator.
/// Security: Simulator only accepts connections from localhost for safety.
/// </summary>
public class UasWandSimulatorService : BackgroundService, IUasWandSimulatorService
{
    private readonly ILogger<UasWandSimulatorService> _logger;
    private SimulatedUasWandDevice? _simulatedDevice;
    private readonly bool _enableSimulator = true; // Hardcoded for development
    private readonly int _simulatorPort = 8080;    // Hardcoded for development
    private DateTime? _startTime;

    // IUasWandSimulatorService implementation
    public bool IsRunning => _simulatedDevice?.IsRunning ?? false;
    public int Port => _simulatorPort;
    public string? IPAddress => GetLocalIPAddress();
    public event EventHandler<bool>? SimulatorStateChanged;

    public UasWandSimulatorService(ILogger<UasWandSimulatorService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enableSimulator)
        {
            _logger.LogInformation("UAS-WAND simulator is disabled via configuration");
            return;
        }

        try
        {
            var loggerFactory = LoggerFactory.Create(builder => 
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            var simulatorLogger = loggerFactory.CreateLogger<SimulatedUasWandDevice>();
                
            _simulatedDevice = new SimulatedUasWandDevice(simulatorLogger, _simulatorPort);
            
            _logger.LogInformation("Starting UAS-WAND simulator on localhost:{Port} (local connections only)", _simulatorPort);
            await _simulatedDevice.StartAsync(stoppingToken);
            _startTime = DateTime.Now;
            _logger.LogInformation("UAS-WAND simulator started successfully on localhost (secure mode). IsRunning: {IsRunning}", IsRunning);
            SimulatorStateChanged?.Invoke(this, true);
            
            // Keep the service running until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("UAS-WAND simulator service is stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running UAS-WAND simulator service");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping UAS-WAND simulator service");
        
        if (_simulatedDevice != null)
        {
            await _simulatedDevice.StopAsync();
            _simulatedDevice.Dispose();
            SimulatorStateChanged?.Invoke(this, false);
        }
        
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _simulatedDevice?.Dispose();
        base.Dispose();
    }

    public async Task<bool> StartSimulatorAsync()
    {
        if (IsRunning)
        {
            _logger.LogWarning("Simulator is already running");
            return true;
        }

        try
        {
            if (_simulatedDevice == null)
            {
                var loggerFactory = LoggerFactory.Create(builder => 
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Debug);
                });
                var simulatorLogger = loggerFactory.CreateLogger<SimulatedUasWandDevice>();
                _simulatedDevice = new SimulatedUasWandDevice(simulatorLogger, _simulatorPort);
            }

            await _simulatedDevice.StartAsync();
            _startTime = DateTime.Now;
            SimulatorStateChanged?.Invoke(this, true);
            _logger.LogInformation("UAS-WAND simulator started manually");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start simulator manually");
            return false;
        }
    }

    public async Task<bool> StopSimulatorAsync()
    {
        if (!IsRunning)
        {
            _logger.LogWarning("Simulator is not running");
            return true;
        }

        try
        {
            if (_simulatedDevice != null)
            {
                await _simulatedDevice.StopAsync();
                SimulatorStateChanged?.Invoke(this, false);
                _logger.LogInformation("UAS-WAND simulator stopped manually");
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop simulator manually");
            return false;
        }
    }

    public SimulatorStatus GetStatus()
    {
        return new SimulatorStatus
        {
            IsRunning = IsRunning,
            Port = Port,
            IPAddress = this.IPAddress,
            DeviceName = "UAS-WAND_Simulator",
            FirmwareVersion = "3.9.0-sim",
            ConnectedClients = 0, // TODO: Track connected clients
            StartTime = _startTime,
            Status = IsRunning ? "Running" : "Stopped"
        };
    }

    private static string GetLocalIPAddress()
    {
        // Always return localhost for security - simulator only accepts local connections
        return "127.0.0.1";
    }
}