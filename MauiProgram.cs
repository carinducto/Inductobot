using Microsoft.Extensions.Logging;
using Inductobot.Extensions;
using Inductobot.Services.Core;
using Inductobot.Services.Data;
using Inductobot.Services.Device;
using Inductobot.Services.Simulation;
using Inductobot.ViewModels;
using Inductobot.Views;

namespace Inductobot;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// UAS-WAND simulator will use hardcoded configuration for simplicity

		// Register UAS-WAND Services - Modular architecture with clean separation
		builder.Services.AddUasWandServices();
		
		// Register Core Services
		builder.Services.AddSingleton<IMessagingService, MessagingService>();
		builder.Services.AddSingleton<INavigationService, NavigationService>();
		
		// Register UAS-WAND Simulator (for testing without real device)
		builder.Services.AddHostedService<UasWandSimulatorService>();
		
		// Register ViewModels - Transient (new instance each time)
		builder.Services.AddTransient<MainViewModel>();
		// UasWandControlViewModel is already registered by AddUasWandServices()
		
		// Register Views
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<DeviceConnectionPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
