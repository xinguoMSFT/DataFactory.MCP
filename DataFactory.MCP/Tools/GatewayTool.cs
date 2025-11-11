using ModelContextProtocol.Server;
using System.ComponentModel;
using DataFactory.MCP.Abstractions.Interfaces;
using DataFactory.MCP.Extensions;
using DataFactory.MCP.Models;
using DataFactory.MCP.Models.Gateway;

namespace DataFactory.MCP.Tools;

[McpServerToolType]
public class GatewayTool
{
    private readonly IFabricGatewayService _gatewayService;

    public GatewayTool(IFabricGatewayService gatewayService)
    {
        _gatewayService = gatewayService;
    }

    [McpServerTool, Description(@"Lists all gateways the user has permission for, including on-premises, on-premises (personal mode), and virtual network gateways")]
    public async Task<string> ListGatewaysAsync(
        [Description("A token for retrieving the next page of results (optional)")] string? continuationToken = null)
    {
        try
        {
            var response = await _gatewayService.ListGatewaysAsync(continuationToken);

            if (!response.Value.Any())
            {
                return Messages.NoGatewaysFound;
            }

            var result = new
            {
                TotalCount = response.Value.Count,
                ContinuationToken = response.ContinuationToken,
                HasMoreResults = !string.IsNullOrEmpty(response.ContinuationToken),
                Gateways = response.Value.Select(g => g.ToFormattedInfo())
            };

            return result.ToMcpJson();
        }
        catch (UnauthorizedAccessException ex)
        {
            return ex.ToAuthenticationError().ToMcpJson();
        }
        catch (HttpRequestException ex)
        {
            return ex.ToHttpError().ToMcpJson();
        }
        catch (Exception ex)
        {
            return ex.ToOperationError("listing gateways").ToMcpJson();
        }
    }

    [McpServerTool, Description(@"Gets details about a specific gateway by its ID")]
    public async Task<string> GetGatewayAsync(
        [Description("The ID of the gateway to retrieve")] string gatewayId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(gatewayId))
            {
                return Messages.GatewayIdRequired;
            }

            var gateway = await _gatewayService.GetGatewayAsync(gatewayId);

            if (gateway == null)
            {
                return string.Format(Messages.GatewayNotFoundTemplate, gatewayId);
            }

            var result = gateway.ToFormattedInfo();
            return result.ToMcpJson();
        }
        catch (UnauthorizedAccessException ex)
        {
            return ex.ToAuthenticationError().ToMcpJson();
        }
        catch (Exception ex)
        {
            return ex.ToOperationError("retrieving gateway").ToMcpJson();
        }
    }

    [McpServerTool, Description(@"Creates a new VNet gateway in Microsoft Fabric")]
    public async Task<string> CreateVNetGatewayAsync(
        [Description("Display name for the gateway")] string displayName,
        [Description("The capacity ID where the gateway will be created")] string capacityId,
        [Description("Azure subscription ID containing the virtual network")] string subscriptionId,
        [Description("Azure resource group name containing the virtual network")] string resourceGroupName,
        [Description("Name of the virtual network")] string virtualNetworkName,
        [Description("Name of the subnet within the virtual network")] string subnetName,
        [Description("Number of minutes of inactivity before the gateway goes to sleep. Valid values: 30, 60, 90, 120, 150, 240, 360, 480, 720, 1440 (default: 120)")] int inactivityMinutesBeforeSleep = 120,
        [Description("Number of member gateways (default: 1)")] int numberOfMemberGateways = 1)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return new ArgumentException("displayName parameter is required").ToValidationError().ToMcpJson();
            }

            if (string.IsNullOrWhiteSpace(capacityId))
            {
                return new ArgumentException("capacityId parameter is required").ToValidationError().ToMcpJson();
            }

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return new ArgumentException("subscriptionId parameter is required").ToValidationError().ToMcpJson();
            }

            if (string.IsNullOrWhiteSpace(resourceGroupName))
            {
                return new ArgumentException("resourceGroupName parameter is required").ToValidationError().ToMcpJson();
            }

            if (string.IsNullOrWhiteSpace(virtualNetworkName))
            {
                return new ArgumentException("virtualNetworkName parameter is required").ToValidationError().ToMcpJson();
            }

            if (string.IsNullOrWhiteSpace(subnetName))
            {
                return new ArgumentException("subnetName parameter is required").ToValidationError().ToMcpJson();
            }

            // Validate inactivityMinutesBeforeSleep
            var validValues = new[] { 30, 60, 90, 120, 150, 240, 360, 480, 720, 1440 };
            if (!validValues.Contains(inactivityMinutesBeforeSleep))
            {
                return new ArgumentException($"inactivityMinutesBeforeSleep must be one of: {string.Join(", ", validValues)}")
                    .ToValidationError().ToMcpJson();
            }

            var request = new CreateVNetGatewayRequest
            {
                Type = "VirtualNetwork",
                DisplayName = displayName,
                CapacityId = capacityId,
                InactivityMinutesBeforeSleep = inactivityMinutesBeforeSleep,
                NumberOfMemberGateways = numberOfMemberGateways,
                VirtualNetworkAzureResource = new VirtualNetworkAzureResource
                {
                    SubscriptionId = subscriptionId,
                    ResourceGroupName = resourceGroupName,
                    VirtualNetworkName = virtualNetworkName,
                    SubnetName = subnetName
                }
            };

            var response = await _gatewayService.CreateVNetGatewayAsync(request);

            var result = new
            {
                message = $"Successfully created VNet gateway '{response.DisplayName}' with ID '{response.Id}'",
                gateway = new
                {
                    id = response.Id,
                    displayName = response.DisplayName,
                    type = response.Type,
                    connectivityStatus = response.ConnectivityStatus,
                    capacityId = response.CapacityId
                },
                configuration = new
                {
                    subscriptionId,
                    resourceGroupName,
                    virtualNetworkName,
                    subnetName,
                    inactivityMinutesBeforeSleep,
                    numberOfMemberGateways
                }
            };

            return result.ToMcpJson();
        }
        catch (UnauthorizedAccessException ex)
        {
            return ex.ToAuthenticationError().ToMcpJson();
        }
        catch (HttpRequestException ex)
        {
            return ex.ToHttpError().ToMcpJson();
        }
        catch (Exception ex)
        {
            return ex.ToOperationError($"creating VNet gateway '{displayName}'").ToMcpJson();
        }
    }
}
