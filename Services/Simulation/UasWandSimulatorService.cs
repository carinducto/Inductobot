using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Inductobot.Abstractions.Services;
using Inductobot.Models.Debug;
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
    private readonly DebugConfiguration _config;
    private UasWandHttpsSimulator? _httpSimulator;
    private DateTime? _startTime;

    // IUasWandSimulatorService implementation
    public bool IsRunning => _httpSimulator?.IsRunning ?? false;
    public int Port => _config.SimulatorPort;
    public string? IPAddress => GetLocalIPAddress();
    public event EventHandler<bool>? SimulatorStateChanged;

    public UasWandSimulatorService(ILogger<UasWandSimulatorService> logger, DebugConfiguration config)
    {
        _logger = logger;
        _config = config;
        
        // Log constructor execution to verify DI is working
        try 
        {
            _logger.LogInformation("🏗️ UasWandSimulatorService constructor called - Config.EnableSimulator: {Enabled}", config?.EnableSimulator ?? false);
        }
        catch (Exception ex)
        {
            // Fallback logging if there are any issues
            Console.WriteLine($"❌ UasWandSimulatorService constructor error: {ex.Message}");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("🚀 UasWandSimulatorService ExecuteAsync starting...");
        _logger.LogDebug("📋 Simulator configuration - Enabled: {Enabled}, Port: {Port}", _config.EnableSimulator, _config.SimulatorPort);
        
        if (!_config.EnableSimulator)
        {
            _logger.LogInformation("⏹️ UAS-WAND simulator is disabled via configuration");
            return;
        }

        try
        {
            _logger.LogDebug("🔧 Creating simulator logger factory...");
            var loggerFactory = LoggerFactory.Create(builder => 
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            var simulatorLogger = loggerFactory.CreateLogger<UasWandHttpsSimulator>();
                
            _logger.LogDebug("🏗️ Creating UasWandHttpsSimulator instance on port {Port}...", _config.SimulatorPort);
            _httpSimulator = new UasWandHttpsSimulator(simulatorLogger, _config.SimulatorPort);
            
            _logger.LogInformation("🚀 Starting HTTP UAS-WAND simulator on localhost:{Port} (local connections only)", _config.SimulatorPort);
            await _httpSimulator.StartAsync(stoppingToken);
            _startTime = DateTime.Now;
            _logger.LogInformation("✅ UAS-WAND simulator started successfully on localhost (secure mode). IsRunning: {IsRunning}", IsRunning);
            SimulatorStateChanged?.Invoke(this, true);
            
            // Keep the service running until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("⏹️ UAS-WAND simulator service is stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ CRITICAL ERROR running UAS-WAND simulator service - Simulator will not be available");
            // Don't rethrow here - we don't want to crash the entire app if simulator fails
            // throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping UAS-WAND simulator service");
        
        if (_httpSimulator != null)
        {
            await _httpSimulator.StopAsync();
            _httpSimulator.Dispose();
            SimulatorStateChanged?.Invoke(this, false);
        }
        
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _httpSimulator?.Dispose();
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
            if (_httpSimulator == null)
            {
                var loggerFactory = LoggerFactory.Create(builder => 
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Debug);
                });
                var simulatorLogger = loggerFactory.CreateLogger<UasWandHttpsSimulator>();
                _httpSimulator = new UasWandHttpsSimulator(simulatorLogger, _config.SimulatorPort);
            }

            await _httpSimulator.StartAsync();
            _startTime = DateTime.Now;
            SimulatorStateChanged?.Invoke(this, true);
            _logger.LogInformation("HTTP UAS-WAND simulator started manually");
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
            if (_httpSimulator != null)
            {
                await _httpSimulator.StopAsync();
                SimulatorStateChanged?.Invoke(this, false);
                _logger.LogInformation("HTTP UAS-WAND simulator stopped manually");
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