using Xunit;
using DataFactory.MCP.Tools;
using DataFactory.MCP.Tests.Infrastructure;
using System.Text.Json;

namespace DataFactory.MCP.Tests.Integration;

/// <summary>
/// Integration tests for GatewayTool that call the actual MCP tool methods
/// without mocking to verify real behavior
/// </summary>
public class GatewayToolIntegrationTests : IClassFixture<McpTestFixture>
{
    private readonly GatewayTool _gatewayTool;
    private readonly McpTestFixture _fixture;

    public GatewayToolIntegrationTests(McpTestFixture fixture)
    {
        _fixture = fixture;
        _gatewayTool = _fixture.GetService<GatewayTool>();
    }

    [Fact]
    public async Task ListGatewaysAsync_WithoutAuthentication_ShouldReturnAuthenticationError()
    {
        // Act
        var result = await _gatewayTool.ListGatewaysAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // Should return authentication error since we're not authenticated
        Assert.True(
            result.Contains("Authentication error") ||
            result.Contains("Unauthorized") ||
            result.Contains("authentication") ||
            result.Contains("Error") ||
            result.Contains("No valid authentication found") ||
            result.Contains("authenticate first"),
            $"Expected authentication error but got: {result}"
        );
    }

    [Fact]
    public async Task ListGatewaysAsync_WithContinuationToken_ShouldHandleTokenParameter()
    {
        // Arrange
        var testToken = "test-continuation-token";

        // Act
        var result = await _gatewayTool.ListGatewaysAsync(testToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // Should return either data or authentication error (since we're not authenticated)
        // But it should not throw an exception due to the continuation token parameter
        Assert.DoesNotContain("Exception", result);
        Assert.True(
            result.Contains("Authentication error") ||
            result.Contains("gateways") ||
            result.Contains("Error") ||
            result.Contains("No gateways found"),
            $"Unexpected response format: {result}"
        );
    }

    [Fact]
    public async Task GetGatewayAsync_WithEmptyGatewayId_ShouldReturnValidationError()
    {
        // Test empty string
        var result1 = await _gatewayTool.GetGatewayAsync("");
        Assert.Contains("Gateway ID is required", result1);

        // Test whitespace
        var result2 = await _gatewayTool.GetGatewayAsync("   ");
        Assert.Contains("Gateway ID is required", result2);

        // Test null (will be handled by method signature, but let's test if it gets to validation)
        var result3 = await _gatewayTool.GetGatewayAsync(null!);
        Assert.Contains("Gateway ID is required", result3);
    }

    [Fact]
    public async Task GetGatewayAsync_WithValidGatewayIdFormat_ShouldHandleGracefully()
    {
        // Arrange - using a valid GUID format that won't exist
        var testGatewayId = "12345678-1234-1234-1234-123456789abc";

        // Act
        var result = await _gatewayTool.GetGatewayAsync(testGatewayId);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // Should return either authentication error or not found error
        Assert.True(
            result.Contains("Authentication error") ||
            result.Contains("not found") ||
            result.Contains("don't have permission") ||
            result.Contains("Error"),
            $"Expected controlled error but got: {result}"
        );
    }

    [Theory]
    [InlineData("gateway-1")]
    [InlineData("12345")]
    [InlineData("test-gateway-id")]
    [InlineData("a1b2c3d4-e5f6-7890-abcd-ef1234567890")]
    public async Task GetGatewayAsync_WithVariousGatewayIdFormats_ShouldHandleAllFormats(string gatewayId)
    {
        // Act
        var result = await _gatewayTool.GetGatewayAsync(gatewayId);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // Should not throw exception regardless of ID format
        Assert.DoesNotContain("Exception", result);

        // Should return controlled error message
        Assert.True(
            result.Contains("Authentication error") ||
            result.Contains("not found") ||
            result.Contains("don't have permission") ||
            result.Contains("Error retrieving gateway"),
            $"Expected controlled error for gateway ID '{gatewayId}' but got: {result}"
        );
    }

    [Fact]
    public async Task ListGatewaysAsync_ResponseFormat_ShouldBeValidWhenSuccessful()
    {
        // Act
        var result = await _gatewayTool.ListGatewaysAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // If the response is not an error message, it should be valid JSON
        if (!result.Contains("Authentication error") && !result.Contains("Error"))
        {
            // Try to parse as JSON to verify format
            Assert.True(IsValidJson(result), $"Response should be valid JSON when successful: {result}");

            // If it's a successful response, it should have expected structure
            if (IsValidJson(result))
            {
                var jsonDoc = JsonDocument.Parse(result);
                var root = jsonDoc.RootElement;

                // Should have the expected properties for a gateway list response
                Assert.True(
                    root.TryGetProperty("totalCount", out _) ||
                    root.TryGetProperty("TotalCount", out _) ||
                    result.Contains("No gateways found"),
                    "Successful response should have totalCount property or indicate no gateways found"
                );
            }
        }
    }

    [Fact]
    public async Task GetGatewayAsync_ResponseFormat_ShouldBeValidWhenSuccessful()
    {
        // Arrange
        var testGatewayId = "test-gateway-id";

        // Act
        var result = await _gatewayTool.GetGatewayAsync(testGatewayId);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // If the response is not an error message, it should be valid JSON
        if (!result.Contains("Authentication error") &&
            !result.Contains("not found") &&
            !result.Contains("Error") &&
            !result.Contains("Gateway ID is required"))
        {
            // Try to parse as JSON to verify format
            Assert.True(IsValidJson(result), $"Response should be valid JSON when successful: {result}");
        }
    }

    private static bool IsValidJson(string jsonString)
    {
        try
        {
            JsonDocument.Parse(jsonString);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
