using Xunit;
using DataFactory.MCP.Tools.Pipeline;
using DataFactory.MCP.Tests.Infrastructure;
using System.Text.Json;
using DataFactory.MCP.Models;

namespace DataFactory.MCP.Tests.Integration;

/// <summary>
/// Integration tests for PipelineTool that call the actual MCP tool methods
/// without mocking to verify real behavior
/// </summary>
public class PipelineToolIntegrationTests : FabricToolIntegrationTestBase
{
    private readonly PipelineTool _pipelineTool;

    // Test workspace IDs
    private const string TestWorkspaceId = "349f40ea-ecb0-4fe6-baf4-884b2887b074";
    private const string InvalidWorkspaceId = "invalid-workspace-id";
    private const string InvalidPipelineId = "00000000-0000-0000-0000-000000000001";

    public PipelineToolIntegrationTests(McpTestFixture fixture) : base(fixture)
    {
        _pipelineTool = Fixture.GetService<PipelineTool>();
    }

    #region DI Registration

    [Fact]
    public void PipelineTool_ShouldBeRegisteredInDI()
    {
        // Assert
        Assert.NotNull(_pipelineTool);
        Assert.IsType<PipelineTool>(_pipelineTool);
    }

    #endregion

    #region ListFabricPipelinesAsync - Unauthenticated

    [Fact]
    public async Task ListFabricPipelinesAsync_WithoutAuthentication_ShouldReturnAuthenticationError()
    {
        // Act
        var result = await _pipelineTool.ListFabricPipelinesAsync(TestWorkspaceId);

        // Assert
        AssertAuthenticationError(result);
    }

    [Fact]
    public async Task ListFabricPipelinesAsync_WithContinuationToken_WithoutAuthentication_ShouldReturnAuthenticationError()
    {
        // Arrange
        var testToken = "test-continuation-token";

        // Act
        var result = await _pipelineTool.ListFabricPipelinesAsync(TestWorkspaceId, testToken);

        // Assert
        AssertAuthenticationError(result);
    }

    [Fact]
    public async Task ListFabricPipelinesAsync_WithEmptyWorkspaceId_ShouldReturnValidationError()
    {
        // Act
        var result = await _pipelineTool.ListFabricPipelinesAsync("");

        // Assert
        McpResponseAssertHelper.AssertValidationError(result, Messages.InvalidParameterEmpty("workspaceId"));
    }

    [Fact]
    public async Task ListFabricPipelinesAsync_WithNullWorkspaceId_ShouldReturnValidationError()
    {
        // Act
        var result = await _pipelineTool.ListFabricPipelinesAsync(null!);

        // Assert
        McpResponseAssertHelper.AssertValidationError(result, Messages.InvalidParameterEmpty("workspaceId"));
    }

    #endregion

    #region GetFabricPipelineAsync - Unauthenticated

    [Fact]
    public async Task GetFabricPipelineAsync_WithoutAuthentication_ShouldReturnAuthenticationError()
    {
        // Act
        var result = await _pipelineTool.GetFabricPipelineAsync(TestWorkspaceId, InvalidPipelineId);

        // Assert
        AssertAuthenticationError(result);
    }

    [Fact]
    public async Task GetFabricPipelineAsync_WithEmptyWorkspaceId_ShouldReturnValidationError()
    {
        // Act
        var result = await _pipelineTool.GetFabricPipelineAsync("", InvalidPipelineId);

        // Assert
        McpResponseAssertHelper.AssertValidationError(result, Messages.InvalidParameterEmpty("workspaceId"));
    }

    [Fact]
    public async Task GetFabricPipelineAsync_WithEmptyPipelineId_ShouldReturnValidationError()
    {
        // Act
        var result = await _pipelineTool.GetFabricPipelineAsync(TestWorkspaceId, "");

        // Assert
        McpResponseAssertHelper.AssertValidationError(result, Messages.InvalidParameterEmpty("pipelineId"));
    }

    #endregion

    #region CreateFabricPipelineAsync - Unauthenticated

    [Fact]
    public async Task CreateFabricPipelineAsync_WithoutAuthentication_ShouldReturnAuthenticationError()
    {
        // Act
        var result = await _pipelineTool.CreateFabricPipelineAsync(TestWorkspaceId, "test-pipeline");

        // Assert
        AssertAuthenticationError(result);
    }

    [Fact]
    public async Task CreateFabricPipelineAsync_WithEmptyDisplayName_ShouldReturnValidationError()
    {
        // Act
        var result = await _pipelineTool.CreateFabricPipelineAsync(TestWorkspaceId, "");

        // Assert
        McpResponseAssertHelper.AssertValidationError(result);
    }

    #endregion

    #region UpdateFabricPipelineAsync - Unauthenticated

    [Fact]
    public async Task UpdateFabricPipelineAsync_WithoutAuthentication_ShouldReturnAuthenticationError()
    {
        // Act
        var result = await _pipelineTool.UpdateFabricPipelineAsync(TestWorkspaceId, InvalidPipelineId, displayName: "updated");

        // Assert
        AssertAuthenticationError(result);
    }

    [Fact]
    public async Task UpdateFabricPipelineAsync_WithEmptyWorkspaceId_ShouldReturnValidationError()
    {
        // Act
        var result = await _pipelineTool.UpdateFabricPipelineAsync("", InvalidPipelineId, displayName: "updated");

        // Assert
        McpResponseAssertHelper.AssertValidationError(result, Messages.InvalidParameterEmpty("workspaceId"));
    }

    [Fact]
    public async Task UpdateFabricPipelineAsync_WithNoUpdates_ShouldReturnValidationError()
    {
        // Act - neither displayName nor description provided
        var result = await _pipelineTool.UpdateFabricPipelineAsync(TestWorkspaceId, InvalidPipelineId);

        // Assert
        McpResponseAssertHelper.AssertValidationError(result, "At least one of displayName or description must be provided");
    }

    #endregion

    #region GetFabricPipelineDefinitionAsync - Unauthenticated

    [Fact]
    public async Task GetFabricPipelineDefinitionAsync_WithoutAuthentication_ShouldReturnAuthenticationError()
    {
        // Act
        var result = await _pipelineTool.GetFabricPipelineDefinitionAsync(TestWorkspaceId, InvalidPipelineId);

        // Assert
        AssertAuthenticationError(result);
    }

    [Fact]
    public async Task GetFabricPipelineDefinitionAsync_WithEmptyWorkspaceId_ShouldReturnValidationError()
    {
        // Act
        var result = await _pipelineTool.GetFabricPipelineDefinitionAsync("", InvalidPipelineId);

        // Assert
        McpResponseAssertHelper.AssertValidationError(result, Messages.InvalidParameterEmpty("workspaceId"));
    }

    [Fact]
    public async Task GetFabricPipelineDefinitionAsync_WithEmptyPipelineId_ShouldReturnValidationError()
    {
        // Act
        var result = await _pipelineTool.GetFabricPipelineDefinitionAsync(TestWorkspaceId, "");

        // Assert
        McpResponseAssertHelper.AssertValidationError(result, Messages.InvalidParameterEmpty("pipelineId"));
    }

    #endregion

    #region UpdateFabricPipelineDefinitionAsync - Unauthenticated

    [Fact]
    public async Task UpdateFabricPipelineDefinitionAsync_WithoutAuthentication_ShouldReturnAuthenticationError()
    {
        // Arrange
        var definitionJson = "{\"activities\":[]}";

        // Act
        var result = await _pipelineTool.UpdateFabricPipelineDefinitionAsync(TestWorkspaceId, InvalidPipelineId, definitionJson);

        // Assert
        AssertAuthenticationError(result);
    }

    [Fact]
    public async Task UpdateFabricPipelineDefinitionAsync_WithEmptyWorkspaceId_ShouldReturnValidationError()
    {
        // Arrange
        var definitionJson = "{\"activities\":[]}";

        // Act
        var result = await _pipelineTool.UpdateFabricPipelineDefinitionAsync("", InvalidPipelineId, definitionJson);

        // Assert
        McpResponseAssertHelper.AssertValidationError(result, Messages.InvalidParameterEmpty("workspaceId"));
    }

    [Fact]
    public async Task UpdateFabricPipelineDefinitionAsync_WithInvalidJson_ShouldReturnValidationError()
    {
        // Act
        var result = await _pipelineTool.UpdateFabricPipelineDefinitionAsync(TestWorkspaceId, InvalidPipelineId, "not-valid-json{");

        // Assert
        McpResponseAssertHelper.AssertValidationError(result, "Invalid JSON format");
    }

    [Fact]
    public async Task UpdateFabricPipelineDefinitionAsync_WithEmptyDefinitionJson_ShouldReturnValidationError()
    {
        // Act
        var result = await _pipelineTool.UpdateFabricPipelineDefinitionAsync(TestWorkspaceId, InvalidPipelineId, "");

        // Assert
        McpResponseAssertHelper.AssertValidationError(result, Messages.InvalidParameterEmpty("definitionJson"));
    }

    #endregion

    #region Authenticated Scenarios

    [SkippableFact]
    public async Task ListFabricPipelinesAsync_WithAuthentication_ShouldReturnResultOrApiError()
    {
        // Arrange
        var isAuthenticated = await TryAuthenticateAsync();
        Skip.IfNot(isAuthenticated, "Skipping authenticated test - no valid credentials available");

        // Act
        var result = await _pipelineTool.ListFabricPipelinesAsync(TestWorkspaceId);

        // Assert
        AssertPipelineListResult(result);
    }

    [SkippableFact]
    public async Task ListFabricPipelinesAsync_WithInvalidWorkspaceId_ShouldReturnApiError()
    {
        // Arrange
        var isAuthenticated = await TryAuthenticateAsync();
        Skip.IfNot(isAuthenticated, "Skipping authenticated test - no valid credentials available");

        // Act
        var result = await _pipelineTool.ListFabricPipelinesAsync(InvalidWorkspaceId);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        AssertNoAuthenticationError(result);
        Assert.Contains("workspaceId must be a valid GUID", result);
    }

    [SkippableFact]
    public async Task GetFabricPipelineAsync_WithAuthentication_NonExistentPipeline_ShouldReturnError()
    {
        // Arrange
        var isAuthenticated = await TryAuthenticateAsync();
        Skip.IfNot(isAuthenticated, "Skipping authenticated test - no valid credentials available");

        // Act
        var result = await _pipelineTool.GetFabricPipelineAsync(TestWorkspaceId, InvalidPipelineId);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        SkipIfUpstreamBlocked(result);
        AssertNoAuthenticationError(result);
    }

    #endregion

    #region Helper Methods

    private static void AssertPipelineListResult(string result)
    {
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // Skip if upstream service is blocking requests
        SkipIfUpstreamBlocked(result);

        AssertNoAuthenticationError(result);

        // Should either be "No pipelines found" or valid JSON
        if (result.Contains("No pipelines found"))
        {
            Assert.Contains("No pipelines found", result);
            return;
        }

        if (result.Contains("HttpRequestError"))
        {
            McpResponseAssertHelper.AssertHttpError(result);
            return;
        }

        // If it's JSON, verify basic structure
        if (IsValidJson(result))
        {
            var jsonDoc = JsonDocument.Parse(result);
            var root = jsonDoc.RootElement;

            Assert.True(root.TryGetProperty("workspaceId", out _), "JSON response should have workspaceId property");
            Assert.True(root.TryGetProperty("pipelineCount", out _), "JSON response should have pipelineCount property");
            Assert.True(root.TryGetProperty("pipelines", out _), "JSON response should have pipelines property");
        }
    }

    #endregion
}
