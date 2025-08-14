namespace Inductobot.Services.Core;

public interface INavigationService
{
    Task NavigateToAsync(string route, Dictionary<string, object>? parameters = null);
    Task NavigateToAsync<TViewModel>(Dictionary<string, object>? parameters = null) where TViewModel : class;
    Task GoBackAsync();
    Task GoToRootAsync();
    Task ShowPopupAsync(string message, string title = "Information");
    Task<bool> ShowConfirmationAsync(string message, string title = "Confirm");
    Task<string?> ShowInputAsync(string message, string title = "Input", string? defaultValue = null);
}