using Inductobot.Views;

namespace Inductobot;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		
		// Register routes for dependency injection support
		Routing.RegisterRoute("DeviceConnection", typeof(DeviceConnectionPage));
		Routing.RegisterRoute("DeviceControl", typeof(MainPage));
	}
}
