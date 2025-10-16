using Microsoft.Extensions.Hosting;
using ProtWatchdog.Models;
using System.Diagnostics;

namespace ProtWatchdog.Services;

public class ProcessManagerService : BackgroundService
{
    private readonly ProcessRepository _repo;
    private readonly ILogger<ProcessManagerService> _logger;
    private readonly TimeSpan _monitorInterval = TimeSpan.FromSeconds(2);

    public ProcessManagerService(ProcessRepository repo, ILogger<ProcessManagerService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProcessManagerService starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var items = _repo.GetAll().ToList(); // Snapshot to avoid race conditions

                foreach (var mp in items)
                {
                    try
                    {
                        // Health check for running processes
                        if (mp.EnableHealthCheck && mp.RunningProcess != null && !mp.RunningProcess.HasExited)
                        {
                            PerformHealthCheck(mp);
                        }

                        bool needsRestart = false;

                        if (mp.RunningProcess == null)
                        {
                            // Check if we're in suppression period
                            if (mp.SuppressAutoRestartUntil.HasValue && DateTime.UtcNow < mp.SuppressAutoRestartUntil.Value)
                            {
                                // Don't auto-restart during manual operations
                                needsRestart = false;
                            }
                            else
                            {
                                // Only auto-start if AutoRestart is enabled
                                needsRestart = mp.AutoRestart;
                            }
                        }
                        else if (mp.RunningProcess.HasExited)
                        {
                            mp.LastExitCode = mp.RunningProcess.ExitCode;
                            _logger.LogWarning("Process {Name} exited with code {ExitCode}", mp.Name, mp.LastExitCode);

                            mp.RunningProcess.Dispose();
                            mp.RunningProcess = null;

                            // Check if we're in suppression period
                            if (mp.SuppressAutoRestartUntil.HasValue && DateTime.UtcNow < mp.SuppressAutoRestartUntil.Value)
                            {
                                _logger.LogInformation("Suppressing auto-restart for {Name} (manual operation in progress)", mp.Name);
                                needsRestart = false;
                            }
                            else
                            {
                                // Only auto-restart if AutoRestart is enabled
                                needsRestart = mp.AutoRestart;
                            }
                        }

                        if (needsRestart)
                        {
                            // Check circuit breaker before restarting
                            if (CheckCircuitBreaker(mp))
                            {
                                _logger.LogError("Circuit breaker tripped for {Name} - too many restarts ({Count} in {Minutes} minutes). Auto-restart disabled.",
                                    mp.Name, mp.RecentRestarts.Count, mp.RestartTimeWindowMinutes);
                                mp.CircuitBreakerTripped = true;
                                mp.AutoRestart = false;
                                _repo.Save();
                                continue; // Skip this process
                            }

                            // Wait restart delay before starting (but not on first start)
                            if (!mp.IsFirstStart)
                            {
                                _logger.LogInformation("Waiting {Delay}s before restarting {Name}", mp.RestartDelaySeconds, mp.Name);
                                await Task.Delay(TimeSpan.FromSeconds(mp.RestartDelaySeconds), stoppingToken);
                            }

                            StartProcess(mp);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error managing process {Name}", mp.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in monitor loop");
            }

            await Task.Delay(_monitorInterval, stoppingToken);
        }

        _logger.LogInformation("ProcessManagerService stopping - killing all managed processes");

        // Cleanup on shutdown
        var allProcesses = _repo.GetAll().ToList();
        foreach (var mp in allProcesses)
        {
            _repo.TryKill(mp);
        }
    }

    // Helper method to start a process
    private void StartProcess(ManagedProcess mp)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Environment.ExpandEnvironmentVariables(mp.ExecutablePath),
                Arguments = Environment.ExpandEnvironmentVariables(mp.Parameters),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            var p = Process.Start(startInfo);

            if (p != null)
            {
                mp.RunningProcess = p;
                mp.LastStart = DateTime.UtcNow;

                // Only increment restart count after first start
                if (!mp.IsFirstStart)
                {
                    mp.RestartCount++;

                    // Track restart time for circuit breaker
                    mp.RecentRestarts.Add(DateTime.UtcNow);

                    // Clean old restarts outside time window
                    var cutoff = DateTime.UtcNow.AddMinutes(-mp.RestartTimeWindowMinutes);
                    mp.RecentRestarts.RemoveAll(r => r < cutoff);
                }
                mp.IsFirstStart = false;

                _logger.LogInformation("Started {Name} (PID: {Pid}, RestartCount: {Count}, RecentRestarts: {Recent})",
                    mp.Name, p.Id, mp.RestartCount, mp.RecentRestarts.Count);
            }
            else
            {
                _logger.LogError("Failed to start {Name}: Process.Start returned null", mp.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start process {Name}", mp.Name);
        }
    }

    // Check if circuit breaker should trip
    private bool CheckCircuitBreaker(ManagedProcess mp)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-mp.RestartTimeWindowMinutes);
        var recentRestartCount = mp.RecentRestarts.Count(r => r > cutoff);

        return recentRestartCount >= mp.MaxRestartAttempts;
    }

    // Perform health check on running process
    private void PerformHealthCheck(ManagedProcess mp)
    {
        try
        {
            // Check if it's time for health check
            if (mp.LastHealthCheck.HasValue)
            {
                var timeSinceLastCheck = (DateTime.UtcNow - mp.LastHealthCheck.Value).TotalSeconds;
                if (timeSinceLastCheck < mp.HealthCheckIntervalSeconds)
                {
                    return; // Too soon
                }
            }

            mp.LastHealthCheck = DateTime.UtcNow;

            var process = mp.RunningProcess;
            if (process == null || process.HasExited) return;

            // Refresh process info
            process.Refresh();

            // Get CPU usage (approximate)
            var startTime = DateTime.UtcNow;
            var startCpuTime = process.TotalProcessorTime;

            System.Threading.Thread.Sleep(500); // Sample for 500ms

            process.Refresh();
            var endTime = DateTime.UtcNow;
            var endCpuTime = process.TotalProcessorTime;

            var cpuUsed = (endCpuTime - startCpuTime).TotalMilliseconds;
            var totalTime = (endTime - startTime).TotalMilliseconds;
            var cpuPercent = (cpuUsed / (Environment.ProcessorCount * totalTime)) * 100;

            mp.LastCpuPercent = cpuPercent;

            // Get memory usage
            var memoryMB = process.WorkingSet64 / (1024.0 * 1024.0);
            mp.LastMemoryMB = memoryMB;

            // Check health violations
            bool isUnhealthy = false;
            string reason = "";

            if (mp.MaxMemoryMB > 0 && memoryMB > mp.MaxMemoryMB)
            {
                isUnhealthy = true;
                reason = $"Memory {memoryMB:F1}MB exceeds limit {mp.MaxMemoryMB}MB";
            }
            else if (mp.MaxCpuPercent > 0 && cpuPercent > mp.MaxCpuPercent)
            {
                isUnhealthy = true;
                reason = $"CPU {cpuPercent:F1}% exceeds limit {mp.MaxCpuPercent}%";
            }
            else if (mp.MinCpuPercent > 0 && cpuPercent < mp.MinCpuPercent)
            {
                isUnhealthy = true;
                reason = $"CPU {cpuPercent:F1}% below minimum {mp.MinCpuPercent}% (zombie?)";
            }

            if (isUnhealthy)
            {
                if (!mp.FirstUnhealthyTime.HasValue)
                {
                    mp.FirstUnhealthyTime = DateTime.UtcNow;
                    _logger.LogWarning("Process {Name} became unhealthy: {Reason}", mp.Name, reason);
                }
                else
                {
                    var unhealthyDuration = (DateTime.UtcNow - mp.FirstUnhealthyTime.Value).TotalSeconds;
                    if (unhealthyDuration >= mp.UnhealthyThresholdSeconds)
                    {
                        _logger.LogError("Process {Name} unhealthy for {Duration}s, restarting. Reason: {Reason}",
                            mp.Name, (int)unhealthyDuration, reason);

                        // Kill and let the main loop restart it
                        _repo.TryKill(mp);
                        mp.FirstUnhealthyTime = null;
                    }
                }
            }
            else
            {
                // Process is healthy
                if (mp.FirstUnhealthyTime.HasValue)
                {
                    _logger.LogInformation("Process {Name} recovered and is now healthy", mp.Name);
                    mp.FirstUnhealthyTime = null;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing health check on {Name}", mp.Name);
        }
    }

    // Public method to manually start a process
    public void ManualStart(ManagedProcess mp)
    {
        if (mp.RunningProcess != null && !mp.RunningProcess.HasExited)
        {
            _logger.LogWarning("Cannot start {Name} - already running (PID: {Pid})", mp.Name, mp.RunningProcess.Id);
            return;
        }

        // Reset circuit breaker on manual start
        mp.CircuitBreakerTripped = false;
        mp.RecentRestarts.Clear();

        StartProcess(mp);
    }

    // Public method to manually stop a process
    public void ManualStop(ManagedProcess mp)
    {
        if (mp.RunningProcess == null || mp.RunningProcess.HasExited)
        {
            _logger.LogWarning("Cannot stop {Name} - not running", mp.Name);
            return;
        }

        _logger.LogInformation("Manually stopping {Name}", mp.Name);
        _repo.TryKill(mp);
    }
}