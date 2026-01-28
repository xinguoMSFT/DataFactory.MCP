namespace DataFactory.MCP.Abstractions.Interfaces;

/// <summary>
/// Monitors all active background jobs using a single polling loop.
/// More efficient than spawning a Task per job.
/// </summary>
public interface IBackgroundJobMonitor
{
    /// <summary>
    /// Registers a job for monitoring.
    /// The monitor will periodically check the job's status until completion.
    /// </summary>
    /// <param name="job">The job to monitor</param>
    void RegisterJob(IBackgroundJob job);

    /// <summary>
    /// Gets whether there are any active jobs being monitored.
    /// </summary>
    bool HasActiveJobs { get; }

    /// <summary>
    /// Gets the count of active jobs being monitored.
    /// </summary>
    int ActiveJobCount { get; }
}
