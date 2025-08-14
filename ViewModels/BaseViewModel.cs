using System.ComponentModel;
using System.Runtime.CompilerServices;
using Inductobot.Services.Core;

namespace Inductobot.ViewModels;

public abstract class BaseViewModel : INotifyPropertyChanged
{
    private bool _isBusy;
    private string _title = string.Empty;
    
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }
    
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }
    
    public bool IsNotBusy => !IsBusy;
    
    protected INavigationService NavigationService { get; }
    protected IMessagingService MessagingService { get; }
    
    protected BaseViewModel(INavigationService navigationService, IMessagingService messagingService)
    {
        NavigationService = navigationService;
        MessagingService = messagingService;
    }
    
    public virtual Task InitializeAsync(Dictionary<string, object>? parameters = null)
    {
        return Task.CompletedTask;
    }
    
    public virtual Task OnAppearingAsync()
    {
        return Task.CompletedTask;
    }
    
    public virtual Task OnDisappearingAsync()
    {
        return Task.CompletedTask;
    }
    
    protected bool SetProperty<T>(ref T backingStore, T value,
        [CallerMemberName] string propertyName = "",
        Action? onChanged = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
            return false;
        
        backingStore = value;
        onChanged?.Invoke();
        OnPropertyChanged(propertyName);
        return true;
    }
    
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected async Task ExecuteAsync(Func<Task> operation, string? loadingMessage = null)
    {
        if (IsBusy)
            return;
        
        try
        {
            IsBusy = true;
            
            if (!string.IsNullOrEmpty(loadingMessage))
            {
                MessagingService.Send(new StatusMessage(loadingMessage, StatusLevel.Info));
            }
            
            await operation();
        }
        catch (Exception ex)
        {
            MessagingService.Send(new ErrorMessage("Operation Failed", ex.Message, ex));
            await NavigationService.ShowPopupAsync($"An error occurred: {ex.Message}", "Error");
        }
        finally
        {
            IsBusy = false;
        }
    }
}