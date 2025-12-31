using DataFactory.MCP.Abstractions;
using DataFactory.MCP.Abstractions.Interfaces;
using DataFactory.MCP.Models.Dataflow;
using DataFactory.MCP.Models.Dataflow.Query;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataFactory.MCP.Services;

/// <summary>
/// Internal class for HTTP response deserialization
/// </summary>
internal class GetDataflowDefinitionHttpResponse
{
    [JsonPropertyName("definition")]
    public DataflowDefinition Definition { get; set; } = new();
}

/// <summary>
/// Service for interacting with Microsoft Fabric Dataflows API
/// </summary>
public class FabricDataflowService : FabricServiceBase, IFabricDataflowService
{
    private readonly IValidationService _validationService;
    private readonly IArrowDataReaderService _arrowDataReaderService;
    private readonly IPowerBICloudDatasourceV2Service _cloudDatasourceService;

    public FabricDataflowService(
        ILogger<FabricDataflowService> logger,
        IAuthenticationService authService,
        IValidationService validationService,
        IArrowDataReaderService arrowDataReaderService,
        IPowerBICloudDatasourceV2Service cloudDatasourceService)
        : base(logger, authService)
    {
        _validationService = validationService;
        _arrowDataReaderService = arrowDataReaderService;
        _cloudDatasourceService = cloudDatasourceService;
    }

    public async Task<ListDataflowsResponse> ListDataflowsAsync(
        string workspaceId,
        string? continuationToken = null)
    {
        try
        {
            _validationService.ValidateGuid(workspaceId, nameof(workspaceId));

            await EnsureAuthenticationAsync();

            var url = BuildDataflowsUrl(workspaceId, continuationToken);

            Logger.LogInformation("Fetching dataflows from workspace {WorkspaceId}: {Url}", workspaceId, url);

            var response = await HttpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var dataflowsResponse = System.Text.Json.JsonSerializer.Deserialize<ListDataflowsResponse>(content, JsonOptions);

                Logger.LogInformation("Successfully retrieved {Count} dataflows from workspace {WorkspaceId}",
                    dataflowsResponse?.Value?.Count ?? 0, workspaceId);
                return dataflowsResponse ?? new ListDataflowsResponse();
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Logger.LogError("API request failed. Status: {StatusCode}, Content: {Content}",
                    response.StatusCode, errorContent);

                throw new HttpRequestException($"API request failed: {response.StatusCode} - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error fetching dataflows from workspace {WorkspaceId}", workspaceId);
            throw;
        }
    }

    public async Task<CreateDataflowResponse> CreateDataflowAsync(
        string workspaceId,
        CreateDataflowRequest request)
    {
        try
        {
            _validationService.ValidateGuid(workspaceId, nameof(workspaceId));
            _validationService.ValidateAndThrow(request, nameof(request));

            await EnsureAuthenticationAsync();

            var url = $"{BaseUrl}/workspaces/{workspaceId}/dataflows";
            var jsonContent = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            Logger.LogInformation("Creating dataflow '{DisplayName}' in workspace {WorkspaceId}: {Url}",
                request.DisplayName, workspaceId, url);

            var response = await HttpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var createResponse = JsonSerializer.Deserialize<CreateDataflowResponse>(responseContent, JsonOptions);

                Logger.LogInformation("Successfully created dataflow '{DisplayName}' with ID {DataflowId} in workspace {WorkspaceId}",
                    request.DisplayName, createResponse?.Id, workspaceId);

                return createResponse ?? new CreateDataflowResponse();
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Logger.LogError("Failed to create dataflow. Status: {StatusCode}, Content: {Content}",
                    response.StatusCode, errorContent);

                throw new HttpRequestException($"Failed to create dataflow: {response.StatusCode} - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error creating dataflow '{DisplayName}' in workspace {WorkspaceId}",
                request?.DisplayName, workspaceId);
            throw;
        }
    }

    private static string BuildDataflowsUrl(string workspaceId, string? continuationToken)
    {
        var url = new StringBuilder($"{BaseUrl}/workspaces/{workspaceId}/dataflows");
        var queryParams = new List<string>();

        if (!string.IsNullOrEmpty(continuationToken))
        {
            queryParams.Add($"continuationToken={Uri.EscapeDataString(continuationToken)}");
        }

        if (queryParams.Any())
        {
            url.Append("?");
            url.Append(string.Join("&", queryParams));
        }

        return url.ToString();
    }

    public async Task<ExecuteDataflowQueryResponse> ExecuteQueryAsync(
        string workspaceId,
        string dataflowId,
        ExecuteDataflowQueryRequest request)
    {
        try
        {
            _validationService.ValidateGuid(workspaceId, nameof(workspaceId));
            _validationService.ValidateGuid(dataflowId, nameof(dataflowId));
            _validationService.ValidateAndThrow(request, nameof(request));

            await EnsureAuthenticationAsync();

            var url = $"{BaseUrl}/workspaces/{workspaceId}/dataflows/{dataflowId}/executeQuery";
            var jsonContent = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            Logger.LogInformation("Executing query '{QueryName}' on dataflow {DataflowId} in workspace {WorkspaceId}: {Url}",
                request.QueryName, dataflowId, workspaceId, url);

            var response = await HttpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                // Read the binary response data
                var responseData = await response.Content.ReadAsByteArrayAsync();
                var contentType = response.Content.Headers.ContentType?.MediaType;
                var contentLength = responseData.Length;

                Logger.LogInformation("Successfully executed query '{QueryName}' on dataflow {DataflowId}. Response: {ContentLength} bytes, Content-Type: {ContentType}",
                    request.QueryName, dataflowId, contentLength, contentType);

                // Extract data from Arrow stream directly to final format
                var summary = await _arrowDataReaderService.ReadArrowStreamAsync(responseData);

                return new ExecuteDataflowQueryResponse
                {
                    Data = responseData,
                    ContentType = contentType,
                    ContentLength = contentLength,
                    Success = true,
                    Summary = summary,
                    Metadata = new Dictionary<string, object>
                    {
                        { "executedAt", DateTime.UtcNow },
                        { "workspaceId", workspaceId },
                        { "dataflowId", dataflowId },
                        { "queryName", request.QueryName }
                    }
                };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Logger.LogError("Failed to execute query '{QueryName}' on dataflow {DataflowId}. Status: {StatusCode}, Content: {Content}",
                    request.QueryName, dataflowId, response.StatusCode, errorContent);

                return new ExecuteDataflowQueryResponse
                {
                    Success = false,
                    Error = $"Query execution failed: {response.StatusCode} - {errorContent}",
                    ContentLength = 0
                };
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error executing query '{QueryName}' on dataflow {DataflowId} in workspace {WorkspaceId}",
                request?.QueryName, dataflowId, workspaceId);

            return new ExecuteDataflowQueryResponse
            {
                Success = false,
                Error = $"Query execution error: {ex.Message}",
                ContentLength = 0
            };
        }
    }

    public async Task<DecodedDataflowDefinition> GetDecodedDataflowDefinitionAsync(
        string workspaceId,
        string dataflowId)
    {
        try
        {
            _validationService.ValidateGuid(workspaceId, nameof(workspaceId));
            _validationService.ValidateGuid(dataflowId, nameof(dataflowId));

            var definitionResponse = await GetDataflowDefinitionResponseAsync(workspaceId, dataflowId);

            var decoded = new DecodedDataflowDefinition
            {
                RawParts = definitionResponse?.Definition?.Parts ?? new List<DataflowDefinitionPart>()
            }; foreach (var part in decoded.RawParts)
            {
                if (string.IsNullOrEmpty(part.Payload)) continue;

                try
                {
                    var decodedBytes = Convert.FromBase64String(part.Payload);
                    var decodedText = Encoding.UTF8.GetString(decodedBytes);

                    switch (part.Path.ToLowerInvariant())
                    {
                        case "querymetadata.json":
                            decoded.QueryMetadata = ParseJsonContent(decodedText);
                            break;
                        case "mashup.pq":
                            decoded.MashupQuery = decodedText;
                            break;
                        case ".platform":
                            decoded.PlatformMetadata = ParseJsonContent(decodedText);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to decode part {PartPath} for dataflow {DataflowId}",
                        part.Path, dataflowId);
                }
            }

            Logger.LogInformation("Successfully decoded definition for dataflow {DataflowId}", dataflowId);
            return decoded;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error decoding definition for dataflow {DataflowId} in workspace {WorkspaceId}",
                dataflowId, workspaceId);
            throw;
        }
    }

    private static JsonElement? ParseJsonContent(string jsonContent)
    {
        try
        {
            // Parse JSON content into JsonElement for structured access
            using var document = JsonDocument.Parse(jsonContent);
            return document.RootElement.Clone();
        }
        catch
        {
            // If parsing fails, return null
            return null;
        }
    }

    private async Task<GetDataflowDefinitionHttpResponse> GetDataflowDefinitionResponseAsync(string workspaceId, string dataflowId)
    {
        await EnsureAuthenticationAsync();

        var url = $"{BaseUrl}/workspaces/{workspaceId}/items/{dataflowId}/getDefinition";
        var content = new StringContent("{}", Encoding.UTF8, "application/json");

        Logger.LogInformation("Getting definition for dataflow {DataflowId} in workspace {WorkspaceId}: {Url}",
            dataflowId, workspaceId, url);

        var response = await HttpClient.PostAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Logger.LogError("Failed to get dataflow definition. Status: {StatusCode}, Content: {Content}",
                response.StatusCode, errorContent);
            throw new HttpRequestException($"Failed to get dataflow definition: {response.StatusCode} - {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<GetDataflowDefinitionHttpResponse>(responseContent, JsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize dataflow definition response");
    }

    public async Task<UpdateDataflowDefinitionResponse> AddConnectionToDataflowAsync(
        string workspaceId,
        string dataflowId,
        string connectionId)
    {
        try
        {
            _validationService.ValidateGuid(workspaceId, nameof(workspaceId));
            _validationService.ValidateGuid(dataflowId, nameof(dataflowId));
            _validationService.ValidateGuid(connectionId, nameof(connectionId));

            await EnsureAuthenticationAsync();

            Logger.LogInformation("Adding connection {ConnectionId} to dataflow {DataflowId} in workspace {WorkspaceId}",
                connectionId, dataflowId, workspaceId);

            // Step 1: Get current dataflow definition
            var currentDefinition = await GetDataflowDefinitionResponseAsync(workspaceId, dataflowId);
            if (currentDefinition.Definition?.Parts == null)
            {
                return new UpdateDataflowDefinitionResponse
                {
                    Success = false,
                    ErrorMessage = "Failed to retrieve current dataflow definition",
                    DataflowId = dataflowId,
                    WorkspaceId = workspaceId
                };
            }

            // Step 2: Get connection details
            var connectionDetails = await GetConnectionDetailsAsync(connectionId);
            if (connectionDetails == null)
            {
                return new UpdateDataflowDefinitionResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to retrieve connection details for connection ID: {connectionId}",
                    DataflowId = dataflowId,
                    WorkspaceId = workspaceId
                };
            }

            Logger.LogInformation("Retrieved connection details successfully for connection: {ConnectionId}", connectionId);

            // Step 3: Get the ClusterId for this connection from the Power BI v2.0 API
            // This is required for proper credential binding in the dataflow
            string? clusterId = null;
            try
            {
                clusterId = await _cloudDatasourceService.GetClusterIdForConnectionAsync(connectionId);
                if (clusterId != null)
                {
                    Logger.LogInformation("Successfully retrieved ClusterId {ClusterId} for connection {ConnectionId}", 
                        clusterId, connectionId);
                }
                else
                {
                    Logger.LogWarning("Could not find ClusterId for connection {ConnectionId}. " +
                        "Credential binding may not work correctly.", connectionId);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to get ClusterId for connection {ConnectionId}. " +
                    "Continuing without ClusterId - credential binding may not work correctly.", connectionId);
            }

            // Step 4: Update queryMetadata.json to include the new connection
            var updatedDefinition = await AddConnectionToDefinitionAsync(
                currentDefinition.Definition,
                connectionDetails.Value,
                connectionId,
                clusterId);

            // Step 5: Update the dataflow definition via API
            var updateResult = await UpdateDataflowDefinitionAsync(workspaceId, dataflowId, updatedDefinition);

            Logger.LogInformation("Successfully added connection {ConnectionId} to dataflow {DataflowId}",
                connectionId, dataflowId);

            return new UpdateDataflowDefinitionResponse
            {
                Success = true,
                DataflowId = dataflowId,
                WorkspaceId = workspaceId
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error adding connection {ConnectionId} to dataflow {DataflowId} in workspace {WorkspaceId}",
                connectionId, dataflowId, workspaceId);

            return new UpdateDataflowDefinitionResponse
            {
                Success = false,
                ErrorMessage = ex.Message,
                DataflowId = dataflowId,
                WorkspaceId = workspaceId
            };
        }
    }

    private async Task<JsonElement?> GetConnectionDetailsAsync(string connectionId)
    {
        try
        {
            var url = $"{BaseUrl}/connections/{connectionId}";
            Logger.LogInformation("Getting connection details for {ConnectionId}: {Url}", connectionId, url);

            var response = await HttpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning("Failed to get connection details. Status: {StatusCode}", response.StatusCode);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseContent);
            return document.RootElement.Clone();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting connection details for {ConnectionId}", connectionId);
            return null;
        }
    }

    private async Task<DataflowDefinition> AddConnectionToDefinitionAsync(
        DataflowDefinition definition,
        JsonElement connectionDetails,
        string connectionId,
        string? clusterId)
    {
        // Find and update the queryMetadata.json part
        var queryMetadataPart = definition.Parts?.FirstOrDefault(p =>
            p.Path?.Equals("querymetadata.json", StringComparison.OrdinalIgnoreCase) == true);

        if (queryMetadataPart?.Payload != null)
        {
            // Decode current queryMetadata
            var decodedBytes = Convert.FromBase64String(queryMetadataPart.Payload);
            var currentMetadataJson = Encoding.UTF8.GetString(decodedBytes);

            using var document = JsonDocument.Parse(currentMetadataJson);
            var metadata = document.RootElement;

            // Create updated metadata with new connection (including ClusterId if available)
            var updatedMetadata = CreateUpdatedQueryMetadata(metadata, connectionDetails, connectionId, clusterId);

            // Encode updated metadata back to Base64
            var updatedMetadataJson = JsonSerializer.Serialize(updatedMetadata, new JsonSerializerOptions { WriteIndented = true });
            var updatedBytes = Encoding.UTF8.GetBytes(updatedMetadataJson);
            queryMetadataPart.Payload = Convert.ToBase64String(updatedBytes);
        }

        return definition;
    }

    private Dictionary<string, object> CreateUpdatedQueryMetadata(
        JsonElement currentMetadata,
        JsonElement connectionDetails,
        string connectionId,
        string? clusterId)
    {
        // Convert the entire JsonElement to a Dictionary for easier manipulation
        var metadataDict = JsonElementToDictionary(currentMetadata);

        // Handle connections array
        if (!metadataDict.ContainsKey("connections"))
        {
            metadataDict["connections"] = new List<object>();
        }

        var connectionsList = metadataDict["connections"] as List<object> ?? new List<object>();

        // Build the connectionId value for the metadata
        // If we have a ClusterId, use the format: {"ClusterId":"...","DatasourceId":"..."}
        // This format is required for proper credential binding in Fabric dataflows
        string connectionIdValue;
        if (!string.IsNullOrEmpty(clusterId))
        {
            // Use the ClusterId + DatasourceId format that Fabric UI uses
            // This enables proper credential binding for query execution
            var connectionIdObj = new Dictionary<string, string>
            {
                ["ClusterId"] = clusterId,
                ["DatasourceId"] = connectionId
            };
            connectionIdValue = JsonSerializer.Serialize(connectionIdObj);
        }
        else
        {
            // Fall back to plain connectionId if ClusterId is not available
            connectionIdValue = connectionId;
        }

        // Add new connection if it doesn't already exist
        var newConnection = new Dictionary<string, object>
        {
            ["connectionId"] = connectionIdValue,
            ["kind"] = GetConnectionKind(connectionDetails),
            ["path"] = GetConnectionPath(connectionDetails)
        };

        // Check if connection already exists
        bool connectionExists = connectionsList.Any(conn =>
        {
            if (conn is Dictionary<string, object> dict && dict.ContainsKey("connectionId"))
            {
                var existingConnectionId = dict["connectionId"]?.ToString();
                // Check if it matches the connectionId or any format containing it
                return existingConnectionId == connectionId ||
                       existingConnectionId == connectionIdValue ||
                       existingConnectionId?.Contains(connectionId) == true;
            }
            return false;
        });

        if (!connectionExists)
        {
            connectionsList.Add(newConnection);
        }

        metadataDict["connections"] = connectionsList;
        return metadataDict;
    }

    private Dictionary<string, object> JsonElementToDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, object>();

        foreach (var property in element.EnumerateObject())
        {
            dict[property.Name] = JsonElementToObject(property.Value);
        }

        return dict;
    }

    private object JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.TryGetInt32(out var intValue) ? intValue : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
            JsonValueKind.Object => JsonElementToDictionary(element),
            _ => element.GetRawText()
        };
    }

    private string GetConnectionKind(JsonElement connectionDetails)
    {
        try
        {
            if (connectionDetails.TryGetProperty("connectionDetails", out var connDetails) &&
                connDetails.TryGetProperty("type", out var typeProp))
            {
                return typeProp.GetString() ?? "Unknown";
            }
            return "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private string GetConnectionPath(JsonElement connectionDetails)
    {
        try
        {
            if (connectionDetails.TryGetProperty("connectionDetails", out var connDetails) &&
                connDetails.TryGetProperty("path", out var pathProp))
            {
                return pathProp.GetString() ?? "";
            }
            return "";
        }
        catch
        {
            return "";
        }
    }

    private async Task<bool> UpdateDataflowDefinitionAsync(string workspaceId, string dataflowId, DataflowDefinition definition)
    {
        try
        {
            var url = $"{BaseUrl}/workspaces/{workspaceId}/items/{dataflowId}/updateDefinition";
            var request = new UpdateDataflowDefinitionRequest { Definition = definition };
            var requestJson = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            Logger.LogInformation("Updating dataflow definition for {DataflowId}: {Url}", dataflowId, url);
            Logger.LogInformation("Request JSON (first 500 chars): {RequestJson}",
                requestJson.Length > 500 ? requestJson.Substring(0, 500) + "..." : requestJson);

            var response = await HttpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Logger.LogError("Failed to update dataflow definition. Status: {StatusCode}, Content: {Content}",
                    response.StatusCode, errorContent);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating dataflow definition for {DataflowId}", dataflowId);
            return false;
        }
    }
}