using Inductobot.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace Inductobot;

public partial class App : Application
{
	private readonly ILogger<App> _logger;
	private readonly IUasWandSimulatorService _simulatorService;

	public App(ILogger<App> logger, IUasWandSimulatorService simulatorService)
	{
		_logger = logger;
		_simulatorService = simulatorService;
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}

	protected override void OnStart()
	{
		base.OnStart();
		
		// Start the simulator manually since MAUI background services don't start automatically
		_ = Task.Run(async () =>
		{
			try
			{
				_logger.LogInformation("🚀 Manually starting UAS-WAND simulator from App.OnStart()...");
				var started = await _simulatorService.StartSimulatorAsync();
				if (started)
				{
					_logger.LogInformation("✅ UAS-WAND simulator started successfully from App.OnStart()");
				}
				else
				{
					_logger.LogWarning("⚠️ Failed to start UAS-WAND simulator from App.OnStart()");
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "❌ Error starting UAS-WAND simulator from App.OnStart()");
			}
		});
	}

	protected override void OnSleep()
	{
		base.OnSleep();
		
		// Optionally stop the simulator when the app goes to sleep
		_ = Task.Run(async () =>
		{
			try
			{
				_logger.LogInformation("💤 Stopping UAS-WAND simulator (app sleeping)...");
				await _simulatorService.StopSimulatorAsync();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error stopping simulator on app sleep");
			}
		});
	}
}