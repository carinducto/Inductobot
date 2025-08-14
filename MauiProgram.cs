using Microsoft.Extensions.Logging;
using Inductobot.Services.Communication;
using Inductobot.Services.Core;
using Inductobot.Services.Data;
using Inductobot.Services.Device;
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

		// Register Services - Singletons (live throughout app lifetime)
		builder.Services.AddSingleton<ITcpDeviceService, TcpDeviceService>();
		builder.Services.AddSingleton<ByteSnapTcpClient>();
		builder.Services.AddSingleton<IDeviceDiscoveryService, DeviceDiscoveryService>();
		builder.Services.AddSingleton<IMessagingService, MessagingService>();
		builder.Services.AddSingleton<INavigationService, NavigationService>();
		
		// Register ViewModels - Transient (new instance each time)
		builder.Services.AddTransient<MainViewModel>();
		
		// Register Views
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<DeviceConnectionPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
