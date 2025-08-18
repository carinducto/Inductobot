using Microsoft.Extensions.Logging;

namespace Inductobot.Framework.UI;

/// <summary>
/// Configuration for button operations including UI feedback and behavior
/// </summary>
public record ButtonOperationConfig
{
    public bool RequireConnection { get; init; } = true;
    public string LoadingText { get; init; } = "Processing...";
    public string SuccessText { get; init; } = "✅ Success";
    public string ErrorText { get; init; } = "❌ Error";
    public string OriginalText { get; init; } = "";
    public string SuccessMessage { get; init; } = "";
    public string InfoMessage { get; init; } = "";
    public int SuccessDisplayDuration { get; init; } = 2000;
}

/// <summary>
/// Centralized button operation handler for consistent UI feedback and error handling
/// </summary>
public class ButtonOperationHandler
{
    private readonly ILogger<ButtonOperationHandler> _logger;
    
    public ButtonOperationHandler(ILogger<ButtonOperationHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Execute a button operation with standardized UI feedback
    /// </summary>
    public async Task<bool> ExecuteAsync(
        Button button, 
        Func<Task<bool>> operation, 
        ButtonOperationConfig config,
        Func<bool>? connectionCheck = null)
    {
        if (button == null)
        {
            _logger.LogWarning("Button is null in ExecuteAsync");
            return false;
        }

        // Connection check
        if (config.RequireConnection && connectionCheck?.Invoke() == false)
        {
            _logger.LogWarning("Button operation blocked - connection required but not available");
            return false;
        }

        var originalText = button.Text;
        var originalBackgroundColor = button.BackgroundColor;

        try
        {
            // Set loading state
            SetButtonLoading(button, config.LoadingText);
            
            // Show info message if provided
            if (!string.IsNullOrEmpty(config.InfoMessage))
            {
                // This would need to be injected or passed in
                // await ShowInfoMessage(config.InfoMessage);
            }

            // Execute operation
            var success = await operation();

            if (success)
            {
                SetButtonSuccess(button, config.SuccessText);
                
                // Show success message if provided
                if (!string.IsNullOrEmpty(config.SuccessMessage))
                {
                    // await ShowSuccessMessage(config.SuccessMessage);
                }
                
                // Reset button after delay
                _ = Task.Run(async () =>
                {
                    await Task.Delay(config.SuccessDisplayDuration);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        SetButtonDefault(button, originalText, originalBackgroundColor);
                    });
                });
                
                return true;
            }
            else
            {
                SetButtonError(button, config.ErrorText);
                
                // Reset button after delay
                _ = Task.Run(async () =>
                {
                    await Task.Delay(config.SuccessDisplayDuration);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        SetButtonDefault(button, originalText, originalBackgroundColor);
                    });
                });
                
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing button operation");
            SetButtonError(button, config.ErrorText);
            
            // Reset button after delay
            _ = Task.Run(async () =>
            {
                await Task.Delay(config.SuccessDisplayDuration);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SetButtonDefault(button, originalText, originalBackgroundColor);
                });
            });
            
            return false;
        }
    }

    /// <summary>
    /// Execute a button operation with content update (for operations that return display content)
    /// </summary>
    public async Task<bool> ExecuteWithContentAsync<T>(
        Button button,
        Label contentLabel,
        Func<Task<(bool Success, T? Data)>> operation,
        ButtonOperationConfig config,
        Func<T, string> formatContent,
        Func<bool>? connectionCheck = null)
    {
        var success = await ExecuteAsync(button, async () =>
        {
            var (operationSuccess, data) = await operation();
            if (operationSuccess && data != null)
            {
                var formattedContent = formatContent(data);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    contentLabel.Text = formattedContent;
                });
            }
            return operationSuccess;
        }, config, connectionCheck);

        return success;
    }

    private static void SetButtonLoading(Button button, string loadingText)
    {
        button.Text = loadingText;
        button.BackgroundColor = Colors.Orange;
        button.IsEnabled = false;
    }

    private static void SetButtonSuccess(Button button, string successText)
    {
        button.Text = successText;
        button.BackgroundColor = Colors.Green;
        button.IsEnabled = true;
    }

    private static void SetButtonError(Button button, string errorText)
    {
        button.Text = errorText;
        button.BackgroundColor = Colors.Red;
        button.IsEnabled = true;
    }

    private static void SetButtonDefault(Button button, string originalText, Color originalBackgroundColor)
    {
        button.Text = originalText;
        button.BackgroundColor = originalBackgroundColor;
        button.IsEnabled = true;
    }
}