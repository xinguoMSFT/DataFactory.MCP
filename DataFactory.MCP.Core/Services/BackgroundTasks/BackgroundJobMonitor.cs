using System.Collections.Concurrent;
using DataFactory.MCP.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

namespace DataFactory.MCP.Services.BackgroundTasks;

/// <summary>
/// Monitors all active background jobs using a single timer-based polling loop.
/// More efficient than spawning a Task per job.
/// Thread-safe for concurrent job registration.
/// </summary>
public class BackgroundJobMonitor : IBackgroundJobMonitor, IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MaxJobAge = TimeSpan.FromHours(4);

    private readonly ConcurrentDictionary<string, MonitoredJob> _activeJobs = new();
    private readonly IBackgroundTaskTracker _taskTracker;
    private readonly INotificationQueue _notificationQueue;
    private readonly ILogger<BackgroundJobMonitor> _logger;
    private readonly Timer _pollTimer;
    private readonly SemaphoreSlim _pollLock = new(1, 1);
    private bool _disposed;

    public BackgroundJobMonitor(
        IBackgroundTaskTracker taskTracker,
        INotificationQueue notificationQueue,
        ILogger<BackgroundJobMonitor> logger)
    {
        _taskTracker = taskTracker;
        _notificationQueue = notificationQueue;
        _logger = logger;

        // Start timer but don't poll until we have jobs
        _pollTimer = new Timer(OnPollTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);

        _logger.LogDebug("BackgroundJobMonitor initialized with {Interval}s poll interval",
            PollInterval.TotalSeconds);
    }

    public void RegisterJob(IBackgroundJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        var monitoredJob = new MonitoredJob
        {
            Job = job,
            RegisteredAt = DateTime.UtcNow
        };

        if (_activeJobs.TryAdd(job.JobId, monitoredJob))
        {
            _logger.LogInformation("Registered job for monitoring: {JobType} '{DisplayName}' (ID: {JobId})",
                job.JobType, job.DisplayName, job.JobId);

            // Start polling if this is the first job
            if (_activeJobs.Count == 1)
            {
                StartPolling();
            }
        }
        else
        {
            _logger.LogWarning("Job {JobId} is already being monitored", job.JobId);
        }
    }

    public bool HasActiveJobs => !_activeJobs.IsEmpty;

    public int ActiveJobCount => _activeJobs.Count;

    private void StartPolling()
    {
        _logger.LogDebug("Starting poll timer");
        _pollTimer.Change(PollInterval, PollInterval);
    }

    private void StopPolling()
    {
        _logger.LogDebug("Stopping poll timer (no active jobs)");
        _pollTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private async void OnPollTimerElapsed(object? state)
    {
        if (_disposed) return;

        // Prevent concurrent poll executions
        if (!await _pollLock.WaitAsync(0))
        {
            _logger.LogDebug("Poll already in progress, skipping");
            return;
        }

        try
        {
            await PollAllJobsAsync();
        }
        finally
        {
            _pollLock.Release();
        }
    }

    private async Task PollAllJobsAsync()
    {
        if (_activeJobs.IsEmpty)
        {
            StopPolling();
            return;
        }

        _logger.LogDebug("Polling {Count} active job(s)", _activeJobs.Count);

        var completedJobs = new List<string>();
        var now = DateTime.UtcNow;

        // Check all jobs in parallel
        var checkTasks = _activeJobs.Values.Select(async monitoredJob =>
        {
            try
            {
                // Check for timeout
                if (now - monitoredJob.RegisteredAt > MaxJobAge)
                {
                    _logger.LogWarning("Job {JobId} timed out after {Duration}",
                        monitoredJob.Job.JobId, MaxJobAge);

                    HandleJobCompletion(monitoredJob.Job, new BackgroundJobResult
                    {
                        IsComplete = true,
                        IsSuccess = false,
                        Status = "Timeout",
                        ErrorMessage = $"Job did not complete within {MaxJobAge.TotalHours} hours",
                        StartedAt = monitoredJob.RegisteredAt,
                        CompletedAt = now
                    });

                    return monitoredJob.Job.JobId;
                }

                var result = await monitoredJob.Job.CheckStatusAsync();

                if (result.IsComplete)
                {
                    _logger.LogInformation("Job {JobId} completed with status {Status}",
                        monitoredJob.Job.JobId, result.Status);

                    HandleJobCompletion(monitoredJob.Job, result);
                    return monitoredJob.Job.JobId;
                }

                _logger.LogDebug("Job {JobId} still in progress: {Status}",
                    monitoredJob.Job.JobId, result.Status);

                return null; // Not completed
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking job {JobId}", monitoredJob.Job.JobId);

                HandleJobCompletion(monitoredJob.Job, new BackgroundJobResult
                {
                    IsComplete = true,
                    IsSuccess = false,
                    Status = "Error",
                    ErrorMessage = $"Monitoring failed: {ex.Message}",
                    StartedAt = monitoredJob.RegisteredAt,
                    CompletedAt = now
                });

                return monitoredJob.Job.JobId;
            }
        });

        var results = await Task.WhenAll(checkTasks);

        // Remove completed jobs
        foreach (var jobId in results.Where(id => id != null))
        {
            _activeJobs.TryRemove(jobId!, out _);
        }

        // Stop timer if no more jobs
        if (_activeJobs.IsEmpty)
        {
            StopPolling();
        }
    }

    private void HandleJobCompletion(IBackgroundJob job, BackgroundJobResult result)
    {
        // Update tracker
        _taskTracker.Update(job.JobId, task =>
        {
            task.Status = result.Status;
            task.CompletedAt = result.CompletedAt ?? DateTime.UtcNow;
            task.FailureReason = result.ErrorMessage;
        });

        // Queue notification
        var notification = CreateNotification(job, result);
        _notificationQueue.Enqueue(notification);
    }

    private static QueuedNotification CreateNotification(IBackgroundJob job, BackgroundJobResult result)
    {
        var title = $"{job.JobType} {result.Status}";
        var duration = result.DurationFormatted ?? "unknown duration";

        if (result.IsSuccess)
        {
            return new QueuedNotification
            {
                Title = title,
                Message = $"'{job.DisplayName}' completed successfully in {duration}",
                Level = NotificationLevel.Success
            };
        }
        else if (result.Status == "Timeout")
        {
            return new QueuedNotification
            {
                Title = title,
                Message = $"'{job.DisplayName}' timed out",
                Level = NotificationLevel.Warning
            };
        }
        else
        {
            return new QueuedNotification
            {
                Title = title,
                Message = $"'{job.DisplayName}' failed: {result.ErrorMessage ?? "Unknown error"}",
                Level = NotificationLevel.Error
            };
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _pollTimer.Dispose();
        _pollLock.Dispose();
    }

    private class MonitoredJob
    {
        public required IBackgroundJob Job { get; init; }
        public required DateTime RegisteredAt { get; init; }
    }
}
