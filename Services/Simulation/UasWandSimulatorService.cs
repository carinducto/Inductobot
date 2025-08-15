using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Inductobot.Services.Simulation;

/// <summary>
/// Background service that runs the UAS-WAND device simulator
/// </summary>
public class UasWandSimulatorService : BackgroundService
{
    private readonly ILogger<UasWandSimulatorService> _logger;
    private SimulatedUasWandDevice? _simulatedDevice;
    private readonly bool _enableSimulator = true; // Hardcoded for development
    private readonly int _simulatorPort = 8080;    // Hardcoded for development

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
            
            _logger.LogInformation("Starting UAS-WAND simulator on port {Port}", _simulatorPort);
            await _simulatedDevice.StartAsync(stoppingToken);
            
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
        }
        
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _simulatedDevice?.Dispose();
        base.Dispose();
    }
}