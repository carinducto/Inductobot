using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Inductobot.Extensions;
using Inductobot.Abstractions.Services;
using Inductobot.Services.Core;
using Inductobot.Services.Data;
using Inductobot.Services.Device;
using Inductobot.Services.Simulation;
using Inductobot.Services.Debug;
using Inductobot.Models.Debug;
using Inductobot.ViewModels;
using Inductobot.Views;

namespace Inductobot;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		// Parse command-line arguments for debug options
		var args = Environment.GetCommandLineArgs();
		var debugConfig = DebugArgumentParser.ParseArguments(args);
		
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Register Debug Configuration
		builder.Services.AddSingleton(debugConfig);
		
		// Register Debug Services (if enabled)
		DebugConsoleService? consoleService = null;
		FileLoggingService? fileService = null;
		
		if (debugConfig.ShowConsole)
		{
			consoleService = new DebugConsoleService(debugConfig, 
				LoggerFactory.Create(b => b.AddConsole()).CreateLogger<DebugConsoleService>());
			builder.Services.AddSingleton(consoleService);
			builder.Services.AddHostedService(provider => provider.GetRequiredService<DebugConsoleService>());
		}
		
		if (debugConfig.EnableFileLogging)
		{
			fileService = new FileLoggingService(debugConfig,
				LoggerFactory.Create(b => b.AddConsole()).CreateLogger<FileLoggingService>());
			builder.Services.AddSingleton(fileService);
			builder.Services.AddHostedService(provider => provider.GetRequiredService<FileLoggingService>());
		}

		// UAS-WAND simulator will use hardcoded configuration for simplicity

		// Register UAS-WAND Services - Modular architecture with clean separation
		builder.Services.AddUasWandServices();
		
		// Register Core Services
		builder.Services.AddSingleton<IMessagingService, MessagingService>();
		builder.Services.AddSingleton<INavigationService, NavigationService>();
		builder.Services.AddSingleton<INetworkInfoService, NetworkInfoService>();
		
		// Register Configuration Services
		builder.Services.AddSingleton<IConfigurationService, Services.Core.ConfigurationService>();
		builder.Services.AddSingleton<IRuntimeLoggingService, Services.Core.RuntimeLoggingService>();
		
		// Register UAS-WAND Simulator (for testing without real device)
		builder.Services.AddSingleton<UasWandSimulatorService>();
		builder.Services.AddSingleton<IUasWandSimulatorService>(provider => provider.GetRequiredService<UasWandSimulatorService>());
		builder.Services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<UasWandSimulatorService>());
		
		// Register ViewModels - Transient (new instance each time)
		builder.Services.AddTransient<MainViewModel>();
		builder.Services.AddTransient<SimulatorControlViewModel>();
		// UasWandControlViewModel is already registered by AddUasWandServices()
		
		// Register Views
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<DeviceConnectionPage>();

		// Configure logging
		builder.Logging.ClearProviders();
		builder.Logging.SetMinimumLevel(LogLevel.Trace);
		
#if DEBUG
		builder.Logging.AddDebug();
#endif

		// Add console logging for general output
		builder.Logging.AddConsole();
		
		// Add our custom debug logger provider if debug services are enabled
		if (consoleService != null || fileService != null)
		{
			builder.Logging.AddProvider(new DebugLoggerProvider(consoleService, fileService));
		}

		var app = builder.Build();
		
		// Log application startup
		var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
		var logger = loggerFactory.CreateLogger("Inductobot.Startup");
		logger.LogInformation("🚀 Inductobot application starting...");
		
		if (debugConfig.ShowConsole)
		{
			logger.LogInformation("Debug console enabled - Log level: {LogLevel}", debugConfig.ConsoleLogLevel);
		}
		
		if (debugConfig.EnableFileLogging)
		{
			logger.LogInformation("File logging enabled - Log level: {LogLevel}, Directory: {LogDirectory}", 
				debugConfig.FileLogLevel, debugConfig.LogDirectory);
		}
		
		return app;
	}
}
