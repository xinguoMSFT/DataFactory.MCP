using DataFactory.MCP.Abstractions;
using DataFactory.MCP.Abstractions.Interfaces;
using DataFactory.MCP.Configuration;
using DataFactory.MCP.Models.Connection;
using DataFactory.MCP.Models.Dataflow;
using DataFactory.MCP.Models.Dataflow.Definition;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace DataFactory.MCP.Services;

/// <summary>
/// Service for processing and transforming dataflow definitions
/// </summary>
public class DataflowDefinitionProcessor : IDataflowDefinitionProcessor
{
    private readonly ILogger<DataflowDefinitionProcessor> _logger;
    private readonly IDataTransformationService _dataTransformationService;

    public DataflowDefinitionProcessor(
        ILogger<DataflowDefinitionProcessor> logger,
        IDataTransformationService dataTransformationService)
    {
        _logger = logger;
        _dataTransformationService = dataTransformationService;
    }

    public DecodedDataflowDefinition DecodeDefinition(DataflowDefinition rawDefinition)
    {
        var decoded = new DecodedDataflowDefinition
        {
            RawParts = rawDefinition?.Parts ?? new List<DataflowDefinitionPart>()
        };

        foreach (var part in decoded.RawParts)
        {
            if (string.IsNullOrEmpty(part.Payload)) continue;

            try
            {
                var decodedBytes = Convert.FromBase64String(part.Payload);
                var decodedText = Encoding.UTF8.GetString(decodedBytes);

                switch (part.Path.ToLowerInvariant())
                {
                    case "querymetadata.json":
                        decoded.QueryMetadata = _dataTransformationService.ParseJsonContent(decodedText);
                        break;
                    case "mashup.pq":
                        decoded.MashupQuery = decodedText;
                        break;
                    case ".platform":
                        decoded.PlatformMetadata = _dataTransformationService.ParseJsonContent(decodedText);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decode part {PartPath}", part.Path);
            }
        }

        return decoded;
    }

    public DataflowDefinition AddConnectionsToDefinition(
        DataflowDefinition definition,
        IEnumerable<(Connection Connection, string ConnectionId, string? ClusterId)> connections)
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

            // Create updated metadata with all connections
            var updatedMetadata = CreateUpdatedQueryMetadataWithConnections(metadata, connections);

            // Encode updated metadata back to Base64
            var updatedMetadataJson = JsonSerializer.Serialize(updatedMetadata, JsonSerializerOptionsProvider.Indented);
            var updatedBytes = Encoding.UTF8.GetBytes(updatedMetadataJson);
            queryMetadataPart.Payload = Convert.ToBase64String(updatedBytes);
        }

        return definition;
    }

    private Dictionary<string, object> CreateUpdatedQueryMetadataWithConnections(
        JsonElement currentMetadata,
        IEnumerable<(Connection Connection, string ConnectionId, string? ClusterId)> connections)
    {
        // Convert the entire JsonElement to a Dictionary for easier manipulation
        var metadataDict = _dataTransformationService.JsonElementToDictionary(currentMetadata);

        // Ensure documentLocale is set (required for proper date/number parsing)
        if (!metadataDict.ContainsKey("documentLocale"))
        {
            metadataDict["documentLocale"] = "en-US";
        }

        // Handle connections array
        if (!metadataDict.ContainsKey("connections"))
        {
            metadataDict["connections"] = new List<object>();
        }

        var connectionsList = metadataDict["connections"] as List<object> ?? new List<object>();

        foreach (var (connection, connectionId, clusterId) in connections)
        {
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

            // Check if connection already exists (check for both old format and new Fabric format)
            bool connectionExists = connectionsList.Any(conn =>
            {
                if (conn is Dictionary<string, object> dict && dict.ContainsKey("connectionId"))
                {
                    var existingConnectionId = dict["connectionId"]?.ToString();
                    // Check if it matches the plain connectionId or the Fabric format
                    return existingConnectionId == connectionId ||
                           existingConnectionId == connectionIdValue ||
                           existingConnectionId?.Contains($"\"DatasourceId\":\"{connectionId}\"") == true;
                }
                return false;
            });

            if (!connectionExists)
            {
                // Add new connection
                var newConnection = new Dictionary<string, object>
                {
                    ["connectionId"] = connectionIdValue,
                    ["kind"] = connection.ConnectionDetails.Type,
                    ["path"] = connection.ConnectionDetails.Path
                };
                connectionsList.Add(newConnection);
            }
        }

        metadataDict["connections"] = connectionsList;
        return metadataDict;
    }

    public DataflowDefinition AddOrUpdateQueryInDefinition(
        DataflowDefinition definition,
        string queryName,
        string mCode,
        string? attribute = null,
        string? sectionAttribute = null)
    {
        // Step 1: Update the mashup.pq file with the new/updated query
        var mashupPart = definition.Parts?.FirstOrDefault(p =>
            p.Path?.Equals("mashup.pq", StringComparison.OrdinalIgnoreCase) == true);

        if (mashupPart?.Payload != null)
        {
            var decodedBytes = Convert.FromBase64String(mashupPart.Payload);
            var currentMashup = Encoding.UTF8.GetString(decodedBytes);

            var updatedMashup = UpdateMashupWithQuery(currentMashup, queryName, mCode, attribute, sectionAttribute);

            var updatedBytes = Encoding.UTF8.GetBytes(updatedMashup);
            mashupPart.Payload = Convert.ToBase64String(updatedBytes);
        }

        // Step 2: Update the queryMetadata.json file with the query metadata
        var queryMetadataPart = definition.Parts?.FirstOrDefault(p =>
            p.Path?.Equals("querymetadata.json", StringComparison.OrdinalIgnoreCase) == true);

        if (queryMetadataPart?.Payload != null)
        {
            var decodedBytes = Convert.FromBase64String(queryMetadataPart.Payload);
            var currentMetadataJson = Encoding.UTF8.GetString(decodedBytes);

            using var document = JsonDocument.Parse(currentMetadataJson);
            var metadata = document.RootElement;

            var updatedMetadata = CreateUpdatedQueryMetadataWithQuery(metadata, queryName);

            var updatedMetadataJson = JsonSerializer.Serialize(updatedMetadata, JsonSerializerOptionsProvider.Indented);
            var updatedBytes = Encoding.UTF8.GetBytes(updatedMetadataJson);
            queryMetadataPart.Payload = Convert.ToBase64String(updatedBytes);
        }

        return definition;
    }

    private string UpdateMashupWithQuery(string currentMashup, string queryName, string mCode, string? attribute = null, string? sectionAttribute = null)
    {
        // Normalize query name for M code (handle special characters)
        var normalizedQueryName = NormalizeQueryName(queryName);
        var sharedDeclaration = $"shared {normalizedQueryName} =";

        // Check if query already exists - also check for attribute prefix
        var lines = currentMashup.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
        var queryStartIndex = -1;
        var queryEndIndex = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            var trimmedLine = lines[i].TrimStart();
            // Check if this line or the next line (after attribute) contains the shared declaration
            if (trimmedLine.StartsWith(sharedDeclaration, StringComparison.OrdinalIgnoreCase) ||
                (trimmedLine.StartsWith("[") && i + 1 < lines.Count &&
                 lines[i + 1].TrimStart().StartsWith(sharedDeclaration, StringComparison.OrdinalIgnoreCase)))
            {
                // If this line is an attribute, start from here
                if (trimmedLine.StartsWith("[") && !trimmedLine.StartsWith("[StagingDefinition", StringComparison.OrdinalIgnoreCase))
                {
                    queryStartIndex = i;
                }
                else if (trimmedLine.StartsWith(sharedDeclaration, StringComparison.OrdinalIgnoreCase))
                {
                    queryStartIndex = i;
                }
                else
                {
                    continue;
                }

                // Find the end of this query (next attribute/shared or end of file)
                for (int j = queryStartIndex + 1; j < lines.Count; j++)
                {
                    var nextTrimmed = lines[j].TrimStart();
                    if (nextTrimmed.StartsWith("shared ", StringComparison.OrdinalIgnoreCase) ||
                        (nextTrimmed.StartsWith("[") && !nextTrimmed.StartsWith("[StagingDefinition", StringComparison.OrdinalIgnoreCase)))
                    {
                        queryEndIndex = j - 1;
                        break;
                    }
                }
                if (queryEndIndex == -1) queryEndIndex = lines.Count - 1;
                break;
            }
        }

        // Build the new query declaration with attribute
        var newQueryCode = BuildQueryDeclaration(normalizedQueryName, mCode, attribute);

        if (queryStartIndex >= 0)
        {
            // Replace existing query
            lines.RemoveRange(queryStartIndex, queryEndIndex - queryStartIndex + 1);
            lines.Insert(queryStartIndex, newQueryCode);
        }
        else
        {
            // Add new query at the end
            // Ensure there's a section declaration if the mashup is empty or only has section
            if (string.IsNullOrWhiteSpace(currentMashup) || currentMashup.Trim() == "section Section1;")
            {
                var sectionDecl = !string.IsNullOrEmpty(sectionAttribute)
                    ? $"{sectionAttribute}\r\nsection Section1;"
                    : "section Section1;";
                return $"{sectionDecl}\r\n{newQueryCode}";
            }
            lines.Add(newQueryCode);
        }

        // Handle section-level attribute if provided and not already present
        var result = string.Join("\r\n", lines);
        if (!string.IsNullOrEmpty(sectionAttribute) && !result.Contains("[StagingDefinition"))
        {
            // Insert section attribute before "section Section1;"
            result = result.Replace("section Section1;", $"{sectionAttribute}\r\nsection Section1;");
        }

        return result;
    }

    private string NormalizeQueryName(string queryName)
    {
        // If query name contains spaces or special characters, wrap in #""
        if (queryName.Contains(' ') || queryName.Contains('(') || queryName.Contains(')'))
        {
            return $"#\"{queryName}\"";
        }
        return queryName;
    }

    private string BuildQueryDeclaration(string normalizedQueryName, string mCode, string? attribute = null)
    {
        // Ensure the M code starts with "let" and ends with proper structure
        var trimmedCode = mCode.Trim();

        // If the code doesn't start with "let", wrap it
        if (!trimmedCode.StartsWith("let", StringComparison.OrdinalIgnoreCase))
        {
            trimmedCode = $"let\n    Source = {trimmedCode}\nin\n    Source";
        }

        // Build declaration with optional attribute
        if (!string.IsNullOrEmpty(attribute))
        {
            return $"{attribute}\r\nshared {normalizedQueryName} = {trimmedCode};";
        }

        // Ensure it ends with a semicolon for the shared declaration
        return $"shared {normalizedQueryName} = {trimmedCode};";
    }

    private Dictionary<string, object> CreateUpdatedQueryMetadataWithQuery(
        JsonElement currentMetadata,
        string queryName)
    {
        var metadataDict = _dataTransformationService.JsonElementToDictionary(currentMetadata);

        // Ensure documentLocale is set (required for proper date/number parsing)
        if (!metadataDict.ContainsKey("documentLocale"))
        {
            metadataDict["documentLocale"] = "en-US";
        }

        // Handle queriesMetadata object
        if (!metadataDict.ContainsKey("queriesMetadata"))
        {
            metadataDict["queriesMetadata"] = new Dictionary<string, object>();
        }

        var queriesMetadata = metadataDict["queriesMetadata"] as Dictionary<string, object>
            ?? new Dictionary<string, object>();

        // Check if query already exists
        if (!queriesMetadata.ContainsKey(queryName))
        {
            // Add new query metadata with a generated GUID
            var queryId = Guid.NewGuid().ToString();

            // Determine if this is a DataDestination query (helper query for ETL)
            var isDataDestinationQuery = queryName.EndsWith("_DataDestination", StringComparison.OrdinalIgnoreCase);

            var queryMetadataEntry = new Dictionary<string, object>
            {
                ["queryId"] = queryId,
                ["queryName"] = queryName,
                ["loadEnabled"] = false  // Prevent loading to default destination
            };

            // DataDestination queries should be hidden from UI
            if (isDataDestinationQuery)
            {
                queryMetadataEntry["isHidden"] = true;
            }

            queriesMetadata[queryName] = queryMetadataEntry;
        }
        else
        {
            // Update existing query to ensure loadEnabled is set
            if (queriesMetadata[queryName] is Dictionary<string, object> existingEntry)
            {
                if (!existingEntry.ContainsKey("loadEnabled"))
                {
                    existingEntry["loadEnabled"] = false;
                }

                // Ensure DataDestination queries are hidden
                var isDataDestinationQuery = queryName.EndsWith("_DataDestination", StringComparison.OrdinalIgnoreCase);
                if (isDataDestinationQuery && !existingEntry.ContainsKey("isHidden"))
                {
                    existingEntry["isHidden"] = true;
                }
            }
        }

        metadataDict["queriesMetadata"] = queriesMetadata;
        return metadataDict;
    }
}