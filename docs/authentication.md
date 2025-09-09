# Authentication Guide

The Microsoft Data Factory MCP Server supports multiple authentication methods for Azure AD integration.

## Overview

Authentication is required to access Microsoft Fabric and Azure Data Factory services. The server supports two primary authentication flows:

1. **Interactive Authentication** - User-based authentication with browser flow
2. **Service Principal Authentication** - Application-based authentication with client credentials

## Interactive Authentication

Interactive authentication uses the device code flow or browser-based authentication to obtain user credentials.

### Usage

```
authenticate_interactive
```

This method will:
1. Open a browser window or provide a device code
2. Prompt you to sign in with your Azure AD credentials
3. Store the authentication token for subsequent requests

### Requirements

- Valid Azure AD user account
- Access to a web browser (for browser flow)
- Appropriate permissions to access Data Factory resources

## Service Principal Authentication

Service principal authentication uses application credentials (client ID and client secret) for automated scenarios.

### Setup

1. **Create an Azure AD Application**:
   - Navigate to Azure Portal > Azure Active Directory > App registrations
   - Click "New registration"
   - Provide a name and configure settings
   - Note the Application (client) ID

2. **Create a Client Secret**:
   - In your app registration, go to "Certificates & secrets"
   - Click "New client secret"
   - Copy the secret value (it won't be shown again)

3. **Grant Permissions**:
   - In your app registration, go to "API permissions"
   - Add the following Microsoft Graph permissions:
     - `Gateway.Read.All` (for read-only access)
     - `Gateway.ReadWrite.All` (for full access)
   - Grant admin consent for the permissions

### Usage

```
authenticate_service_principal(
    applicationId: "your-app-client-id",
    clientSecret: "your-client-secret",
    tenantId: "your-tenant-id"  // optional
)
```

### Parameters

- `applicationId` (required): The Application (client) ID from your Azure AD app registration
- `clientSecret` (required): The client secret value
- `tenantId` (optional): The Azure AD tenant ID. If not provided, will use the default tenant

## Authentication Status and Management

### Check Authentication Status

```
get_authentication_status
```

Returns information about the current authentication state, including:
- Whether the user is authenticated
- Authentication method used
- Token expiration information
- User/application details

### Get Access Token

```
get_access_token
```

Retrieves the current access token for making authenticated requests. Useful for debugging or manual API calls.

### Sign Out

```
sign_out
```

Clears the current authentication state and removes stored tokens.

## Usage Examples

### Interactive Authentication
```
# Simple interactive login
> authenticate with Azure AD

# The system will open a browser or provide a device code for authentication
```

### Service Principal Authentication
```
# Authenticate with service principal
> authenticate using service principal with client ID abc123 and secret xyz789

# With explicit tenant ID
> authenticate using service principal with client ID abc123, secret xyz789, and tenant def456
```

### Managing Authentication
```
# Check current authentication status
> get authentication status

# Get access token for debugging
> get access token

# Sign out
> sign out
```
