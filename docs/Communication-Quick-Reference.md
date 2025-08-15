# UAS Communication - Developer Quick Reference

## Quick Start Checklist

### ✅ Connection Best Practices
```csharp
// ✅ DO: Use cancellation tokens
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var result = await tcpService.ConnectAsync(device, cts.Token);

// ✅ DO: Handle connection state changes
tcpService.ConnectionStateChanged += (s, state) => {
    StatusLabel.Text = state.ToString();
};

// ❌ DON'T: Connect without timeout
var result = await tcpService.ConnectAsync(device); // No cancellation!
```

### ✅ UI Operation Patterns
```csharp
private CancellationTokenSource? _operationCts;

private async void OnLongOperationClicked(object sender, EventArgs e)
{
    _operationCts?.Cancel();
    _operationCts = new CancellationTokenSource();
    
    try
    {
        StatusLabel.Text = "Working...";
        await SomeLongOperation(_operationCts.Token);
        StatusLabel.Text = "Completed";
    }
    catch (OperationCanceledException)
    {
        StatusLabel.Text = "Cancelled";
    }
    finally
    {
        _operationCts?.Dispose();
        _operationCts = null;
    }
}

protected override void OnDisappearing()
{
    base.OnDisappearing();
    _operationCts?.Cancel();
    _operationCts?.Dispose();
}
```

## Common Patterns

### 1. Safe Connection with User Feedback
```csharp
private async Task<bool> SafeConnectAsync(string ip, int port)
{
    using var cts = new CancellationTokenSource();
    
    try
    {
        StatusLabel.Text = "Connecting...";
        
        var connectTask = _tcpClient.ConnectAsync(ip, port, cts.Token);
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(15), cts.Token);
        
        var completedTask = await Task.WhenAny(connectTask, timeoutTask);
        
        if (completedTask == timeoutTask)
        {
            var continueWaiting = await DisplayAlert("Connection Timeout",
                $"Connection to {ip}:{port} is taking longer than expected.",
                "Keep Trying", "Cancel");
                
            if (!continueWaiting)
            {
                cts.Cancel();
                return false;
            }
            
            return await connectTask; // Wait for completion
        }
        
        return await connectTask;
    }
    catch (OperationCanceledException)
    {
        StatusLabel.Text = "Connection cancelled";
        return false;
    }
    catch (Exception ex)
    {
        StatusLabel.Text = $"Connection failed: {ex.Message}";
        return false;
    }
}
```

### 2. Command Execution with Timeout
```csharp
private async Task<T> ExecuteCommandAsync<T>(string command, int timeoutMs = 10000)
{
    var request = new CommandRequest
    {
        CommandId = Guid.NewGuid().ToString(),
        Command = command,
        Type = CommandType.Query,
        TimeoutMs = timeoutMs
    };
    
    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs + 5000));
    var response = await _tcpService.SendCommandAsync(request, cts.Token);
    
    if (response.Success)
    {
        return JsonSerializer.Deserialize<T>(response.Data.ToString());
    }
    
    throw new InvalidOperationException($"Command failed: {response.Message}");
}
```

### 3. Discovery with Cancellation
```csharp
private async Task DiscoverDevicesWithCancellationAsync()
{
    using var scanCts = new CancellationTokenSource();
    
    try
    {
        ScanButton.Text = "Stop Scan";
        ScanButton.BackgroundColor = Colors.Red;
        
        await _discoveryService.StartScanAsync(scanCts.Token);
    }
    catch (OperationCanceledException)
    {
        // User cancelled - this is normal
    }
    finally
    {
        ScanButton.Text = "Scan Network";
        ScanButton.BackgroundColor = Colors.Blue;
    }
}
```

## Error Handling Patterns

### 1. Connection Error Classification
```csharp
private void HandleConnectionError(CommandResponse response)
{
    switch (response.Code)
    {
        case ResponseCode.ConnectionError:
            // Device unreachable, network issues
            ShowRetryDialog("Connection lost", "Retry connection?");
            break;
            
        case ResponseCode.Timeout:
            // Operation took too long
            ShowMessage("Operation timed out. Try again or check device status.");
            break;
            
        case ResponseCode.ServiceUnavailable:
            // Device not ready
            ShowMessage("Device is busy. Please wait and try again.");
            break;
            
        default:
            ShowMessage($"Error: {response.Message}");
            break;
    }
}
```

### 2. Automatic Recovery
```csharp
private async Task<bool> ExecuteWithRetryAsync<T>(Func<CancellationToken, Task<T>> operation)
{
    const int maxRetries = 3;
    
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10 * attempt));
            await operation(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw; // Don't retry cancellations
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            _logger.LogWarning($"Attempt {attempt} failed: {ex.Message}. Retrying...");
            await Task.Delay(1000 * attempt); // Progressive delay
        }
    }
    
    return false;
}
```

## Memory Management

### 1. Proper Disposal Pattern
```csharp
public class DevicePage : ContentPage, IDisposable
{
    private CancellationTokenSource? _pageCts;
    private readonly List<IDisposable> _disposables = new();
    
    public DevicePage()
    {
        InitializeComponent();
        _pageCts = new CancellationTokenSource();
    }
    
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _pageCts?.Cancel();
    }
    
    public void Dispose()
    {
        _pageCts?.Cancel();
        _pageCts?.Dispose();
        
        foreach (var disposable in _disposables)
        {
            disposable?.Dispose();
        }
        _disposables.Clear();
        
        GC.SuppressFinalize(this);
    }
}
```

### 2. Resource Tracking
```csharp
private void TrackDisposable(IDisposable resource)
{
    _disposables.Add(resource);
}

private CancellationTokenSource CreateTrackedCts(TimeSpan timeout)
{
    var cts = new CancellationTokenSource(timeout);
    TrackDisposable(cts);
    return cts;
}
```

## Performance Optimization

### 1. Connection Pooling
```csharp
public class ConnectionPool : IDisposable
{
    private readonly ConcurrentDictionary<string, ITcpDeviceService> _connections = new();
    
    public async Task<ITcpDeviceService> GetConnectionAsync(string endpoint)
    {
        return _connections.GetOrAdd(endpoint, key => 
        {
            var service = new TcpDeviceService(_logger);
            var parts = key.Split(':');
            _ = service.ConnectAsync(parts[0], int.Parse(parts[1]));
            return service;
        });
    }
    
    public void Dispose()
    {
        foreach (var connection in _connections.Values)
        {
            connection.Dispose();
        }
        _connections.Clear();
    }
}
```

### 2. Batch Operations
```csharp
private async Task<List<T>> ExecuteBatchAsync<T>(IEnumerable<CommandRequest> commands)
{
    const int batchSize = 5;
    var results = new List<T>();
    
    foreach (var batch in commands.Chunk(batchSize))
    {
        var batchTasks = batch.Select(cmd => _tcpService.SendCommandAsync(cmd));
        var batchResults = await Task.WhenAll(batchTasks);
        
        results.AddRange(batchResults.Where(r => r.Success)
                                   .Select(r => JsonSerializer.Deserialize<T>(r.Data.ToString())));
    }
    
    return results;
}
```

## Debugging Tips

### 1. Enable Detailed Logging
```csharp
// In Startup.cs or MauiProgram.cs
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// In your service
_logger.LogDebug($"Connection attempt {attempt} to {device.IpAddress}:{device.Port}");
_logger.LogTrace($"Sending command: {request.Command}");
```

### 2. Connection State Monitoring
```csharp
private void MonitorConnection()
{
    _tcpService.ConnectionStateChanged += (s, state) =>
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ConnectionIndicator.BackgroundColor = state switch
            {
                ConnectionState.Connected => Colors.Green,
                ConnectionState.Connecting => Colors.Yellow,
                ConnectionState.Error => Colors.Red,
                _ => Colors.Gray
            };
        });
    };
}
```

### 3. Performance Metrics
```csharp
private void TrackPerformance()
{
    var stopwatch = Stopwatch.StartNew();
    
    _tcpService.ConnectionStateChanged += (s, state) =>
    {
        if (state == ConnectionState.Connected)
        {
            stopwatch.Stop();
            _logger.LogInformation($"Connection established in {stopwatch.ElapsedMilliseconds}ms");
        }
    };
}
```

## Configuration Examples

### 1. Custom Timeouts
```csharp
public class CustomTcpService : TcpDeviceService
{
    protected override TimeSpan ConnectionTimeout => TimeSpan.FromSeconds(20);
    protected override TimeSpan CommandTimeout => TimeSpan.FromSeconds(15);
    protected override int MaxRetries => 5;
}
```

### 2. Environment-Specific Settings
```csharp
public class CommunicationConfig
{
    public static TimeSpan GetConnectionTimeout()
    {
        return Environment.GetEnvironmentVariable("ENVIRONMENT") switch
        {
            "Development" => TimeSpan.FromSeconds(60), // Longer for debugging
            "Testing" => TimeSpan.FromSeconds(5),      // Faster for tests
            _ => TimeSpan.FromSeconds(10)              // Production default
        };
    }
}
```

## Testing Patterns

### 1. Mock Connection Testing
```csharp
[Test]
public async Task ConnectAsync_WithTimeout_ShouldCancel()
{
    // Arrange
    var mockTcpClient = new Mock<ITcpDeviceService>();
    mockTcpClient.Setup(x => x.ConnectAsync(It.IsAny<UASDeviceInfo>(), It.IsAny<CancellationToken>()))
              .Returns(Task.Delay(TimeSpan.FromMinutes(1))); // Simulate long operation
    
    // Act
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
    var exception = await Assert.ThrowsAsync<OperationCanceledException>(
        () => mockTcpClient.Object.ConnectAsync(device, cts.Token));
    
    // Assert
    Assert.True(cts.Token.IsCancellationRequested);
}
```

### 2. Integration Testing
```csharp
[Test]
public async Task RealDevice_ConnectionRetry_ShouldSucceedEventually()
{
    var service = new TcpDeviceService(_logger);
    var device = new UASDeviceInfo { IpAddress = "192.168.1.100", Port = 80 };
    
    // Test with network conditions
    var result = await service.ConnectAsync(device);
    
    Assert.True(result || service.ConnectionState == ConnectionState.Error);
}
```

## Common Pitfalls to Avoid

### ❌ Don't Do This
```csharp
// Missing cancellation support
await _tcpService.ConnectAsync(device); // Can hang forever

// Not disposing cancellation tokens
var cts = new CancellationTokenSource();
await SomeOperation(cts.Token); // Memory leak

// Blocking UI thread
var result = _tcpService.ConnectAsync(device).Result; // UI freeze

// Swallowing cancellation exceptions
try { await operation(token); }
catch (Exception) { } // Hides important cancellation info
```

### ✅ Do This Instead
```csharp
// Proper cancellation support
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await _tcpService.ConnectAsync(device, cts.Token);

// Async all the way
var result = await _tcpService.ConnectAsync(device);

// Handle cancellation properly
try
{
    await operation(token);
}
catch (OperationCanceledException) when (token.IsCancellationRequested)
{
    // User cancelled - this is expected
    return;
}
```

## Troubleshooting Quick Fixes

| Problem | Quick Fix |
|---------|-----------|
| Connection hangs | Add `CancellationTokenSource` with timeout |
| Memory growing | Check for undisposed `CancellationTokenSource` |
| UI freezing | Ensure all network calls are `await`ed |
| Operations not cancelling | Use `CreateLinkedTokenSource` for nested operations |
| Timeouts too short | Use progressive timeouts (10s → 15s → 20s) |
| Too many retries | Limit to 3 attempts with exponential backoff |

## Reference Links

- [Main Documentation](./Communication-Improvements.md)
- [TcpDeviceService.cs:42](../Services/Communication/TcpDeviceService.cs#L42) - Connection retry logic
- [ByteSnapTcpClient.cs:107](../Services/Communication/ByteSnapTcpClient.cs#L107) - Command execution
- [DeviceConnectionPage.xaml.cs:113](../Views/DeviceConnectionPage.xaml.cs#L113) - UI cancellation patterns