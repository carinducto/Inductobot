using Microsoft.Extensions.Logging;

namespace Inductobot.Services.Core;

public class NavigationService : INavigationService
{
    private readonly ILogger<NavigationService> _logger;

    public NavigationService(ILogger<NavigationService> logger)
    {
        _logger = logger;
    }

    public async Task NavigateToAsync(string route, Dictionary<string, object>? parameters = null)
    {
        try
        {
            if (parameters != null)
            {
                await Shell.Current.GoToAsync(route, parameters);
            }
            else
            {
                await Shell.Current.GoToAsync(route);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to navigate to {route}");
        }
    }

    public async Task NavigateToAsync<TViewModel>(Dictionary<string, object>? parameters = null) where TViewModel : class
    {
        var route = typeof(TViewModel).Name.Replace("ViewModel", "").ToLower();
        await NavigateToAsync(route, parameters);
    }

    public async Task GoBackAsync()
    {
        try
        {
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate back");
        }
    }

    public async Task GoToRootAsync()
    {
        try
        {
            await Shell.Current.GoToAsync("//");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to root");
        }
    }

    public async Task ShowPopupAsync(string message, string title = "Information")
    {
        try
        {
            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.DisplayAlert(title, message, "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to show popup: {title}");
        }
    }

    public async Task<bool> ShowConfirmationAsync(string message, string title = "Confirm")
    {
        try
        {
            if (Application.Current?.MainPage != null)
            {
                return await Application.Current.MainPage.DisplayAlert(title, message, "Yes", "No");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to show confirmation: {title}");
        }
        
        return false;
    }

    public async Task<string?> ShowInputAsync(string message, string title = "Input", string? defaultValue = null)
    {
        try
        {
            if (Application.Current?.MainPage != null)
            {
                return await Application.Current.MainPage.DisplayPromptAsync(title, message, initialValue: defaultValue);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to show input dialog: {title}");
        }
        
        return null;
    }
}