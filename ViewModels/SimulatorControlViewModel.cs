using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Inductobot.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace Inductobot.ViewModels;

/// <summary>
/// ViewModel for UAS-WAND simulator control UI
/// </summary>
public class SimulatorControlViewModel : INotifyPropertyChanged
{
    private readonly IUasWandSimulatorService _simulatorService;
    private readonly ILogger<SimulatorControlViewModel> _logger;
    
    public event PropertyChangedEventHandler? PropertyChanged;

    public SimulatorControlViewModel(
        IUasWandSimulatorService simulatorService, 
        ILogger<SimulatorControlViewModel> logger)
    {
        _simulatorService = simulatorService;
        _logger = logger;
        
        // Subscribe to simulator state changes
        _simulatorService.SimulatorStateChanged += OnSimulatorStateChanged;
        
        // Initialize commands
        StartCommand = new AsyncRelayCommand(StartSimulatorAsync, () => !IsRunning);
        StopCommand = new AsyncRelayCommand(StopSimulatorAsync, () => IsRunning);
        RefreshStatusCommand = new AsyncRelayCommand(RefreshStatusAsync);
        
        // Initialize status
        _ = RefreshStatusAsync();
    }

    #region Properties

    public bool IsRunning => _simulatorService.IsRunning;
    public int Port => _simulatorService.Port;
    public string? IPAddress => _simulatorService.IPAddress;

    private SimulatorStatus? _status;
    public SimulatorStatus? Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    private string _statusMessage = "Checking status...";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StartButtonText => IsRunning ? "Running" : "Start Simulator";
    public string StopButtonText => "Stop Simulator";
    public string StatusColor => IsRunning ? "Green" : "Gray";

    #endregion

    #region Commands

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand RefreshStatusCommand { get; }

    #endregion

    #region Command Handlers

    private async Task StartSimulatorAsync()
    {
        IsLoading = true;
        StatusMessage = "Starting simulator...";
        
        try
        {
            var success = await _simulatorService.StartSimulatorAsync();
            if (success)
            {
                StatusMessage = "Simulator started successfully";
                await RefreshStatusAsync();
            }
            else
            {
                StatusMessage = "Failed to start simulator";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting simulator");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task StopSimulatorAsync()
    {
        IsLoading = true;
        StatusMessage = "Stopping simulator...";
        
        try
        {
            var success = await _simulatorService.StopSimulatorAsync();
            if (success)
            {
                StatusMessage = "Simulator stopped successfully";
                await RefreshStatusAsync();
            }
            else
            {
                StatusMessage = "Failed to stop simulator";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping simulator");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshStatusAsync()
    {
        try
        {
            Status = _simulatorService.GetStatus();
            StatusMessage = Status.IsRunning 
                ? $"Running on {Status.IPAddress}:{Status.Port}" 
                : "Stopped";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing status");
            StatusMessage = "Error refreshing status";
        }
        
        // Refresh command states
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(StartButtonText));
        OnPropertyChanged(nameof(StatusColor));
        ((AsyncRelayCommand)StartCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)StopCommand).RaiseCanExecuteChanged();
    }

    #endregion

    #region Event Handlers

    private void OnSimulatorStateChanged(object? sender, bool isRunning)
    {
        // Ensure UI updates happen on the main thread
        if (Application.Current?.Dispatcher?.IsDispatchRequired == true)
        {
            Application.Current.Dispatcher.Dispatch(async () => await RefreshStatusAsync());
        }
        else
        {
            _ = RefreshStatusAsync();
        }
    }

    #endregion

    #region INotifyPropertyChanged

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}

/// <summary>
/// Async relay command implementation
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute?.Invoke() ?? true);
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;

        _isExecuting = true;
        RaiseCanExecuteChanged();

        try
        {
            await _execute();
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

