// --- Auth Manager ---
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Models;

public class CjAuthManager
{
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly CjAuthRequest _authRequest;  
    // Cache keys
    private const string AccessTokenKey = "API@CJ5607474@CJ:eyJhbGciOiJIUzI1NiJ9.eyJqdGkiOiI0MjU1MiIsInR5cGUiOiJBQ0NFU1NfVE9LRU4iLCJzdWIiOiJicUxvYnFRMGxtTm55UXB4UFdMWnlqSnUrckYyc2FpOEVtL3VvdXlSSWNXaDUwQjF1eGZ5dzB4TjVWYlJJeXczN1pQUEJ6Y1RYWFhEZzNyOHdqYWxvdzRXNHdkcXIyWTFNK0dUcDFjWUVqRnU4VTU4Tkw4dXRCaEZSd25iM1pqRkxPTHhvUGtwVVhIZnR1cWNkTEdrS2p6T1NFMVFBVFhqVXNFOVNISExHenFPbFJYeGZGTzhiVUJMalVheUdQMEcxaS94VXFydFovTmhvUkkzTnZuTWdsQW5pMFVySEZ5Uno0TG1UVVR1UncrUU5GZE1HWHNVY1BlTFV6RkJxS0ZNeFFVekxkMkdKT1BGOEE1N3Y4NFprY3ZLUTlBUVlKTEJyZlMwMnR6a3EyUT0iLCJpYXQiOjE3ODc1NDMxMTJ9.68j-JWflOvm1RGLHcg7KuRn8HNpJLF0Rh3ZKZ7P_D-A";
    private const string RefreshTokenKey = "Cj_Refresh_Token";

    public CjAuthManager(HttpClient httpClient, IDistributedCache cache, IOptions<CjAuthRequest> options)
    {
        _httpClient = httpClient;
        _cache = cache;
        var authRequest = new CjAuthRequest
        {
            ApiKey = options.Value.ApiKey,
            BaseUrl = options.Value.BaseUrl
        };
        _authRequest = authRequest;
    }

    public async Task<string> GetValidAccessTokenAsync()
    {
        // 1. Try to get valid access token from cache
        var accessToken = await _cache.GetStringAsync(AccessTokenKey);
        if (!string.IsNullOrEmpty(accessToken))
            return accessToken;

        // 2. If access token is missing/expired, try to use refresh token
        var refreshToken = await _cache.GetStringAsync(RefreshTokenKey);
        if (!string.IsNullOrEmpty(refreshToken))
        {
            var refreshed = await RefreshTokenAsync(refreshToken);
#pragma warning disable CS8603 // Possible null reference return.
            if (refreshed) return await _cache.GetStringAsync(AccessTokenKey);
#pragma warning restore CS8603 // Possible null reference return.
        }

        // 3. If neither exists, perform full login with API Key
        await LoginWithApiKeyAsync();
        return await _cache.GetStringAsync(AccessTokenKey) 
               ?? throw new Exception("Failed to retrieve CJ Access Token.");
    }

    private async Task LoginWithApiKeyAsync()
    {
        var requestBody = new { apiKey = _authRequest.ApiKey };
        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var url = $"{_authRequest.BaseUrl.TrimEnd('/')}/v1/authentication/getAccessToken";
        var response = await _httpClient.PostAsync(url, content);
        await ProcessAuthResponseAsync(response);
    }

    private async Task<bool> RefreshTokenAsync(string refreshToken)
    {
        var requestBody = new { refreshToken = refreshToken };
        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var url = $"{_authRequest.BaseUrl.TrimEnd('/')}/v1/authentication/refreshAccessToken";
        var response = await _httpClient.PostAsync(url, content);
        return await ProcessAuthResponseAsync(response);
    }

    private async Task<bool> ProcessAuthResponseAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) return false;

        var json = await response.Content.ReadAsStringAsync();
        var authData = JsonSerializer.Deserialize<CjAuthResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (authData?.Result == true && authData.Data != null)
        {
            // Cache Access Token (CJ sets expiry to 15 days, we cache for 14 days to be safe)
            await _cache.SetStringAsync(AccessTokenKey, authData.Data.AccessToken, 
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });

            // Cache Refresh Token (CJ sets expiry to 180 days, we cache for 170 days)
            await _cache.SetStringAsync(RefreshTokenKey, authData.Data.RefreshToken, 
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(170) });
            
            return true;
        }
        return false;
    }
}