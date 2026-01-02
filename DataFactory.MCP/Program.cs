using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DataFactory.MCP.Tools;
using DataFactory.MCP.Abstractions.Interfaces;
using DataFactory.MCP.Abstractions.Interfaces.DMTSv2;
using DataFactory.MCP.Services;
using DataFactory.MCP.Services.DMTSv2;
using DataFactory.MCP.Models.Connection.Factories;
using DataFactory.MCP.Configuration;
using DataFactory.MCP.Infrastructure.Http;

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Create a logger for startup tracing
using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace));
var logger = loggerFactory.CreateLogger("DataFactory.MCP.Startup");

// Register authentication handlers as transient (DelegatingHandlers must be transient)
builder.Services.AddTransient<FabricAuthenticationHandler>();
builder.Services.AddTransient<AzureResourceManagerAuthenticationHandler>();

// Register named HttpClients with authentication handlers to avoid socket exhaustion
// and centralize authentication logic. Base URLs use centralized ApiVersions constants.
builder.Services.AddHttpClient(HttpClientNames.FabricApi, client =>
{
    client.BaseAddress = new Uri(ApiVersions.Fabric.V1BaseUrl + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<FabricAuthenticationHandler>();

builder.Services.AddHttpClient(HttpClientNames.AzureResourceManager, client =>
{
    client.BaseAddress = new Uri("https://management.azure.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AzureResourceManagerAuthenticationHandler>();

builder.Services.AddHttpClient(HttpClientNames.PowerBiV2Api, client =>
{
    client.BaseAddress = new Uri(ApiVersions.PowerBi.V2BaseUrl + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<FabricAuthenticationHandler>();

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services
    .AddSingleton<IValidationService, ValidationService>()
    .AddSingleton<IAuthenticationService, AuthenticationService>()
    .AddSingleton<IArrowDataReaderService, ArrowDataReaderService>()
    .AddSingleton<IGatewayClusterDatasourceService, GatewayClusterDatasourceService>()
    .AddSingleton<IDataTransformationService, DataTransformationService>()
    .AddSingleton<IDataflowDefinitionProcessor, DataflowDefinitionProcessor>()
    .AddSingleton<IFabricGatewayService, FabricGatewayService>()
    .AddSingleton<IFabricConnectionService, FabricConnectionService>()
    .AddSingleton<IFabricWorkspaceService, FabricWorkspaceService>()
    .AddSingleton<IFabricDataflowService, FabricDataflowService>()
    .AddSingleton<IFabricCapacityService, FabricCapacityService>()
    .AddSingleton<IAzureResourceDiscoveryService, AzureResourceDiscoveryService>()
    .AddSingleton<FabricDataSourceConnectionFactory>();

// Configure MCP server with conditional tool registration
logger.LogInformation("Registering core MCP tools...");
var mcpBuilder = builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<AuthenticationTool>()
    .WithTools<GatewayTool>()
    .WithTools<ConnectionsTool>()
    .WithTools<WorkspacesTool>()
    .WithTools<DataflowTool>()
    .WithTools<CapacityTool>()
    .WithTools<AzureResourceDiscoveryTool>();

// Conditionally enable DataflowQueryTool based on feature flag
mcpBuilder.RegisterToolWithFeatureFlag<DataflowQueryTool>(
    builder.Configuration,
    args,
    FeatureFlags.DataflowQuery,
    nameof(DataflowQueryTool),
    logger);

await builder.Build().RunAsync();
