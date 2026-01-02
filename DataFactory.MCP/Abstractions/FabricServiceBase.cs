using DataFactory.MCP.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataFactory.MCP.Abstractions;

/// <summary>
/// Abstract base class for Microsoft Fabric API services providing common functionality.
/// Uses IHttpClientFactory for proper HttpClient lifecycle management.
/// Authentication is handled automatically by FabricAuthenticationHandler in the HTTP pipeline.
/// </summary>
public abstract class FabricServiceBase
{
    protected readonly HttpClient HttpClient;
    protected readonly ILogger Logger;
    protected readonly IValidationService ValidationService;
    protected static JsonSerializerOptions JsonOptions => JsonSerializerOptionsProvider.FabricApi;

    protected FabricServiceBase(
        IHttpClientFactory httpClientFactory,
        ILogger logger,
        IValidationService validationService)
    {
        HttpClient = httpClientFactory.CreateClient(HttpClientNames.FabricApi);
        Logger = logger;
        ValidationService = validationService;
    }

    /// <summary>
    /// Validates GUIDs before making API calls
    /// </summary>
    protected void ValidateGuids(params (string value, string name)[] guids)
    {
        foreach (var (value, name) in guids)
        {
            ValidationService.ValidateGuid(value, name);
        }
    }

    protected async Task<T?> GetAsync<T>(string endpoint, string? continuationToken = null) where T : class
    {
        var url = FabricUrlBuilder.ForFabricApi()
            .WithLiteralPath(endpoint)
            .WithContinuationToken(continuationToken)
            .Build();

        Logger.LogInformation("Fetching from: {Url}", url);

        var response = await HttpClient.GetAsync(url);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(content, JsonOptions);
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Logger.LogError("API request failed. Status: {StatusCode}, Content: {Content}",
                response.StatusCode, errorContent);

            throw new HttpRequestException($"API request failed: {response.StatusCode} - {errorContent}");
        }
    }

    protected async Task<T?> PostAsync<T>(string endpoint, object request) where T : class
    {
        var url = FabricUrlBuilder.ForFabricApi()
            .WithLiteralPath(endpoint)
            .Build();
        Logger.LogInformation("Posting to: {Url}", url);

        // Serialize object to JSON content
        var jsonContent = JsonSerializer.Serialize(request, JsonOptions);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await HttpClient.PostAsync(url, content);

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(responseContent, JsonOptions);
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Logger.LogError("API POST request failed. Status: {StatusCode}, Content: {Content}",
                response.StatusCode, errorContent);

            throw new HttpRequestException($"API POST request failed: {response.StatusCode} - {errorContent}");
        }
    }

    /// <summary>
    /// Posts a request and returns the response as a byte array (for binary responses like Arrow format)
    /// </summary>
    protected async Task<byte[]> PostAsBytesAsync(string endpoint, object request)
    {
        var url = FabricUrlBuilder.ForFabricApi()
            .WithLiteralPath(endpoint)
            .Build();
        Logger.LogInformation("Posting to: {Url}", url);

        var jsonContent = JsonSerializer.Serialize(request, JsonOptions);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await HttpClient.PostAsync(url, content);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsByteArrayAsync();
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Logger.LogError("API POST request failed. Status: {StatusCode}, Content: {Content}",
                response.StatusCode, errorContent);

            throw new HttpRequestException($"API POST request failed: {response.StatusCode} - {errorContent}");
        }
    }

    /// <summary>
    /// Posts a request expecting no content response (204). Returns true on success, false on failure.
    /// </summary>
    protected async Task<bool> PostNoContentAsync(string endpoint, object request)
    {
        var url = FabricUrlBuilder.ForFabricApi()
            .WithLiteralPath(endpoint)
            .Build();
        Logger.LogInformation("Posting to: {Url}", url);

        var jsonContent = JsonSerializer.Serialize(request, JsonOptions);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await HttpClient.PostAsync(url, content);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Logger.LogError("API POST request failed. Status: {StatusCode}, Content: {Content}",
                response.StatusCode, errorContent);

            return false;
        }
    }
}
