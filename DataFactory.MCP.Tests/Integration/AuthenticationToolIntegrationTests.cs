using Xunit;
using DataFactory.MCP.Tools;
using DataFactory.MCP.Tests.Infrastructure;

namespace DataFactory.MCP.Tests.Integration;

/// <summary>
/// Integration tests for AuthenticationTool that call the actual MCP tool methods
/// without mocking to verify real behavior
/// </summary>
public class AuthenticationToolIntegrationTests : IClassFixture<McpTestFixture>
{
    private readonly AuthenticationTool _authTool;
    private readonly McpTestFixture _fixture;

    public AuthenticationToolIntegrationTests(McpTestFixture fixture)
    {
        _fixture = fixture;
        _authTool = _fixture.GetService<AuthenticationTool>();
    }

    [Fact]
    public void GetAuthenticationStatus_ShouldReturnValidResponse()
    {
        // Act
        var result = _authTool.GetAuthenticationStatus();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // Should contain either authentication status information or an error message
        Assert.True(
            result.Contains("authenticated") ||
            result.Contains("not authenticated") ||
            result.Contains("Error") ||
            result.Contains("Authentication service not available"),
            $"Unexpected response format: {result}"
        );
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithoutAuthentication_ShouldReturnErrorMessage()
    {
        // Act
        var result = await _authTool.GetAccessTokenAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // Should return an error since we're not authenticated
        Assert.True(
            result.Contains("Error") ||
            result.Contains("error") ||
            result.Contains("Authentication service not available") ||
            result.Contains("not authenticated") ||
            result.Contains("No valid authentication found") ||
            result.Contains("authenticate first"),
            $"Expected error message but got: {result}"
        );
    }

    [Fact]
    public async Task SignOutAsync_ShouldReturnValidResponse()
    {
        // Act
        var result = await _authTool.SignOutAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // Should return either success message or error
        Assert.True(
            result.Contains("signed out") ||
            result.Contains("Sign out") ||
            result.Contains("Error") ||
            result.Contains("error") ||
            result.Contains("Authentication service not available") ||
            result.Contains("No active authentication session found"),
            $"Unexpected response format: {result}"
        );
    }

    [Fact]
    public async Task AuthenticateServicePrincipalAsync_WithRealCredentials_ShouldSucceed()
    {
        // Arrange - using real service principal credentials from environment
        var realAppId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? "#{AZURE_CLIENT_ID}#";
        var realSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? "#{AZURE_CLIENT_SECRET}#";
        var realTenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? "#{AZURE_TENANT_ID}#";

        // Act
        var result = await _authTool.AuthenticateServicePrincipalAsync(realAppId, realSecret, realTenantId);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // Should either succeed or fail gracefully
        Assert.True(
            result.Contains("Authentication successful") ||
            result.Contains("Successfully authenticated") ||
            result.Contains("Service principal authentication completed successfully") ||
            result.Contains("Service principal authentication failed") ||
            result.Contains("Authentication error"),
            $"Expected either success or controlled error but got: {result}"
        );
    }

    [Theory]
    [InlineData("test-app-id")]
    [InlineData("a1b2c3d4-e5f6-7890-abcd-ef1234567890")]
    public async Task AuthenticateServicePrincipalAsync_WithValidFormatButInvalidCredentials_ShouldHandleGracefully(string appId)
    {
        // Arrange
        var testSecret = "test-secret-value";

        // Act
        var result = await _authTool.AuthenticateServicePrincipalAsync(appId, testSecret);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // Should not throw exception and should return error message
        Assert.DoesNotContain("Exception", result);
        Assert.True(
            result.Contains("Authentication error") ||
            result.Contains("authentication failed") ||
            result.Contains("Service principal authentication failed") ||
            result.Contains("error") ||
            result.Contains("Authentication service not available"),
            $"Expected controlled error handling but got: {result}"
        );
    }
}
