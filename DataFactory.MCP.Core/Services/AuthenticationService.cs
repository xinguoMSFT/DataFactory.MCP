using DataFactory.MCP.Abstractions.Interfaces;
using DataFactory.MCP.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

namespace DataFactory.MCP.Services;

/// <summary>
/// Azure AD authentication service implementation for DataFactory MCP
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly ILogger<AuthenticationService> _logger;
    private McpAuthenticationResult? _currentAuth;
    private IPublicClientApplication? _publicClientApp;
    private Task<AuthenticationResult>? _pendingDeviceAuth;
    private string? _pendingDeviceInstructions;
    private DateTime? _deviceAuthStartTime;
    private readonly TimeSpan _deviceAuthTimeout = TimeSpan.FromMinutes(15); // Match Azure AD default

    public AuthenticationService(ILogger<AuthenticationService> logger)
    {
        _logger = logger;
        InitializeClientApplications();
    }

    private void InitializeClientApplications()
    {
        try
        {
            // Initialize public client for both interactive and device code authentication
            _publicClientApp = PublicClientApplicationBuilder
                .Create(AzureAdConfiguration.ClientId)
                .WithAuthority(new Uri(AzureAdConfiguration.Authority))
                .WithRedirectUri(AzureAdConfiguration.RedirectUri)
                .Build();

            _logger.LogInformation(Messages.AzureAdClientInitializedSuccessfully);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Azure AD client applications");
            throw;
        }
    }

    /// <summary>
    /// Authenticate using interactive browser flow
    /// </summary>
    public async Task<string> AuthenticateInteractiveAsync()
    {
        try
        {
            if (_publicClientApp == null)
            {
                return Messages.PublicClientNotInitialized;
            }

            _logger.LogInformation(Messages.StartingInteractiveAuthentication);

            var result = await _publicClientApp
                .AcquireTokenInteractive(AzureAdConfiguration.PowerBIScopes)
                .ExecuteAsync();

            _currentAuth = McpAuthenticationResult.Success(
                result.AccessToken,
                result.Account.Username,
                result.TenantId,
                result.ExpiresOn.DateTime,
                string.Join(", ", result.Scopes)
            );

            _logger.LogInformation("Interactive authentication completed successfully for user: {Username}", result.Account.Username);
            return string.Format(Messages.InteractiveAuthenticationSuccessTemplate, result.Account.Username);
        }
        catch (MsalException msalEx)
        {
            _logger.LogError(msalEx, "MSAL authentication failed");
            _currentAuth = null;
            return string.Format(Messages.AuthenticationFailedTemplate, msalEx.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Interactive authentication failed");
            _currentAuth = null;
            return string.Format(Messages.AuthenticationErrorTemplate, ex.Message);
        }
    }

    /// <summary>
    /// Start device code authentication - returns device code and URL immediately
    /// </summary>
    public async Task<string> StartDeviceCodeAuthAsync()
    {
        try
        {
            if (_publicClientApp == null)
            {
                return Messages.PublicClientNotInitialized;
            }

            if (_pendingDeviceAuth != null && !_pendingDeviceAuth.IsCompleted)
            {
                var timeRemaining = _deviceAuthStartTime.HasValue
                    ? _deviceAuthTimeout - (DateTime.UtcNow - _deviceAuthStartTime.Value)
                    : TimeSpan.Zero;

                if (timeRemaining > TimeSpan.Zero)
                {
                    return $"Device authentication already in progress.\n\n{_pendingDeviceInstructions}\n\n⏱️ Time remaining: {timeRemaining.Minutes} minutes";
                }
                else
                {
                    // Timeout reached, clean up
                    _logger.LogWarning("Device authentication timed out, cleaning up");
                    _pendingDeviceAuth = null;
                    _pendingDeviceInstructions = null;
                    _deviceAuthStartTime = null;
                }
            }

            _logger.LogInformation("Starting device code authentication");
            _deviceAuthStartTime = DateTime.UtcNow;

            string deviceInstructions = string.Empty;
            var taskCompletionSource = new TaskCompletionSource<string>();

            // Start the device code flow with timeout
            var cancellationTokenSource = new CancellationTokenSource(_deviceAuthTimeout);

            // Start the device code flow but don't await it
            _pendingDeviceAuth = _publicClientApp
                .AcquireTokenWithDeviceCode(AzureAdConfiguration.PowerBIScopes, callback =>
                {
                    deviceInstructions = $@"🔐 **Device Code Authentication Started**

**Step 1:** Open your web browser and go to:
👉 {callback.VerificationUrl}

**Step 2:** Enter this device code:
📋 **{callback.UserCode}**

**Step 3:** Complete the sign-in process

⏳ **Use 'check_device_auth_status' tool to check completion status**

You have {callback.ExpiresOn.Subtract(DateTimeOffset.Now).Minutes} minutes to complete this.";

                    _pendingDeviceInstructions = deviceInstructions;
                    _logger.LogInformation("Device code: {UserCode} | URL: {VerificationUrl}",
                        callback.UserCode, callback.VerificationUrl);

                    // Signal that instructions are ready
                    taskCompletionSource.SetResult(deviceInstructions);
                    return Task.FromResult(0);
                })
                .ExecuteAsync(cancellationTokenSource.Token);

            // Wait for the callback to provide the instructions
            return await taskCompletionSource.Task;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Device code authentication timed out");
            _pendingDeviceAuth = null;
            _pendingDeviceInstructions = null;
            _deviceAuthStartTime = null;
            return "⏰ Device code authentication timed out. Please try again with 'start_device_code_auth'.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start device code authentication");
            _pendingDeviceAuth = null;
            _pendingDeviceInstructions = null;
            _deviceAuthStartTime = null;
            return string.Format(Messages.AuthenticationErrorTemplate, ex.Message);
        }
    }

    /// <summary>
    /// Check the status of pending device code authentication
    /// </summary>
    public async Task<string> CheckDeviceAuthStatusAsync()
    {
        try
        {
            if (_pendingDeviceAuth == null)
            {
                return "No device authentication in progress. Use 'start_device_code_auth' first.";
            }

            if (!_pendingDeviceAuth.IsCompleted)
            {
                var timeRemaining = _deviceAuthStartTime.HasValue
                    ? _deviceAuthTimeout - (DateTime.UtcNow - _deviceAuthStartTime.Value)
                    : TimeSpan.Zero;

                return $"⏳ Device authentication still pending...\n\n{_pendingDeviceInstructions}\n\n⏱️ Time remaining: {Math.Max(0, timeRemaining.Minutes)} minutes";
            }

            // Authentication completed, get the result
            var result = await _pendingDeviceAuth;

            _currentAuth = McpAuthenticationResult.Success(
                result.AccessToken,
                result.Account.Username,
                result.TenantId,
                result.ExpiresOn.DateTime,
                string.Join(", ", result.Scopes)
            );

            _logger.LogInformation("Device code authentication completed successfully for user: {Username}", result.Account.Username);

            // Clear pending auth
            _pendingDeviceAuth = null;
            _pendingDeviceInstructions = null;
            _deviceAuthStartTime = null;

            return $@"✅ **Authentication Successful!**
Signed in as: {result.Account.Username}
Tenant: {result.TenantId}";
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Device code authentication was cancelled or timed out");
            _currentAuth = null;
            _pendingDeviceAuth = null;
            _pendingDeviceInstructions = null;
            _deviceAuthStartTime = null;
            return "⏰ Device code authentication timed out or was cancelled. Please start a new authentication.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Device code authentication failed");
            _currentAuth = null;
            _pendingDeviceAuth = null;
            _pendingDeviceInstructions = null;
            _deviceAuthStartTime = null;
            return string.Format(Messages.AuthenticationFailedTemplate, ex.Message);
        }
    }

    public async Task<string> AuthenticateServicePrincipalAsync(string applicationId, string clientSecret, string? tenantId = null)
    {
        try
        {
            _logger.LogInformation("Starting service principal authentication for app: {ApplicationId}", applicationId);

            // Create a confidential client application for this specific authentication
            var authority = $"https://login.microsoftonline.com/{tenantId ?? AzureAdConfiguration.TenantId}";

            var confidentialClient = ConfidentialClientApplicationBuilder
                .Create(applicationId)
                .WithClientSecret(clientSecret)
                .WithAuthority(authority)
                .Build();

            var result = await confidentialClient
                .AcquireTokenForClient(AzureAdConfiguration.PowerBIScopes)
                .ExecuteAsync();

            _currentAuth = McpAuthenticationResult.Success(
                result.AccessToken,
                $"ServicePrincipal-{applicationId}",
                tenantId ?? AzureAdConfiguration.TenantId,
                result.ExpiresOn.DateTime,
                string.Join(", ", result.Scopes)
            );

            _logger.LogInformation("Service principal authentication completed successfully for app: {ApplicationId}", applicationId);
            return string.Format(Messages.ServicePrincipalAuthenticationSuccessTemplate, applicationId);
        }
        catch (MsalException msalEx)
        {
            _logger.LogError(msalEx, "MSAL service principal authentication failed for app: {ApplicationId}", applicationId);
            _currentAuth = null;
            return string.Format(Messages.ServicePrincipalAuthenticationFailedTemplate, msalEx.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service principal authentication failed for app: {ApplicationId}", applicationId);
            _currentAuth = null;
            return string.Format(Messages.AuthenticationErrorTemplate, ex.Message);
        }
    }

    public string GetAuthenticationStatus()
    {
        if (_currentAuth == null || !_currentAuth.IsSuccess)
        {
            return Messages.NotAuthenticated;
        }

        var status = $"""
            Authentication Status: Authenticated
            User: {_currentAuth.UserName}
            Tenant: {_currentAuth.TenantId}
            Token Expires: {_currentAuth.ExpiresOn:yyyy-MM-dd HH:mm:ss} UTC
            Scopes: {_currentAuth.Scopes}
            """;

        return status;
    }

    public async Task<string> SignOutAsync()
    {
        try
        {
            _logger.LogInformation(Messages.SigningOutCurrentUser);

            if (_currentAuth == null)
            {
                return Messages.NoActiveAuthenticationSession;
            }

            var userName = _currentAuth.UserName;

            // Clear cached tokens if using interactive authentication
            if (_publicClientApp != null && !userName?.StartsWith("ServicePrincipal-") == true)
            {
                var accounts = await _publicClientApp.GetAccountsAsync();
                foreach (var account in accounts)
                {
                    await _publicClientApp.RemoveAsync(account);
                }
            }

            _currentAuth = null;
            _logger.LogInformation("Successfully signed out user: {UserName}", userName);
            return string.Format(Messages.SignOutSuccessTemplate, userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sign out");
            return string.Format(Messages.SignOutErrorTemplate, ex.Message);
        }
    }

    public async Task<string> GetAccessTokenAsync()
    {
        try
        {
            if (_currentAuth == null || !_currentAuth.IsSuccess)
            {
                return Messages.NoAuthenticationFound;
            }

            if (_currentAuth.ExpiresOn.HasValue && _currentAuth.ExpiresOn <= DateTime.UtcNow)
            {
                // Try to refresh the token silently
                if (_publicClientApp != null && !_currentAuth.UserName?.StartsWith("ServicePrincipal-") == true)
                {
                    var accounts = await _publicClientApp.GetAccountsAsync();
                    if (accounts.Any())
                    {
                        try
                        {
                            var result = await _publicClientApp
                                .AcquireTokenSilent(AzureAdConfiguration.PowerBIScopes, accounts.First())
                                .ExecuteAsync();

                            _currentAuth = McpAuthenticationResult.Success(
                                result.AccessToken,
                                result.Account.Username,
                                result.TenantId,
                                result.ExpiresOn.DateTime,
                                string.Join(", ", result.Scopes)
                            );

                            return _currentAuth.AccessToken ?? Messages.TokenNotAvailable;
                        }
                        catch (MsalUiRequiredException)
                        {
                            return Messages.AccessTokenExpiredCannotRefresh;
                        }
                    }
                }

                return Messages.AccessTokenExpired;
            }

            return _currentAuth.AccessToken ?? Messages.TokenNotAvailable;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving access token");
            return string.Format(Messages.ErrorRetrievingAccessTokenTemplate, ex.Message);
        }
    }

    public async Task<string> GetAccessTokenAsync(string[] scopes)
    {
        try
        {
            if (_publicClientApp == null)
            {
                return Messages.PublicClientNotInitialized;
            }

            _logger.LogInformation("Acquiring token for scopes: {Scopes}", string.Join(", ", scopes));

            // Try to get token silently first if we have an authenticated account
            var accounts = await _publicClientApp.GetAccountsAsync();
            if (accounts.Any())
            {
                try
                {
                    var result = await _publicClientApp
                        .AcquireTokenSilent(scopes, accounts.First())
                        .ExecuteAsync();

                    _logger.LogInformation("Token acquired silently for scopes: {Scopes}", string.Join(", ", result.Scopes));
                    return result.AccessToken;
                }
                catch (MsalUiRequiredException)
                {
                    _logger.LogInformation("Silent token acquisition failed, falling back to interactive");
                }
            }

            // If silent acquisition fails, try interactive
            var interactiveResult = await _publicClientApp
                .AcquireTokenInteractive(scopes)
                .ExecuteAsync();

            _logger.LogInformation("Token acquired interactively for scopes: {Scopes}", string.Join(", ", interactiveResult.Scopes));
            return interactiveResult.AccessToken;
        }
        catch (MsalException msalEx)
        {
            _logger.LogError(msalEx, "MSAL token acquisition failed for scopes: {Scopes}", string.Join(", ", scopes));
            return string.Format(Messages.AuthenticationFailedTemplate, msalEx.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token acquisition failed for scopes: {Scopes}", string.Join(", ", scopes));
            return string.Format(Messages.ErrorRetrievingAccessTokenTemplate, ex.Message);
        }
    }
}
