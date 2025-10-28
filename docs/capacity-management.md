# Capacity Management Guide

This guide covers how to use the Microsoft Data Factory MCP Server for managing Microsoft Fabric capacities.

## Overview

The capacity management tools allow you to:
- List all accessible capacities with detailed information
- View capacity SKUs, regions, and status information
- Work with different capacity types (Free Trial, Premium, etc.)
- Navigate paginated results for large capacity collections

## Available Operations

### List Capacities

Retrieve a list of all capacities you have access to with full details including SKU information, regional distribution, and status.

#### Usage
```
list_capacities
```

#### With Pagination
```
list_capacities(continuationToken: "next-page-token")
```

#### Response Format
```json
{
  "totalCount": 209,
  "continuationToken": null,
  "hasMoreResults": false,
  "formattedResults": {
    "capacities": [
      {
        "id": "12345678-1234-1234-1234-123456789012",
        "displayName": "Premium Per User - Reserved",
        "sku": "PP3",
        "skuDescription": "Fabric PP3",
        "region": "West Central US",
        "state": "Active",
        "status": "Ready to use",
        "isActive": true
      },
      {
        "id": "87654321-4321-4321-4321-210987654321",
        "displayName": "Trial-20251027T120000Z-TestCapacity",
        "sku": "FT1",
        "skuDescription": "Free Trial (FT1)",
        "region": "West Central US",
        "state": "Active",
        "status": "Ready to use",
        "isActive": true
      }
    ],
    "summary": {
      "totalCount": 209,
      "byState": {
        "Active": 209
      },
      "bySku": {
        "FT1": 207,
        "PP3": 1,
        "FTL64": 1
      },
      "byRegion": {
        "West Central US": 209
      },
      "activeCount": 209,
      "inactiveCount": 0
    }
  }
}
```

## Usage Examples

### Basic Capacity Operations
```
# List all accessible capacities
> show me all my fabric capacities

# View capacity summary information
> what capacities do I have access to?

# List capacities with pagination
> list my capacities with continuation token abc123
```

### Capacity Analysis
```
# Analyze capacity distribution
> show me my capacity summary by SKU and region

# Check capacity status
> what's the status of my capacities?

# Find specific capacity types
> show me all my trial capacities
```
