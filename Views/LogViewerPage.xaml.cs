using Inductobot.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace Inductobot.Views;

public partial class LogViewerPage : ContentPage
{
    private readonly ILogViewingService _logService;
    private readonly ILogger<LogViewerPage> _logger;
    private LogFileInfo[] _logFiles = Array.Empty<LogFileInfo>();
    private LogEntry[] _currentEntries = Array.Empty<LogEntry>();
    private LogFileInfo? _selectedLogFile;
    
    public LogViewerPage(ILogViewingService logService, ILogger<LogViewerPage> logger)
    {
        InitializeComponent();
        _logService = logService;
        _logger = logger;
        
        Initialize();
    }
    
    // Parameterless constructor for XAML navigation
    public LogViewerPage() : this(GetService<ILogViewingService>(), GetService<ILogger<LogViewerPage>>())
    {
    }
    
    private static T GetService<T>() where T : class
    {
        try
        {
            var services = Application.Current?.Handler?.MauiContext?.Services;
            if (services == null)
            {
                throw new InvalidOperationException("MauiContext.Services not available - app may not be fully initialized");
            }
            
            if (services.GetService<T>() is T service)
            {
                return service;
            }
            
            throw new InvalidOperationException($"Service {typeof(T).Name} not registered in DI container");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to resolve service {typeof(T).Name}: {ex.Message}", ex);
        }
    }
    
    private void Initialize()
    {
        try
        {
            LogLevelPicker.SelectedIndex = 0; // All Levels
            StatusLabel.Text = "Initializing log viewer...";
            _ = LoadLogFilesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing log viewer");
            StatusLabel.Text = $"Initialization error: {ex.Message}";
        }
    }
    
    private async Task LoadLogFilesAsync()
    {
        try
        {
            StatusLabel.Text = "Loading log files...";
            
            _logFiles = await _logService.GetLogFilesAsync();
            LogFilesCollectionView.ItemsSource = _logFiles;
            
            StatusLabel.Text = $"Found {_logFiles.Length} log files";
            
            if (_logFiles.Length > 0)
            {
                // Select the most recent log file by default
                LogFilesCollectionView.SelectedItem = _logFiles[0];
                await LoadSelectedLogFileAsync(_logFiles[0]);
            }
            else
            {
                StatusLabel.Text = "No log files found. Try generating some logs by using the app.";
                LogCountLabel.Text = "Log Entries (0)";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading log files: {Error}", ex.Message);
            StatusLabel.Text = $"Error loading log files: {ex.Message}";
            
            // Show more helpful error message
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert("Log Loading Error", 
                    $"Failed to load log files:\n{ex.Message}\n\nThis might be because:\n• Log directory doesn't exist\n• Permission issues\n• Service not properly configured", 
                    "OK");
            });
        }
    }
    
    private async Task LoadSelectedLogFileAsync(LogFileInfo logFile)
    {
        try
        {
            StatusLabel.Text = $"Loading entries from {logFile.FileName}...";
            _selectedLogFile = logFile;
            
            var minLevel = GetSelectedLogLevel();
            var searchText = string.IsNullOrWhiteSpace(SearchEntry.Text) ? null : SearchEntry.Text;
            
            _currentEntries = await _logService.ReadLogFileAsync(logFile.FilePath, minLevel, searchText, 1000);
            LogEntriesCollectionView.ItemsSource = _currentEntries;
            
            LogCountLabel.Text = $"Log Entries ({_currentEntries.Length})";
            StatusLabel.Text = $"Loaded {_currentEntries.Length} entries from {logFile.FileName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading log entries from {FilePath}", logFile.FilePath);
            StatusLabel.Text = $"Error loading {logFile.FileName}";
        }
    }
    
    private LogLevel? GetSelectedLogLevel()
    {
        return LogLevelPicker.SelectedIndex switch
        {
            0 => null, // All Levels
            1 => LogLevel.Critical,
            2 => LogLevel.Error,
            3 => LogLevel.Warning,
            4 => LogLevel.Information,
            5 => LogLevel.Debug,
            6 => LogLevel.Trace,
            _ => null
        };
    }
    
    private async void OnLogFileSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is LogFileInfo selectedFile)
        {
            await LoadSelectedLogFileAsync(selectedFile);
        }
    }
    
    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await LoadLogFilesAsync();
    }
    
    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_selectedLogFile != null)
        {
            // Debounce search to avoid too many requests
            await Task.Delay(500);
            if (SearchEntry.Text == e.NewTextValue) // Only search if text hasn't changed
            {
                await LoadSelectedLogFileAsync(_selectedLogFile);
            }
        }
    }
    
    private async void OnLogLevelChanged(object sender, EventArgs e)
    {
        if (_selectedLogFile != null)
        {
            await LoadSelectedLogFileAsync(_selectedLogFile);
        }
    }
    
    private async void OnExportClicked(object sender, EventArgs e)
    {
        try
        {
            if (_currentEntries.Length == 0)
            {
                await DisplayAlert("Export", "No log entries to export.", "OK");
                return;
            }
            
            StatusLabel.Text = "Exporting logs...";
            
            var format = await DisplayActionSheet("Export Format", "Cancel", null, "Text File", "JSON File");
            if (format == "Cancel") return;
            
            var fileFormat = format == "JSON File" ? "json" : "txt";
            var exportPath = await _logService.ExportLogsAsync(_currentEntries, fileFormat);
            
            if (exportPath != null)
            {
                StatusLabel.Text = $"Exported to {Path.GetFileName(exportPath)}";
                await DisplayAlert("Export Success", $"Logs exported to:\n{exportPath}", "OK");
            }
            else
            {
                StatusLabel.Text = "Export failed";
                await DisplayAlert("Export Failed", "Failed to export log entries.", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting logs");
            StatusLabel.Text = "Export error";
            await DisplayAlert("Export Error", "An error occurred while exporting logs.", "OK");
        }
    }
    
    private async void OnClearAllLogsClicked(object sender, EventArgs e)
    {
        try
        {
            var confirm = await DisplayAlert("Clear All Logs", 
                "Are you sure you want to delete all log files? This action cannot be undone.", 
                "Delete All", "Cancel");
            
            if (!confirm) return;
            
            StatusLabel.Text = "Clearing all logs...";
            
            int deletedCount = 0;
            foreach (var logFile in _logFiles)
            {
                if (await _logService.DeleteLogFileAsync(logFile.FilePath))
                {
                    deletedCount++;
                }
            }
            
            StatusLabel.Text = $"Deleted {deletedCount} log files";
            await DisplayAlert("Success", $"Deleted {deletedCount} log files.", "OK");
            
            // Refresh the view
            await LoadLogFilesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing all logs");
            StatusLabel.Text = "Error clearing logs";
            await DisplayAlert("Error", "An error occurred while clearing logs.", "OK");
        }
    }
    
    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}