using DataFactory.MCP.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace DataFactory.MCP.Services.BackgroundTasks;

/// <summary>
/// Executes background jobs and registers them with the central monitor.
/// Single Responsibility: job startup and registration only.
/// Monitoring is delegated to IBackgroundJobMonitor for efficiency.
/// </summary>
public class BackgroundJobRunner : IBackgroundJobRunner
{
    private readonly IMcpSessionAccessor _sessionAccessor;
    private readonly IBackgroundTaskTracker _taskTracker;
    private readonly IBackgroundJobMonitor _jobMonitor;
    private readonly INotificationQueue _notificationQueue;
    private readonly ILogger<BackgroundJobRunner> _logger;

    public BackgroundJobRunner(
        IMcpSessionAccessor sessionAccessor,
        IBackgroundTaskTracker taskTracker,
        IBackgroundJobMonitor jobMonitor,
        INotificationQueue notificationQueue,
        ILogger<BackgroundJobRunner> logger)
    {
        _sessionAccessor = sessionAccessor;
        _taskTracker = taskTracker;
        _jobMonitor = jobMonitor;
        _notificationQueue = notificationQueue;
        _logger = logger;
    }

    public async Task<BackgroundJobResult> RunAsync(IBackgroundJob job, McpSession session)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(session);

        // Store session for notifications
        _sessionAccessor.CurrentSession = session;

        _logger.LogInformation("Starting background job {JobType}: {DisplayName} (ID: {JobId})",
            job.JobType, job.DisplayName, job.JobId);

        // Start the job
        var startResult = await job.StartAsync();

        if (startResult.IsComplete)
        {
            // Job completed immediately (or failed to start)
            EnqueueNotification(job, startResult);
            return startResult;
        }

        // Track the job
        _taskTracker.Track(new TrackedTask
        {
            TaskId = job.JobId,
            JobType = job.JobType,
            DisplayName = job.DisplayName,
            Status = startResult.Status,
            StartedAt = startResult.StartedAt,
            Context = startResult.Context
        });

        // Register with central monitor (single timer polls all jobs efficiently)
        _jobMonitor.RegisterJob(job);

        _logger.LogDebug("Job {JobId} registered for monitoring. Active jobs: {Count}",
            job.JobId, _jobMonitor.ActiveJobCount);

        return startResult;
    }

    private void EnqueueNotification(IBackgroundJob job, BackgroundJobResult result)
    {
        var title = $"{job.JobType} {result.Status}";
        var duration = result.DurationFormatted ?? "unknown duration";

        QueuedNotification notification;

        if (result.IsSuccess)
        {
            notification = new QueuedNotification
            {
                Title = title,
                Message = $"'{job.DisplayName}' completed successfully in {duration}",
                Level = NotificationLevel.Success
            };
        }
        else if (result.Status == "Timeout")
        {
            notification = new QueuedNotification
            {
                Title = title,
                Message = $"'{job.DisplayName}' timed out",
                Level = NotificationLevel.Warning
            };
        }
        else
        {
            notification = new QueuedNotification
            {
                Title = title,
                Message = $"'{job.DisplayName}' failed: {result.ErrorMessage ?? "Unknown error"}",
                Level = NotificationLevel.Error
            };
        }

        _notificationQueue.Enqueue(notification);
    }
}
