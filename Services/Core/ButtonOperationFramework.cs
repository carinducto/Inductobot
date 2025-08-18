using Microsoft.Extensions.Logging;

namespace Inductobot.Services.Core;

/// <summary>
/// Configuration for button operations
/// </summary>
public class ButtonOperationConfig
{
    public string OriginalText { get; set; } = "";
    public string LoadingText { get; set; } = "Loading...";
    public string SuccessText { get; set; } = "✅ Success";
    public string ErrorTextFormat { get; set; } = "❌ {0}";
    public string InfoMessage { get; set; } = "";
    public string SuccessMessage { get; set; } = "";
    public string ErrorMessageFormat { get; set; } = "Operation failed: {0}";
    public int SuccessResetDelay { get; set; } = 2000;
    public int ErrorResetDelay { get; set; } = 3000;
    public bool RequireConnection { get; set; } = true;
    public bool CheckBusyState { get; set; } = true;
    public Func<Task<bool>>? PreValidation { get; set; }
}

/// <summary>
/// Base class providing reusable button operation framework
/// </summary>
public abstract class ButtonOperationFrameworkBase
{
    protected readonly ILogger _logger;
    
    protected ButtonOperationFrameworkBase(ILogger logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Override this to provide connection state checking
    /// </summary>
    protected abstract bool IsConnected { get; }
    
    /// <summary>
    /// Override this to provide busy state checking  
    /// </summary>
    protected abstract bool IsBusy { get; }
    
    /// <summary>
    /// Override this to provide status message for errors
    /// </summary>
    protected abstract string StatusMessage { get; }
    
    /// <summary>
    /// Override this to show toast notifications
    /// </summary>
    protected abstract void ShowStatusToast(string message, ToastType type);
    
    /// <summary>
    /// Override this to manage button states
    /// </summary>
    protected abstract void SetButtonLoading(Button button, string loadingText);
    protected abstract void SetButtonSuccess(Button button, string successText);
    protected abstract void SetButtonError(Button button, string errorText);
    protected abstract void SetButtonNormal(Button button, string originalText);
    
    /// <summary>
    /// Override this to provide theme colors
    /// </summary>
    protected abstract Color SecondaryTextColor { get; }
    protected abstract Color SuccessTextColor { get; }
    protected abstract Color ErrorTextColor { get; }
    
    /// <summary>
    /// Executes a button operation with comprehensive feedback and error handling
    /// </summary>
    protected async Task<bool> ExecuteButtonOperationAsync(
        Button button, 
        Func<Task<bool>> operation, 
        ButtonOperationConfig config)
    {
        if (button == null || operation == null) return false;
        
        try
        {
            // Pre-flight validation
            if (config.RequireConnection && !IsConnected)
            {
                ShowStatusToast("Not connected to device. Connect first.", ToastType.Warning);
                return false;
            }
            
            if (config.CheckBusyState && IsBusy)
            {
                ShowStatusToast("Another operation is in progress. Please wait.", ToastType.Warning);
                return false;
            }
            
            // Custom pre-validation
            if (config.PreValidation != null)
            {
                var validationResult = await config.PreValidation();
                if (!validationResult) return false;
            }
            
            // Start operation
            SetButtonLoading(button, config.LoadingText);
            if (!string.IsNullOrEmpty(config.InfoMessage))
            {
                ShowStatusToast(config.InfoMessage, ToastType.Info);
            }
            
            // Execute operation
            var success = await operation();
            
            // Handle result
            if (success)
            {
                SetButtonSuccess(button, config.SuccessText);
                if (!string.IsNullOrEmpty(config.SuccessMessage))
                {
                    ShowStatusToast(config.SuccessMessage, ToastType.Success);
                }
                
                // Auto-reset after success
                _ = ResetButtonAfterDelay(button, config.OriginalText, config.SuccessResetDelay);
                return true;
            }
            else
            {
                var errorText = string.Format(config.ErrorTextFormat, "Failed");
                var errorMessage = string.Format(config.ErrorMessageFormat, StatusMessage);
                
                SetButtonError(button, errorText);
                ShowStatusToast(errorMessage, ToastType.Error);
                
                // Auto-reset after error
                _ = ResetButtonAfterDelay(button, config.OriginalText, config.ErrorResetDelay);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in button operation: {OriginalText}", config.OriginalText);
            
            var errorText = string.Format(config.ErrorTextFormat, "Error");
            var errorMessage = $"Error: {ex.Message}";
            
            SetButtonError(button, errorText);
            ShowStatusToast(errorMessage, ToastType.Error);
            
            // Auto-reset after error
            _ = ResetButtonAfterDelay(button, config.OriginalText, config.ErrorResetDelay);
            return false;
        }
    }
    
    /// <summary>
    /// Executes a button operation with content area updates (for Get Device Info, Get Measurement, etc.)
    /// </summary>
    protected async Task<bool> ExecuteButtonOperationWithContentAsync(
        Button button,
        Label contentLabel,
        Func<Task<bool>> operation,
        ButtonOperationConfig config,
        Func<string> getContentOnSuccess,
        string loadingContent = "Loading...")
    {
        if (contentLabel != null)
        {
            contentLabel.Text = loadingContent;
            contentLabel.TextColor = SecondaryTextColor;
        }
        
        var success = await ExecuteButtonOperationAsync(button, operation, config);
        
        if (success && contentLabel != null)
        {
            contentLabel.Text = getContentOnSuccess();
            contentLabel.TextColor = SuccessTextColor;
        }
        else if (!success && contentLabel != null)
        {
            contentLabel.Text = $"❌ Failed: {StatusMessage}";
            contentLabel.TextColor = ErrorTextColor;
        }
        
        return success;
    }
    
    /// <summary>
    /// Resets button after a delay on a background thread
    /// </summary>
    protected Task ResetButtonAfterDelay(Button button, string originalText, int delayMs)
    {
        return Task.Run(async () =>
        {
            await Task.Delay(delayMs);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SetButtonNormal(button, originalText);
            });
        });
    }
    
    /// <summary>
    /// Creates input validation function for text entries
    /// </summary>
    protected Func<Task<bool>> CreateInputValidation(params (Entry entry, string fieldName, Func<string, bool> validator, string errorMessage)[] validations)
    {
        return () =>
        {
            foreach (var (entry, fieldName, validator, errorMessage) in validations)
            {
                if (entry?.Text == null || !validator(entry.Text))
                {
                    ShowStatusToast(errorMessage, ToastType.Warning);
                    entry?.Focus();
                    return Task.FromResult(false);
                }
            }
            return Task.FromResult(true);
        };
    }
}

/// <summary>
/// Toast notification types
/// </summary>
public enum ToastType
{
    Info,
    Success,
    Warning,
    Error
}