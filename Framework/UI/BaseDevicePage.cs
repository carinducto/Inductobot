using Inductobot.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Graphics;

namespace Inductobot.Framework.UI;

/// <summary>
/// Base page for UAS-WAND device control pages with common functionality
/// </summary>
public abstract class BaseDevicePage : ContentPage
{
    protected readonly ILogger Logger;
    protected readonly ButtonOperationHandler ButtonHandler;
    
    private bool _isPasswordVisible = false;
    private CancellationTokenSource? _passwordVisibilityTimeoutCts;

    protected BaseDevicePage(ILogger logger, ButtonOperationHandler buttonHandler)
    {
        Logger = logger;
        ButtonHandler = buttonHandler;
    }

    /// <summary>
    /// Execute a button operation with standard error handling and UI feedback
    /// </summary>
    protected async Task<bool> ExecuteButtonOperationAsync(
        Button button, 
        Func<Task<bool>> operation, 
        ButtonOperationConfig config,
        Func<bool>? connectionCheck = null)
    {
        return await ButtonHandler.ExecuteAsync(button, operation, config, connectionCheck);
    }

    /// <summary>
    /// Execute a button operation that returns content to display
    /// </summary>
    protected async Task<bool> ExecuteButtonOperationWithContentAsync<T>(
        Button button,
        Label contentLabel,
        Func<Task<(bool Success, T? Data)>> operation,
        ButtonOperationConfig config,
        Func<T, string> formatContent,
        Func<bool>? connectionCheck = null)
    {
        return await ButtonHandler.ExecuteWithContentAsync(
            button, contentLabel, operation, config, formatContent, connectionCheck);
    }

    /// <summary>
    /// Show toast message to user
    /// </summary>
    protected async Task ShowToastAsync(string message, ToastType type = ToastType.Info)
    {
        try
        {
            var color = type switch
            {
                ToastType.Success => Colors.Green,
                ToastType.Warning => Colors.Orange,
                ToastType.Error => Colors.Red,
                _ => Colors.Blue
            };

            // For now, just log - in the future, this could show actual toast notifications
            Logger.LogInformation("Toast ({Type}): {Message}", type, message);
            
            // Could implement platform-specific toast notifications here
            await DisplayAlert(type.ToString(), message, "OK");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error showing toast message");
        }
    }

    /// <summary>
    /// Handle password visibility toggle with security timeout
    /// </summary>
    protected void TogglePasswordVisibility(Label passwordLabel, string actualPassword)
    {
        try
        {
            _isPasswordVisible = !_isPasswordVisible;
            
            if (_isPasswordVisible)
            {
                passwordLabel.Text = actualPassword;
                
                // Set up automatic timeout to hide password for security
                CancelPasswordVisibilityTimeout();
                _passwordVisibilityTimeoutCts = new CancellationTokenSource();
                
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(10000, _passwordVisibilityTimeoutCts.Token); // 10 seconds
                        
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            _isPasswordVisible = false;
                            passwordLabel.Text = "••••••••";
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        // Timeout was cancelled, this is expected
                    }
                });
            }
            else
            {
                passwordLabel.Text = "••••••••";
                CancelPasswordVisibilityTimeout();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error toggling password visibility");
        }
    }

    /// <summary>
    /// Update traffic light connection status indicator (generic method)
    /// </summary>
    protected void UpdateConnectionIndicator(VisualElement indicator, bool isConnected, string? deviceName = null)
    {
        try
        {
            var color = isConnected ? Colors.Green : Colors.Red;
            
            // Try to set background color on the indicator element
            if (indicator is View view)
            {
                view.BackgroundColor = color;
            }
            
            Logger.LogDebug("Connection indicator updated: {Status} for device: {Device}", 
                isConnected ? "Connected" : "Disconnected", deviceName ?? "None");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating connection indicator");
        }
    }

    private void CancelPasswordVisibilityTimeout()
    {
        _passwordVisibilityTimeoutCts?.Cancel();
        _passwordVisibilityTimeoutCts?.Dispose();
        _passwordVisibilityTimeoutCts = null;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Security: Hide password when navigating away
        CancelPasswordVisibilityTimeout();
        _isPasswordVisible = false;
    }
}

/// <summary>
/// Types of toast messages
/// </summary>
public enum ToastType
{
    Info,
    Success, 
    Warning,
    Error
}