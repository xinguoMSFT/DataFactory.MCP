# Connection Management Guide

This guide covers how to use the Microsoft Data Factory MCP Server for managing Azure Data Factory and Microsoft Fabric connections.

## Overview

The connection management tools allow you to:
- List all accessible connections across different types
- Retrieve detailed information about specific connections
- Work with on-premises, virtual network, and cloud connections

## Available Operations

### List Connections

Retrieve a list of all connections you have access to.

#### Usage
```
list_connections
```

#### With Pagination
```
list_connections(continuationToken: "next-page-token")
```

#### Response Format
```json
{
  "totalCount": 3,
  "continuationToken": "eyJza2lwIjoyMCwidGFrZSI6MjB9",
  "hasMoreResults": true,
  "connections": [
    {
      "id": "12345678-1234-1234-1234-123456789012",
      "displayName": "SQL Server Production",
      "connectivityType": "OnPremisesGateway",
      "connectionDetails": {
        "type": "SqlServer",
        "path": "server01.company.com"
      },
      "privacyLevel": "Organizational",
      "credentialDetails": {
        "credentialType": "Windows",
        "singleSignOnType": "None",
        "connectionEncryption": "Encrypted",
        "skipTestConnection": false
      },
      "gatewayId": "87654321-4321-4321-4321-210987654321"
    }
  ]
}
```

### Get Connection Details

Retrieve detailed information about a specific connection.

#### Usage
```
get_connection(connectionId: "12345678-1234-1234-1234-123456789012")
```

#### Response Format
```json
{
  "id": "12345678-1234-1234-1234-123456789012",
  "displayName": "SQL Server Production",
  "connectivityType": "OnPremisesGateway",
  "connectionDetails": {
    "type": "SqlServer",
    "path": "server01.company.com"
  },
  "privacyLevel": "Organizational",
  "credentialDetails": {
    "credentialType": "Windows",
    "singleSignOnType": "None",
    "connectionEncryption": "Encrypted",
    "skipTestConnection": false
  },
  "gatewayId": "87654321-4321-4321-4321-210987654321"
}
```

## Usage Examples

### Basic Connection Operations
```
# List all available connections
> show me all my data factory connections

# Get specific connection details by ID
> get details for connection with ID 12345678-1234-1234-1234-123456789012

# Get specific connection details by name
> get details for connection with name SQL Server Production
```