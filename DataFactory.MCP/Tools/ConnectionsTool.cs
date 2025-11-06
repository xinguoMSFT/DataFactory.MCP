using ModelContextProtocol.Server;
using System.ComponentModel;
using DataFactory.MCP.Abstractions.Interfaces;
using DataFactory.MCP.Extensions;
using DataFactory.MCP.Models;
using DataFactory.MCP.Models.Connection;
using System.Text.Json;

namespace DataFactory.MCP.Tools;

[McpServerToolType]
public class ConnectionsTool
{
    private readonly IFabricConnectionService _connectionService;

    public ConnectionsTool(IFabricConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    [McpServerTool, Description(@"Lists all connections the user has permission for, including on-premises, virtual network and cloud connections")]
    public async Task<string> ListConnectionsAsync(
        [Description("A token for retrieving the next page of results (optional)")] string? continuationToken = null)
    {
        try
        {
            var response = await _connectionService.ListConnectionsAsync(continuationToken);

            if (!response.Value.Any())
            {
                return Messages.NoConnectionsFound;
            }

            var result = new
            {
                TotalCount = response.Value.Count,
                ContinuationToken = response.ContinuationToken,
                HasMoreResults = !string.IsNullOrEmpty(response.ContinuationToken),
                Connections = response.Value.Select(c => c.ToFormattedInfo())
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return string.Format(Messages.AuthenticationErrorTemplate, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return string.Format(Messages.ApiRequestFailedTemplate, ex.Message);
        }
        catch (Exception ex)
        {
            return string.Format(Messages.ErrorListingConnectionsTemplate, ex.Message);
        }
    }

    [McpServerTool, Description(@"Gets details about a specific connection by its ID")]
    public async Task<string> GetConnectionAsync(
        [Description("The ID of the connection to retrieve")] string connectionId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return Messages.ConnectionIdRequired;
            }

            var connection = await _connectionService.GetConnectionAsync(connectionId);

            if (connection == null)
            {
                return string.Format(Messages.ConnectionNotFoundTemplate, connectionId);
            }

            var result = connection.ToFormattedInfo();
            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return string.Format(Messages.AuthenticationErrorTemplate, ex.Message);
        }
        catch (Exception ex)
        {
            return string.Format(Messages.ErrorRetrievingConnectionTemplate, ex.Message);
        }
    }

    [McpServerTool, Description(@"Creates a new cloud SQL Server connection that can be shared across the organization")]
    public async Task<string> CreateCloudConnectionAsync(
        [Description("The display name of the connection (required, max 200 characters)")] string displayName,
        [Description("The SQL Server name (e.g., server.database.windows.net)")] string serverName,
        [Description("The database name")] string databaseName,
        [Description("The username for SQL Server authentication")] string username,
        [Description("The password for SQL Server authentication")] string password,
        [Description("The privacy level (Organizational, Private, Public, None) - defaults to Organizational")] string privacyLevel = "Organizational",
        [Description("Whether to skip connection testing during creation - defaults to false")] bool skipTestConnection = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "Display name is required";
            }

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return "Server name is required";
            }

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                return "Database name is required";
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return "Username is required";
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return "Password is required";
            }

            // Parse privacy level enum
            if (!Enum.TryParse<PrivacyLevel>(privacyLevel, true, out var parsedPrivacyLevel))
            {
                return $"Invalid privacy level: {privacyLevel}. Valid values are: None, Private, Organizational, Public";
            }

            var request = new CreateCloudConnectionRequest
            {
                DisplayName = displayName,
                PrivacyLevel = parsedPrivacyLevel,
                AllowConnectionUsageInGateway = false, // Default to false
                ConnectionDetails = new CreateConnectionDetails
                {
                    Type = "SQL",
                    CreationMethod = "SQL",
                    Parameters = new List<ConnectionDetailsParameter>
                    {
                        new ConnectionDetailsTextParameter { Name = "server", Value = serverName },
                        new ConnectionDetailsTextParameter { Name = "database", Value = databaseName }
                    }
                },
                CredentialDetails = new CreateCredentialDetails
                {
                    SingleSignOnType = SingleSignOnType.None,
                    ConnectionEncryption = ConnectionEncryption.Encrypted, // Default to encrypted
                    SkipTestConnection = skipTestConnection,
                    Credentials = new BasicCredentials
                    {
                        Username = username,
                        Password = password
                    }
                }
            };

            var connection = await _connectionService.CreateCloudConnectionAsync(request);

            var result = new
            {
                Success = true,
                Message = "Cloud connection created successfully",
                Connection = new
                {
                    Id = connection.Id,
                    DisplayName = connection.DisplayName,
                    ConnectivityType = connection.ConnectivityType.ToString(),
                    ConnectionType = connection.ConnectionDetails.Type,
                    Path = connection.ConnectionDetails.Path,
                    PrivacyLevel = connection.PrivacyLevel?.ToString(),
                    AllowConnectionUsageInGateway = connection.AllowConnectionUsageInGateway,
                    CredentialType = connection.CredentialDetails?.CredentialType.ToString(),
                    ConnectionEncryption = connection.CredentialDetails?.ConnectionEncryption.ToString()
                }
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return string.Format(Messages.AuthenticationErrorTemplate, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return string.Format(Messages.ApiRequestFailedTemplate, ex.Message);
        }
        catch (Exception ex)
        {
            return $"Error creating cloud connection: {ex.Message}";
        }
    }

    [McpServerTool, Description(@"Creates a new virtual network gateway SQL Server connection")]
    public async Task<string> CreateVNetGatewayConnectionAsync(
        [Description("The display name of the connection (required, max 200 characters)")] string displayName,
        [Description("The virtual network gateway ID (UUID)")] string gatewayId,
        [Description("The SQL Server name (e.g., server.database.windows.net)")] string serverName,
        [Description("The database name")] string databaseName,
        [Description("The username for SQL Server authentication")] string username,
        [Description("The password for SQL Server authentication")] string password,
        [Description("The privacy level (Organizational, Private, Public, None) - defaults to Organizational")] string privacyLevel = "Organizational")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "Display name is required";
            }

            if (string.IsNullOrWhiteSpace(gatewayId))
            {
                return "Gateway ID is required";
            }

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return "Server name is required";
            }

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                return "Database name is required";
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return "Username is required";
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return "Password is required";
            }

            // Parse privacy level enum
            if (!Enum.TryParse<PrivacyLevel>(privacyLevel, true, out var parsedPrivacyLevel))
            {
                return $"Invalid privacy level: {privacyLevel}. Valid values are: None, Private, Organizational, Public";
            }

            var request = new CreateVirtualNetworkGatewayConnectionRequest
            {
                DisplayName = displayName,
                GatewayId = gatewayId,
                PrivacyLevel = parsedPrivacyLevel,
                ConnectionDetails = new CreateConnectionDetails
                {
                    Type = "SQL",
                    CreationMethod = "SQL",
                    Parameters = new List<ConnectionDetailsParameter>
                    {
                        new ConnectionDetailsTextParameter { Name = "server", Value = serverName },
                        new ConnectionDetailsTextParameter { Name = "database", Value = databaseName }
                    }
                },
                CredentialDetails = new CreateCredentialDetails
                {
                    SingleSignOnType = SingleSignOnType.None,
                    ConnectionEncryption = ConnectionEncryption.Encrypted,
                    SkipTestConnection = true, // VNet Gateway connections often need to skip test
                    Credentials = new BasicCredentials
                    {
                        Username = username,
                        Password = password
                    }
                }
            };

            var connection = await _connectionService.CreateVirtualNetworkGatewayConnectionAsync(request);

            var result = new
            {
                Success = true,
                Message = "Virtual network gateway connection created successfully",
                Connection = new
                {
                    Id = connection.Id,
                    DisplayName = connection.DisplayName,
                    GatewayId = connection.GatewayId,
                    ConnectivityType = connection.ConnectivityType.ToString(),
                    ConnectionType = connection.ConnectionDetails.Type,
                    Path = connection.ConnectionDetails.Path,
                    PrivacyLevel = connection.PrivacyLevel?.ToString(),
                    CredentialType = connection.CredentialDetails?.CredentialType.ToString(),
                    ConnectionEncryption = connection.CredentialDetails?.ConnectionEncryption.ToString()
                }
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return string.Format(Messages.AuthenticationErrorTemplate, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return string.Format(Messages.ApiRequestFailedTemplate, ex.Message);
        }
        catch (Exception ex)
        {
            return $"Error creating virtual network gateway connection: {ex.Message}";
        }
    }
}