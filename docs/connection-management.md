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
  "totalCount": 112,
  "continuationToken": null,
  "hasMoreResults": false,
  "connections": [
    {
      "id": "a0b9fa12-60f5-4f95-85ca-565d34abcea1",
      "displayName": "Example Cloud Data Source",
      "connectivityType": "OnPremisesGateway",
      "connectionDetails": {
        "type": "Web",
        "path": "http://www.microsoft.com/"
      },
      "privacyLevel": "Organizational",
      "credentialDetails": {
        "credentialType": "Anonymous",
        "singleSignOnType": "None",
        "connectionEncryption": "Any",
        "skipTestConnection": false
      },
      "gatewayId": "7d3b5733-732d-4bbe-8d17-db6f6fe5d19c"
    }
  ]
}
```

### Get Connection Details

Retrieve detailed information about a specific connection.

#### Usage
```
get_connection(connectionId: "a0b9fa12-60f5-4f95-85ca-565d34abcea1")
```

#### Response Format
```json
{
  "id": "a0b9fa12-60f5-4f95-85ca-565d34abcea1",
  "displayName": "Example Cloud Data Source",
  "connectivityType": "OnPremisesGateway",
  "connectionDetails": {
    "type": "Web",
    "path": "http://www.microsoft.com/"
  },
  "privacyLevel": "Organizational",
  "credentialDetails": {
    "credentialType": "Anonymous",
    "singleSignOnType": "None",
    "connectionEncryption": "Any",
    "skipTestConnection": false
  },
  "gatewayId": "7d3b5733-732d-4bbe-8d17-db6f6fe5d19c"
}
```

### Create Cloud SQL Connection

Create a new cloud SQL connection with basic authentication.

#### Usage
```
create_cloud_sql_basic(
  displayName: "My SQL Connection",
  serverName: "server.database.windows.net", 
  databaseName: "MyDatabase",
  username: "myuser",
  password: "mypassword"
)
```

#### Parameters
- `displayName`: Name for the connection
- `serverName`: SQL Server hostname
- `databaseName`: Database name
- `username`: SQL authentication username
- `password`: SQL authentication password

### Create VNet SQL Connection  

Create a new VNet gateway SQL connection with basic authentication.

#### Usage
```
create_v_net_sql_basic(
  displayName: "My VNet SQL Connection",
  gatewayId: "7d3b5733-732d-4bbe-8d17-db6f6fe5d19c",
  serverName: "internal-server.local",
  databaseName: "MyDatabase", 
  username: "myuser",
  password: "mypassword"
)
```

#### Parameters
- `displayName`: Name for the connection
- `gatewayId`: Virtual network gateway ID (UUID) or gateway name
- `serverName`: SQL Server hostname (can be internal/private)
- `databaseName`: Database name
- `username`: SQL authentication username
- `password`: SQL authentication password

## Usage Examples

### Basic Connection Operations
```
# List all available connections
> show me all my data factory connections

# Get specific connection details by ID
> get details for connection with ID a0b9fa12-60f5-4f95-85ca-565d34abcea1

# Get specific connection details by name
> get details for connection with name Example Cloud Data Source

# Create cloud SQL connection
> create_cloud_sql_basic displayName="My SQL DB" serverName="server.database.windows.net" databaseName="MyDB" username="myuser" password="mypass"

# Create VNet SQL connection using gateway ID
> create_v_net_sql_basic displayName="Internal SQL" gatewayId="7d3b5733-732d-4bbe-8d17-db6f6fe5d19c" serverName="internal-sql" databaseName="MyDB" username="myuser" password="mypass"

# Create VNet SQL connection using gateway name
> create_v_net_sql_basic displayName="Internal SQL" gatewayId="My VNet Gateway" serverName="internal-sql" databaseName="MyDB" username="myuser" password="mypass"
```