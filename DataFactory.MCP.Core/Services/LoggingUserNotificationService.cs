using DataFactory.MCP.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

namespace DataFactory.MCP.Services;

/// <summary>
/// User notification service that logs notifications instead of showing system toasts.
/// Suitable for HTTP/remote scenarios where OS notifications aren't possible.
/// </summary>
public class LoggingUserNotificationService : IUserNotificationService
{
    private readonly ILogger<LoggingUserNotificationService> _logger;

    public LoggingUserNotificationService(ILogger<LoggingUserNotificationService> logger)
    {
        _logger = logger;
    }

    public Task NotifyAsync(string title, string message, NotificationLevel level = NotificationLevel.Info)
    {
        var logLevel = level switch
        {
            NotificationLevel.Error => LogLevel.Error,
            NotificationLevel.Warning => LogLevel.Warning,
            NotificationLevel.Success => LogLevel.Information,
            _ => LogLevel.Information
        };

        _logger.Log(logLevel, "[User Notification] {Title}: {Message}", title, message);

        return Task.CompletedTask;
    }
}
