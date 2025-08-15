using Inductobot.Abstractions.Communication;
using Inductobot.Abstractions.Services;
using Inductobot.Models.Commands;
using Inductobot.Models.Device;
using Inductobot.Models.Measurements;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Inductobot.Services.Api;

/// <summary>
/// Implementation of UAS-WAND API service using HTTP-like commands over transport
/// </summary>
public class UasWandApiService : IUasWandApiService
{
    private readonly IUasWandTransport _transport;
    private readonly ILogger<UasWandApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public UasWandApiService(IUasWandTransport transport, ILogger<UasWandApiService> logger)
    {
        _transport = transport;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }
    
    public async Task<ApiResponse<UASDeviceInfo>> GetDeviceInfoAsync(CancellationToken cancellationToken = default)
    {
        return await SendHttpCommandAsync<UASDeviceInfo>("/info", HttpMethod.Get, null, cancellationToken);
    }
    
    public async Task<ApiResponse<CodedResponse>> KeepAliveAsync(CancellationToken cancellationToken = default)
    {
        return await SendHttpCommandAsync<CodedResponse>("/ping", HttpMethod.Get, null, cancellationToken);
    }
    
    public async Task<ApiResponse<WifiConfiguration>> GetWifiSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await SendHttpCommandAsync<WifiConfiguration>("/wifi", HttpMethod.Get, null, cancellationToken);
    }
    
    public async Task<ApiResponse<CodedResponse>> SetWifiSettingsAsync(WifiSettings settings, CancellationToken cancellationToken = default)
    {
        return await SendHttpCommandAsync<CodedResponse>("/wifi", HttpMethod.Post, settings, cancellationToken);
    }
    
    public async Task<ApiResponse<CodedResponse>> RestartWifiAsync(CancellationToken cancellationToken = default)
    {
        return await SendHttpCommandAsync<CodedResponse>("/wifi/restart", HttpMethod.Post, null, cancellationToken);
    }
    
    public async Task<ApiResponse<CodedResponse>> SleepAsync(CancellationToken cancellationToken = default)
    {
        return await SendHttpCommandAsync<CodedResponse>("/sleep", HttpMethod.Post, null, cancellationToken);
    }
    
    public async Task<ApiResponse<ScanStatus>> StartScanAsync(ScanTask task, CancellationToken cancellationToken = default)
    {
        return await SendHttpCommandAsync<ScanStatus>("/scan", HttpMethod.Post, new { scan = (int)task }, cancellationToken);
    }
    
    public async Task<ApiResponse<ScanStatus>> GetScanStatusAsync(CancellationToken cancellationToken = default)
    {
        return await SendHttpCommandAsync<ScanStatus>("/scan", HttpMethod.Get, null, cancellationToken);
    }
    
    public async Task<ApiResponse<LiveReadingData>> GetLiveReadingAsync(int startIndex, int numPoints, CancellationToken cancellationToken = default)
    {
        return await SendHttpCommandAsync<LiveReadingData>($"/live?startIndex={startIndex}&numPoints={numPoints}", HttpMethod.Get, null, cancellationToken);
    }
    
    public async Task<ApiResponse<MeasurementData>> GetMeasurementAsync(CancellationToken cancellationToken = default)
    {
        return await SendHttpCommandAsync<MeasurementData>("/measurement", HttpMethod.Get, null, cancellationToken);
    }
    
    private async Task<ApiResponse<T>> SendHttpCommandAsync<T>(string endpoint, HttpMethod method, object? payload, CancellationToken cancellationToken)
    {
        if (!_transport.IsConnected)
        {
            return ApiResponse<T>.Failure("Not connected to UAS-WAND device", "NOT_CONNECTED");
        }
        
        try
        {
            // Create HTTP-like request structure
            var request = new
            {
                endpoint = endpoint,
                method = method.Method,
                payload = payload != null ? SafeSerialize(payload) : null
            };
            
            var requestJson = SafeSerialize(request);
            if (requestJson == null)
            {
                return ApiResponse<T>.Failure("Failed to serialize request", "SERIALIZATION_ERROR");
            }
            
            _logger.LogDebug("Sending UAS-WAND command: {Method} {Endpoint}", method.Method, endpoint);
            _logger.LogDebug("Request JSON: {RequestJson}", requestJson);
            
            var responseJson = await _transport.SendCommandAsync(requestJson, cancellationToken);
            _logger.LogDebug("Response JSON: {ResponseJson}", responseJson);
            
            var response = SafeDeserialize<ApiResponse<T>>(responseJson);
            if (response != null)
            {
                _logger.LogDebug("UAS-WAND command completed: {Method} {Endpoint} -> {Success}", 
                    method.Method, endpoint, response.IsSuccess);
                return response;
            }
            
            return ApiResponse<T>.Failure("Failed to deserialize response", "DESERIALIZATION_ERROR");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "UAS-WAND command cancelled: {Method} {Endpoint}", method.Method, endpoint);
            return ApiResponse<T>.Failure("Operation cancelled", "CANCELLED");
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "UAS-WAND command timeout: {Method} {Endpoint}", method.Method, endpoint);
            return ApiResponse<T>.Failure("Operation timed out", "TIMEOUT");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "UAS-WAND command failed: {Method} {Endpoint}", method.Method, endpoint);
            return ApiResponse<T>.Failure(ex.Message, "TRANSPORT_ERROR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in UAS-WAND command: {Method} {Endpoint}", method.Method, endpoint);
            return ApiResponse<T>.Failure("Unexpected error occurred", "UNEXPECTED_ERROR");
        }
    }
    
    private string? SafeSerialize(object obj)
    {
        try
        {
            return JsonSerializer.Serialize(obj, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JSON serialization error");
            return null;
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
}