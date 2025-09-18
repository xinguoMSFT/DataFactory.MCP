using Xunit;
using DataFactory.MCP.Tools;
using DataFactory.MCP.Tests.Infrastructure;
using DataFactory.MCP.Models;
using System.Text.Json;

namespace DataFactory.MCP.Tests.Integration;

/// <summary>
/// Integration tests for GatewayTool that call the actual MCP tool methods
/// without mocking to verify real behavior
/// </summary>
public class GatewayToolIntegrationTests : FabricToolIntegrationTestBase
{
    private readonly GatewayTool _gatewayTool;

    public GatewayToolIntegrationTests(McpTestFixture fixture) : base(fixture)
    {
        _gatewayTool = Fixture.GetService<GatewayTool>();
    }

    [Fact]
    public async Task ListGatewaysAsync_WithoutAuthentication_ShouldReturnAuthenticationError()
    {
        // Act
        var result = await _gatewayTool.ListGatewaysAsync();

        // Assert
        AssertAuthenticationError(result);
    }

    [Fact]
    public async Task ListGatewaysAsync_WithContinuationToken_WithoutAuthentication_ShouldReturnAuthenticationError()
    {
        // Arrange
        var testToken = "test-continuation-token";

        // Act
        var result = await _gatewayTool.ListGatewaysAsync(testToken);

        // Assert
        AssertAuthenticationError(result);
    }

    [Fact]
    public async Task GetGatewayAsync_WithEmptyGatewayId_ShouldReturnValidationError()
    {
        // Test empty string
        var result1 = await _gatewayTool.GetGatewayAsync("");
        Assert.Contains(Messages.GatewayIdRequired, result1);

        // Test whitespace
        var result2 = await _gatewayTool.GetGatewayAsync("   ");
        Assert.Contains(Messages.GatewayIdRequired, result2);

        // Test null (will be handled by method signature, but let's test if it gets to validation)
        var result3 = await _gatewayTool.GetGatewayAsync(null!);
        Assert.Contains(Messages.GatewayIdRequired, result3);
    }

    [SkippableFact]
    public async Task ListGatewaysAsync_WithAuthentication_ShouldReturnValidResponse()
    {
        // Arrange - Try to authenticate
        var isAuthenticated = await TryAuthenticateAsync();

        Skip.IfNot(isAuthenticated, "Skipping authenticated test - no valid credentials available");

        // Act
        var result = await _gatewayTool.ListGatewaysAsync();

        // Assert
        AssertResult("gateways", result);
    }

    [SkippableFact]
    public async Task ListGatewaysAsync_WithAuthentication_AndContinuationToken_ShouldHandleTokenParameter()
    {
        // Arrange - Try to authenticate
        var isAuthenticated = await TryAuthenticateAsync();

        Skip.IfNot(isAuthenticated, "Skipping authenticated test - no valid credentials available");

        var testToken = "test-continuation-token-12345";

        // Act
        var result = await _gatewayTool.ListGatewaysAsync(testToken);

        // Assert
        AssertResult("gateways", result);
    }

    [SkippableTheory]
    [InlineData("non-existent-gateway-id")]
    [InlineData("invalid-guid-format")]
    [InlineData("test-gateway-that-does-not-exist")]
    public async Task GetGatewayAsync_WithAuthentication_AndNonExistentId_ShouldReturnNotFoundMessage(string gatewayId)
    {
        // Arrange - Try to authenticate
        var isAuthenticated = await TryAuthenticateAsync();

        Skip.IfNot(isAuthenticated, "Skipping authenticated test - no valid credentials available");

        // Act
        var result = await _gatewayTool.GetGatewayAsync(gatewayId);

        // Assert
        AssertOneItemResult("Gateway", gatewayId, result);
    }

    [Fact]
    public void GatewayTool_ShouldBeRegisteredInDI()
    {
        // Assert
        Assert.NotNull(_gatewayTool);
        Assert.IsType<GatewayTool>(_gatewayTool);
    }
}