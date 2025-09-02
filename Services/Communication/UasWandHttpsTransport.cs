using Inductobot.Abstractions.Communication;
using Inductobot.Abstractions.Services;
using Inductobot.Models.Device;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Inductobot.Services.Communication;

/// <summary>
/// HTTPS transport implementation for UAS-WAND devices using legacy working SSL configuration
/// </summary>
public class UasWandHttpsTransport : IUasWandTransport, IDisposable
{
    private readonly ILogger<UasWandHttpsTransport> _logger;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    
    private ConnectionState _connectionState = ConnectionState.Disconnected;
    private string? _baseUrl;
    
    // UAS device credentials (matches real devices)
    private const string Username = "test";
    private const string Password = "0000";
    
    public ConnectionState ConnectionState => _connectionState;
    
    public bool IsConnected => _connectionState == ConnectionState.Connected && _baseUrl != null;
    
    public UASDeviceInfo? CurrentDevice { get; private set; }
    
    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    
    public UasWandHttpsTransport(ILogger<UasWandHttpsTransport> logger, IConfigurationService config)
    {
        _logger = logger;
        
        // Initialize JSON options for UAS API compatibility
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        
        // Use legacy working SSL configuration - simple HttpClientHandler with bypass validation
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
        };
        
        _httpClient = new HttpClient(handler);
        
        // Legacy working configuration: 10-minute timeout with basic auth
        _httpClient.Timeout = TimeSpan.FromMinutes(10);
        
        // Set up Basic Authentication (UAS devices use basic auth)
        var authBytes = Encoding.ASCII.GetBytes($"{Username}:{Password}");
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        
        _logger.LogInformation("UAS HTTPS transport configured using legacy working SSL configuration");
    }
    
    public async Task<bool> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        try
        {
            _baseUrl = $"https://{host}:{port}";
            
            _logger.LogInformation("Connecting to UAS-WAND at {BaseUrl}", _baseUrl);
            
            SetConnectionState(ConnectionState.Connecting);
            
            // Test connection with a simple ping
            var response = await _httpClient.GetAsync(_baseUrl + "/ping", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                SetConnectionState(ConnectionState.Connected);
                _logger.LogInformation("Successfully connected to UAS-WAND at {BaseUrl}", _baseUrl);
                return true;
            }
            else
            {
                _logger.LogWarning("Connection test failed: HTTP {StatusCode}", response.StatusCode);
                SetConnectionState(ConnectionState.Disconnected);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to UAS-WAND at {Host}:{Port}", host, port);
            SetConnectionState(ConnectionState.Disconnected);
            return false;
        }
    }
    
    public async Task<bool> ConnectAsync(UASDeviceInfo device, CancellationToken cancellationToken = default)
    {
        CurrentDevice = device;
        return await ConnectAsync(device.IpAddress, device.Port, cancellationToken);
    }
    
    public Task DisconnectAsync()
    {
        _baseUrl = null;
        CurrentDevice = null;
        SetConnectionState(ConnectionState.Disconnected);
        _logger.LogInformation("Disconnected from UAS-WAND");
        return Task.CompletedTask;
    }
    
    public async Task<byte[]> SendRawDataAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || string.IsNullOrEmpty(_baseUrl))
        {
            throw new InvalidOperationException("Not connected to device");
        }
        
        try
        {
            using var content = new ByteArrayContent(data);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            
            var response = await _httpClient.PutAsync(_baseUrl + "/serial", content, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync(cancellationToken);
            }
            else
            {
                throw new InvalidOperationException($"Raw data send failed: HTTP {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send raw data");
            throw;
        }
    }
    
    public async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || string.IsNullOrEmpty(_baseUrl))
        {
            throw new InvalidOperationException("Not connected to device");
        }
        
        try
        {
            _logger.LogDebug("Sending HTTPS command: {Command}", command);
            
            // For now, treat commands as simple HTTP requests
            // This can be expanded based on the actual UAS-WAND command protocol
            var response = await _httpClient.GetAsync(_baseUrl + "/" + command.TrimStart('/'), cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("Received HTTPS response: {Response}", 
                    result.Length > 200 ? result[..200] + "..." : result);
                return result;
            }
            else
            {
                throw new InvalidOperationException($"Command failed: HTTP {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send command: {Command}", command);
            throw;
        }
    }
    
    private void SetConnectionState(ConnectionState newState)
    {
        if (_connectionState != newState)
        {
            _connectionState = newState;
            ConnectionStateChanged?.Invoke(this, newState);
        }
    }
    
    public void Dispose()
    {
        try
        {
            _httpClient?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing HTTPS transport");
        }
    }
}