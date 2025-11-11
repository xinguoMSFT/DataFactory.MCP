using DataFactory.MCP.Models;
using DataFactory.MCP.Models.Connection;
using DataFactory.MCP.Models.Dataflow;

namespace DataFactory.MCP.Extensions;

/// <summary>
/// Extension methods for creating standardized MCP tool responses
/// </summary>
public static class ResponseExtensions
{
    /// <summary>
    /// Creates a successful response with data
    /// </summary>
    public static object ToSuccessResponse(this object data, string? message = null)
    {
        return new
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    /// <summary>
    /// Creates an error response for failed query execution
    /// </summary>
    public static object ToQueryExecutionError(this ExecuteDataflowQueryResponse response, string workspaceId, string dataflowId, string queryName)
    {
        return new
        {
            Success = false,
            Error = response.Error,
            Message = $"Failed to execute query '{queryName}' on dataflow {dataflowId}",
            WorkspaceId = workspaceId,
            DataflowId = dataflowId,
            QueryName = queryName
        };
    }

    /// <summary>
    /// Converts a Connection to a successful creation response with connection details
    /// </summary>
    public static object ToCreationSuccessResponse(this Connection connection, string message)
    {
        return new
        {
            Success = true,
            Message = message,
            Connection = new
            {
                Id = connection.Id,
                DisplayName = connection.DisplayName,
                ConnectivityType = connection.ConnectivityType.ToString(),
                ConnectionType = connection.ConnectionDetails.Type,
                Path = connection.ConnectionDetails.Path,
                PrivacyLevel = connection.PrivacyLevel?.ToString()
            }
        };
    }

    /// <summary>
    /// Converts an UnauthorizedAccessException to a standardized authentication error response
    /// </summary>
    public static object ToAuthenticationError(this UnauthorizedAccessException ex)
    {
        return new
        {
            Success = false,
            Error = "AuthenticationError",
            Message = string.Format(Messages.AuthenticationErrorTemplate, ex.Message)
        };
    }

    /// <summary>
    /// Converts an HttpRequestException to a standardized HTTP error response
    /// </summary>
    public static object ToHttpError(this HttpRequestException ex)
    {
        return new
        {
            Success = false,
            Error = "HttpRequestError",
            Message = string.Format(Messages.ApiRequestFailedTemplate, ex.Message)
        };
    }

    /// <summary>
    /// Converts an ArgumentException to a standardized validation error response
    /// </summary>
    public static object ToValidationError(this ArgumentException ex)
    {
        return new
        {
            Success = false,
            Error = "ValidationError",
            Message = $"Validation failed: {ex.Message}"
        };
    }

    /// <summary>
    /// Converts a general Exception to a standardized operation error response
    /// </summary>
    public static object ToOperationError(this Exception ex, string operation)
    {
        return new
        {
            Success = false,
            Error = "OperationError",
            Message = $"Error {operation}: {ex.Message}",
            Operation = operation
        };
    }

    /// <summary>
    /// Creates a validation error response from a message
    /// </summary>
    public static object ToValidationError(string message)
    {
        return new
        {
            Success = false,
            Error = "ValidationError",
            Message = $"Validation failed: {message}"
        };
    }

    /// <summary>
    /// Creates a resource not found error response
    /// </summary>
    public static object ToNotFoundError(string resourceType, string resourceId)
    {
        return new
        {
            Success = false,
            Error = "NotFoundError",
            Message = $"{resourceType} with ID '{resourceId}' was not found",
            ResourceType = resourceType,
            ResourceId = resourceId
        };
    }

    /// <summary>
    /// Creates a forbidden access error response
    /// </summary>
    public static object ToForbiddenError(string message)
    {
        return new
        {
            Success = false,
            Error = "ForbiddenError",
            Message = $"Access denied: {message}"
        };
    }

    /// <summary>
    /// Creates a generic error response
    /// </summary>
    public static object ToGenericError(string message)
    {
        return new
        {
            Success = false,
            Error = "ExecutionError",
            Message = $"Error executing dataflow query: {message}"
        };
    }

    /// <summary>
    /// Creates a connection operation error response based on exception type with smart dispatch
    /// </summary>
    public static object ToConnectionError(this Exception ex, string operation)
    {
        return ex switch
        {
            UnauthorizedAccessException uae => uae.ToAuthenticationError(),
            HttpRequestException hre => hre.ToHttpError(),
            ArgumentException ae => ae.ToValidationError(),
            _ => ex.ToOperationError(operation)
        };
    }
}