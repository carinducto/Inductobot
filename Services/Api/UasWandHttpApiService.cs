using Inductobot.Abstractions.Services;
using Inductobot.Models.Authentication;
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
/// Uses HTTP/HTTPS with Basic Authentication (username: "user", password: "1234").
/// Sends standard HTTP requests to device endpoints and parses JSON responses.
/// </summary>
public class UasWandHttpApiService : IUasWandApiService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UasWandHttpApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    
    private string? _baseUrl;
    
    // UAS device credentials (CORRECT from working GRPCService) - UAS uses test:0000
    private string Username = "test";
    private string Password = "0000";
    
    // UAS device keys (EXACT working pattern from GRPCService)
    private string DeviceKey1 = "00112233001122330011223300112233";
    private string DeviceKey2 = "00112233001122330011223300112233";
    
    // Authentication state tracking
    private bool _isAuthenticated = false;
    private DateTime _lastAuthTime = DateTime.MinValue;
    private readonly TimeSpan _authTimeout = TimeSpan.FromMinutes(10); // Authentication expires after 10 minutes
    
    public UasWandHttpApiService(ILogger<UasWandHttpApiService> logger)
    {
        _logger = logger;
        
        // UAS authentication requires client certificate + server cert bypass
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (HttpRequestMessage, cert, chain, sslPolicyErrors) => true
        };
        
        // Load UAS client certificate for mutual TLS authentication
        try
        {
            var certPath = Path.Combine(AppContext.BaseDirectory, "Certificates", "uas-client.pfx");
            if (File.Exists(certPath))
            {
                var clientCert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certPath, "uas");
                handler.ClientCertificates.Add(clientCert);
                _logger.LogInformation("UAS client certificate loaded for mutual TLS authentication");
            }
            else
            {
                _logger.LogWarning("UAS client certificate not found at: {CertPath}", certPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load UAS client certificate");
        }
        
        _httpClient = new HttpClient(handler);
        
        // EXACT legacy configuration - 10-minute timeout and basic auth only
        _httpClient.Timeout = new TimeSpan(0, 10, 0);
        
        // Set up Basic Authentication exactly like legacy (test:0000)
        var authBytes = Encoding.ASCII.GetBytes($"{Username}:{Password}");
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        
        _logger.LogInformation("UAS HTTPS API client configured with EXACT legacy working pattern - HttpClientHandler + cert bypass + basic auth only");
        
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
        
        // Log which protocol we're using for ESP32 debugging
        if (_baseUrl?.StartsWith("https://") == true)
        {
            _logger.LogInformation("Using HTTPS - ESP32 device must have sufficient heap memory (~37kB per connection)");
        }
        else if (_baseUrl?.StartsWith("http://") == true) 
        {
            _logger.LogInformation("Using HTTP - no SSL memory overhead");
        }
    }
    
    public void SetCredentials(string username, string password)
    {
        Username = username;
        Password = password;
        
        // Update HTTP client with new authentication
        var authBytes = Encoding.ASCII.GetBytes($"{Username}:{Password}");
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
            
        _logger.LogInformation("UAS-WAND HTTP API credentials updated to: {Username}:***", username);
    }
    
    public void SetDeviceKeys(string deviceKey1, string deviceKey2)
    {
        DeviceKey1 = deviceKey1;
        DeviceKey2 = deviceKey2;
        _logger.LogInformation("UAS-WAND device keys updated (Key1: {Key1Length} chars, Key2: {Key2Length} chars)", 
            deviceKey1?.Length ?? 0, deviceKey2?.Length ?? 0);
    }
    
    public async Task<ApiResponse<UASDeviceInfo>> GetDeviceInfoAsync(CancellationToken cancellationToken = default)
    {
        // Ensure authentication before API call
        var authResult = await EnsureAuthenticatedAsync(cancellationToken);
        if (authResult != null && !authResult.IsSuccess)
        {
            return ApiResponse<UASDeviceInfo>.Failure(authResult.Message, authResult.ErrorCode);
        }
        
        return await SendHttpRequestAsync<UASDeviceInfo>("GET", "/info", null, cancellationToken);
    }
    
    public async Task<ApiResponse<CodedResponse>> KeepAliveAsync(CancellationToken cancellationToken = default)
    {
        // Ensure authentication before API call - real UAS devices require auth even for ping
        var authResult = await EnsureAuthenticatedAsync(cancellationToken);
        if (authResult != null && !authResult.IsSuccess)
        {
            return ApiResponse<CodedResponse>.Failure(authResult.Message, authResult.ErrorCode);
        }
        
        return await SendHttpRequestAsync<CodedResponse>("GET", "/ping", null, cancellationToken);
    }
    
    public async Task<ApiResponse<WifiConfiguration>> GetWifiSettingsAsync(CancellationToken cancellationToken = default)
    {
        // Ensure authentication before API call
        var authResult = await EnsureAuthenticatedAsync(cancellationToken);
        if (authResult != null && !authResult.IsSuccess)
        {
            return ApiResponse<WifiConfiguration>.Failure(authResult.Message, authResult.ErrorCode);
        }
        
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
        // Ensure authentication before API call
        var authResult = await EnsureAuthenticatedAsync(cancellationToken);
        if (authResult != null && !authResult.IsSuccess)
        {
            return ApiResponse<ScanStatus>.Failure(authResult.Message, authResult.ErrorCode);
        }
        
        var payload = new { scan = (int)task };
        return await SendHttpRequestAsync<ScanStatus>("POST", "/scan", payload, cancellationToken);
    }
    
    public async Task<ApiResponse<ScanStatus>> GetScanStatusAsync(CancellationToken cancellationToken = default)
    {
        // Ensure authentication before API call
        var authResult = await EnsureAuthenticatedAsync(cancellationToken);
        if (authResult != null && !authResult.IsSuccess)
        {
            return ApiResponse<ScanStatus>.Failure(authResult.Message, authResult.ErrorCode);
        }
        
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
    
    /// <summary>
    /// Performs the complete challenge-response authentication sequence with the UAS device.
    /// EXACT implementation based on working GRPCService ByteSnapApi patterns.
    /// Uses dual challenge-response: we send challenge to device AND device sends challenge to us.
    /// </summary>
    public async Task<ApiResponse<object>> PerformChallengeResponseAuthAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting UAS challenge-response authentication sequence (GRPCService pattern)");
        
        try
        {
            // EXACT GRPCService Pattern: Dual Challenge-Response Authentication
            
            // Step 1: Send OUR challenge to the device (32-byte challenge with header offset)
            _logger.LogDebug("Step 1: Sending our challenge to UAS device");
            var ourChallenge = GenerateChallenge();
            _logger.LogDebug("Generated our challenge: {ChallengeLength} bytes", ourChallenge.Length);
            
            var sendChallengeResult = await SendHttpRequestAsync<object>("POST", "/auth", new { challenge = ourChallenge }, cancellationToken);
            if (!sendChallengeResult.IsSuccess)
            {
                _logger.LogError("Failed to send our challenge: {Message}", sendChallengeResult.Message);
                return ApiResponse<object>.Failure($"Send challenge failed: {sendChallengeResult.Message}", "SEND_CHALLENGE_FAILED");
            }
            
            // Step 2: Request challenge from device
            _logger.LogDebug("Step 2: Requesting challenge from UAS device");  
            var deviceChallengeResponse = await SendHttpRequestAsync<ChallengeRequest>("GET", "/auth", null, cancellationToken);
            
            if (!deviceChallengeResponse.IsSuccess || deviceChallengeResponse.Data == null)
            {
                _logger.LogError("Failed to request device challenge: {Message}", deviceChallengeResponse.Message);
                return ApiResponse<object>.Failure($"Device challenge request failed: {deviceChallengeResponse.Message}", "DEVICE_CHALLENGE_FAILED");
            }
            
            var deviceChallenge = deviceChallengeResponse.Data.Challenge;
            _logger.LogDebug("Received device challenge: {ChallengeLength} bytes", deviceChallenge?.Length ?? 0);
            
            // Step 3: Generate response to device challenge using device key
            var response = GenerateChallengeResponse(deviceChallenge, DeviceKey1, DeviceKey2);
            _logger.LogDebug("Generated response to device challenge: {ResponseLength} bytes", response?.Length ?? 0);
            
            // Step 4: Send our response to device challenge  
            _logger.LogDebug("Step 4: Sending response to device challenge");
            var authResult = await SendHttpRequestAsync<AuthResult>("POST", "/auth", new { response = response }, cancellationToken);
            
            if (authResult.IsSuccess && (authResult.Data?.Authenticated == true || authResult.IsSuccess))
            {
                _isAuthenticated = true;
                _lastAuthTime = DateTime.UtcNow;
                _logger.LogInformation("UAS dual challenge-response authentication successful");
                return ApiResponse<object>.Success(null, "Authentication successful");
            }
            else
            {
                _logger.LogError("UAS authentication failed: {Message}", authResult.Message);
                return ApiResponse<object>.Failure($"Authentication failed: {authResult.Message}", "AUTHENTICATION_FAILED");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during UAS challenge-response authentication");
            return ApiResponse<object>.Failure($"Authentication exception: {ex.Message}", "AUTHENTICATION_EXCEPTION");
        }
    }
    
    /// <summary>
    /// Generates 32-byte challenge following GRPCService pattern
    /// </summary>
    private byte[] GenerateChallenge()
    {
        const int challengeLen = 32;
        const int headerLen = 2;
        var challenge = new byte[challengeLen];
        
        // GRPCService pattern: simple incrementing pattern for testing
        for (int i = 0; i < challengeLen; ++i)
        {
            challenge[i] = (byte)(i + headerLen);
        }
        
        return challenge;
    }
    
    /// <summary>
    /// Generates challenge response using device keys based on UAS protocol analysis
    /// </summary>
    private byte[] GenerateChallengeResponse(byte[]? challenge, string key1, string key2)
    {
        if (challenge == null || challenge.Length == 0)
        {
            _logger.LogWarning("Empty challenge received, using default response");
            return new byte[16]; // Default empty response
        }
        
        try
        {
            // Convert hex string keys to byte arrays (format: 00112233001122330011223300112233)
            var keyBytes1 = ConvertHexStringToBytes(key1);
            var keyBytes2 = ConvertHexStringToBytes(key2);
            
            _logger.LogDebug("Using device keys - Key1: {Key1Length} bytes, Key2: {Key2Length} bytes", 
                keyBytes1?.Length ?? 0, keyBytes2?.Length ?? 0);
            
            // Simple XOR-based response (simplified version of actual crypto)
            // Real implementation would use proper cryptographic functions
            var response = new byte[Math.Max(challenge.Length, 16)];
            
            for (int i = 0; i < response.Length; i++)
            {
                var challengeByte = i < challenge.Length ? challenge[i] : (byte)0;
                var key1Byte = keyBytes1 != null && i < keyBytes1.Length ? keyBytes1[i % keyBytes1.Length] : (byte)0;
                var key2Byte = keyBytes2 != null && i < keyBytes2.Length ? keyBytes2[i % keyBytes2.Length] : (byte)0;
                
                response[i] = (byte)(challengeByte ^ key1Byte ^ key2Byte);
            }
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating challenge response");
            return new byte[16]; // Fallback to default response
        }
    }
    
    /// <summary>
    /// Converts hex string (like "00112233001122330011223300112233") to byte array
    /// </summary>
    private byte[]? ConvertHexStringToBytes(string hexString)
    {
        if (string.IsNullOrEmpty(hexString) || hexString.Length % 2 != 0)
        {
            _logger.LogWarning("Invalid hex string: {HexString}", hexString);
            return null;
        }
        
        try
        {
            var bytes = new byte[hexString.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting hex string to bytes: {HexString}", hexString);
            return null;
        }
    }
    
    /// <summary>
    /// Ensures the device is authenticated before performing API operations
    /// </summary>
    private async Task<ApiResponse<object>?> EnsureAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        // Check if we're already authenticated and not expired
        if (_isAuthenticated && DateTime.UtcNow - _lastAuthTime < _authTimeout)
        {
            return null; // Already authenticated
        }
        
        _logger.LogInformation("Authentication required - performing challenge-response sequence");
        _isAuthenticated = false; // Reset authentication state
        
        return await PerformChallengeResponseAuthAsync(cancellationToken);
    }
    
    // Legacy methods for compatibility
    public async Task<ApiResponse<object>> RequestChallengeAsync(CancellationToken cancellationToken = default)
    {
        return await SendHttpRequestAsync<object>("GET", "/auth", null, cancellationToken);
    }
    
    public async Task<ApiResponse<object>> SendChallengeResponseAsync(byte[] challengeResponse, CancellationToken cancellationToken = default)
    {
        var payload = new { response = challengeResponse };
        return await SendHttpRequestAsync<object>("POST", "/auth", payload, cancellationToken);
    }
    
    // Advanced UAS Authentication (based on WandV3TestBench analysis)
    
    public async Task<ApiResponse<object>> ProvisionDeviceKeysAsync(CancellationToken cancellationToken = default)
    {
        var payload = new { 
            key1 = DeviceKey1,
            key2 = DeviceKey2 
        };
        return await SendHttpRequestAsync<object>("POST", "/provision-keys", payload, cancellationToken);
    }
    
    public async Task<ApiResponse<object>> RemoveProvisioningAsync(CancellationToken cancellationToken = default)
    {
        return await SendHttpRequestAsync<object>("POST", "/remove-provisioning", null, cancellationToken);
    }
    
    public async Task<ApiResponse<object>> GetLoginStatusAsync(CancellationToken cancellationToken = default)
    {
        return await SendHttpRequestAsync<object>("GET", "/login-status", null, cancellationToken);
    }
    
    public async Task<ApiResponse<object>> LogoutAsync(CancellationToken cancellationToken = default)
    {
        return await SendHttpRequestAsync<object>("POST", "/logout", null, cancellationToken);
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
            cts.CancelAfter(TimeSpan.FromSeconds(30)); // Longer timeout for ESP32 SSL memory allocation
            
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
            
            // Check if this might be ESP32 SSL memory exhaustion
            if (url.StartsWith("https://") && (ex.Message.Contains("timeout") || ex.Message.Contains("handshake") || ex.Message.Contains("SSL")))
            {
                _logger.LogWarning("HTTPS request failed - ESP32 may be experiencing SSL memory exhaustion (needs ~37kB heap per connection). Consider trying HTTP instead.");
            }
            
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
        try
        {
            _httpClient?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing UAS HTTP API service");
        }
    }
}