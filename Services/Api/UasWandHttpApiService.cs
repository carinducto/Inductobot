using Inductobot.Abstractions.Services;
using Inductobot.Models.Commands;
using Inductobot.Models.Device;
using Inductobot.Models.Measurements;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Inductobot.Services.Api;

/// <summary>
/// HTTP-based UAS-WAND API service that matches the real device protocol.
/// Uses HTTP/HTTPS with Basic Authentication (username: "test", password: "0000").
/// Sends standard HTTP requests to device endpoints and parses JSON responses.
/// </summary>
public class UasWandHttpApiService : IUasWandApiService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UasWandHttpApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    
    private string? _baseUrl;
    
    // UAS device credentials (matches real devices)
    private const string Username = "test";
    private const string Password = "0000";
    
    public UasWandHttpApiService(ILogger<UasWandHttpApiService> logger)
    {
        _logger = logger;
        
        // Create HTTP client with Basic Auth
        _httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        });
        
        // Set up Basic Authentication
        var authBytes = Encoding.ASCII.GetBytes($"{Username}:{Password}");
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }
    
    public void SetBaseUrl(string baseUrl)
    {
        _baseUrl = baseUrl?.TrimEnd('/');
        _logger.LogDebug("UAS-WAND HTTP API base URL set to: {BaseUrl}", _baseUrl);
    }
    
    public async Task<ApiResponse<UASDeviceInfo>> GetDeviceInfoAsync(CancellationToken cancellationToken = default)
    {
        return await SendHttpRequestAsync<UASDeviceInfo>("GET", "/info", null, cancellationToken);
    }
    
    public async Task<ApiResponse<CodedResponse>> KeepAliveAsync(CancellationToken cancellationToken = default)
    {
        return await SendHttpRequestAsync<CodedResponse>("GET", "/ping", null, cancellationToken);
    }
    
    public async Task<ApiResponse<WifiConfiguration>> GetWifiSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await SendHttpRequestAsync<WifiConfiguration>("GET", "/wifi", null, cancellationToken);
    }
    
    public async Task<ApiResponse<CodedResponse>> SetWifiSettingsAsync(WifiSettings settings, CancellationToken cancellationToken = default)
    {
        return await SendHttpRequestAsync<CodedResponse>("POST", "/wifi", settings, cancellationToken);
    }
    
    public async Task<ApiResponse<CodedResponse>> RestartWifiAsync(CancellationToken cancellationToken = default)
    {
        return await SendHttpRequestAsync<CodedResponse>("POST", "/wifi/restart", null, cancellationToken);
    }
    
    public async Task<ApiResponse<CodedResponse>> SleepAsync(CancellationToken cancellationToken = default)
    {
        return await SendHttpRequestAsync<CodedResponse>("POST", "/sleep", null, cancellationToken);
    }
    
    public async Task<ApiResponse<ScanStatus>> StartScanAsync(ScanTask task, CancellationToken cancellationToken = default)
    {
        var payload = new { scan = (int)task };
        return await SendHttpRequestAsync<ScanStatus>("POST", "/scan", payload, cancellationToken);
    }
    
    public async Task<ApiResponse<ScanStatus>> GetScanStatusAsync(CancellationToken cancellationToken = default)
    {
        return await SendHttpRequestAsync<ScanStatus>("GET", "/scan", null, cancellationToken);
    }
    
    public async Task<ApiResponse<LiveReadingData>> GetLiveReadingAsync(int startIndex, int numPoints, CancellationToken cancellationToken = default)
    {
        var endpoint = $"/live?startIndex={startIndex}&numPoints={numPoints}";
        return await SendHttpRequestAsync<LiveReadingData>("GET", endpoint, null, cancellationToken);
    }
    
    public async Task<ApiResponse<MeasurementData>> GetMeasurementAsync(CancellationToken cancellationToken = default)
    {
        return await SendHttpRequestAsync<MeasurementData>("GET", "/measurement", null, cancellationToken);
    }
    
    private async Task<ApiResponse<T>> SendHttpRequestAsync<T>(string method, string endpoint, object? payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_baseUrl))
        {
            return ApiResponse<T>.Failure("Base URL not set", "NO_BASE_URL");
        }
        
        var url = _baseUrl + endpoint;
        
        try
        {
            _logger.LogDebug("UAS-WAND HTTP request: {Method} {Url}", method, url);
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            
            HttpResponseMessage response;
            
            if (method.ToUpperInvariant() == "GET")
            {
                response = await _httpClient.GetAsync(url, cts.Token);
            }
            else if (method.ToUpperInvariant() == "POST")
            {
                var content = payload != null 
                    ? new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json")
                    : new StringContent("", Encoding.UTF8, "application/json");
                    
                response = await _httpClient.PostAsync(url, content, cts.Token);
            }
            else
            {
                return ApiResponse<T>.Failure($"Unsupported HTTP method: {method}", "UNSUPPORTED_METHOD");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
            _logger.LogDebug("UAS-WAND HTTP response: {StatusCode} {Content}", 
                (int)response.StatusCode, responseContent.Length > 200 ? responseContent[..200] + "..." : responseContent);
            
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return ApiResponse<T>.Failure("Authentication failed", "UNAUTHORIZED");
            }
            
            if (!response.IsSuccessStatusCode)
            {
                return ApiResponse<T>.Failure($"HTTP error: {response.StatusCode}", "HTTP_ERROR");
            }
            
            // Parse the UAS-WAND API response format
            var apiResponse = SafeDeserialize<ApiResponse<T>>(responseContent);
            if (apiResponse != null)
            {
                _logger.LogDebug("UAS-WAND API response parsed: Success={Success}", apiResponse.IsSuccess);
                return apiResponse;
            }
            
            return ApiResponse<T>.Failure("Failed to parse response", "PARSE_ERROR");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "UAS-WAND HTTP request timeout: {Method} {Url}", method, url);
            return ApiResponse<T>.Failure("Request timeout", "TIMEOUT");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "UAS-WAND HTTP request failed: {Method} {Url}", method, url);
            return ApiResponse<T>.Failure($"HTTP request failed: {ex.Message}", "HTTP_REQUEST_ERROR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in UAS-WAND HTTP request: {Method} {Url}", method, url);
            return ApiResponse<T>.Failure($"Unexpected error: {ex.Message}", "UNEXPECTED_ERROR");
        }
    }
    
    private T? SafeDeserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JSON deserialization error. JSON: {Json}", 
                json?.Length > 100 ? json[..100] + "..." : json);
            return default;
        }
    }
    
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}