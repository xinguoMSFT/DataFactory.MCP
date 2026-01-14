namespace DataFactory.MCP.Configuration;

/// <summary>
/// Constants for feature flag names used throughout the application
/// </summary>
public static class FeatureFlags
{
    /// <summary>
    /// Feature flag for enabling the DataflowQueryTool
    /// Command line: --dataflow-query
    /// </summary>
    public const string DataflowQuery = "dataflow-query";

    /// <summary>
    /// Feature flag for enabling the DeviceCodeAuthenticationTool
    /// Command line: --device-code-auth
    /// Only enabled for HTTP version
    /// </summary>
    public const string DeviceCodeAuth = "device-code-auth";
}