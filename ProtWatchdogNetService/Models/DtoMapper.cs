namespace ProtWatchdog.Models;

public static class DtoMapper
{
    // Convert to API DTO (with runtime info)
    public static ManagedProcessDto ToApiDto(this ManagedProcess mp)
    {
        // Calculate recent restarts in time window
        var cutoff = DateTime.UtcNow.AddMinutes(-mp.RestartTimeWindowMinutes);
        var recentCount = mp.RecentRestarts.Count(r => r > cutoff);

        // Determine health status
        bool isHealthy = true;
        string? healthStatus = null;

        if (mp.EnableHealthCheck && mp.RunningProcess != null && !mp.RunningProcess.HasExited)
        {
            if (mp.FirstUnhealthyTime.HasValue)
            {
                var unhealthyDuration = (DateTime.UtcNow - mp.FirstUnhealthyTime.Value).TotalSeconds;
                isHealthy = false;
                healthStatus = $"Unhealthy for {(int)unhealthyDuration}s";
            }
            else
            {
                healthStatus = "Healthy";
            }
        }

        return new ManagedProcessDto(
            mp.Id,
            mp.Name,
            mp.ExecutablePath,
            mp.Parameters,
            mp.RestartDelaySeconds,
            mp.RestartCount,
            mp.LastStart,
            mp.LastExitCode,
            mp.RunningProcess?.Id,
            IsRunning(mp),
            mp.AutoRestart,
            mp.MaxRestartAttempts,
            mp.RestartTimeWindowMinutes,
            mp.CircuitBreakerTripped,
            recentCount,
            mp.EnableHealthCheck,
            Math.Round(mp.LastCpuPercent, 1),
            Math.Round(mp.LastMemoryMB, 1),
            mp.LastHealthCheck,
            isHealthy,
            healthStatus
        );
    }

    // Convert to persistence DTO (for JSON save)
    public static PersistedProcessDto ToPersistedDto(this ManagedProcess mp)
    {
        return new PersistedProcessDto(
            mp.Id,
            mp.Name,
            mp.ExecutablePath,
            mp.Parameters,
            mp.RestartDelaySeconds,
            mp.RestartCount,
            mp.LastStart,
            mp.LastExitCode,
            mp.AutoRestart,
            mp.MaxRestartAttempts,
            mp.RestartTimeWindowMinutes,
            mp.RecentRestarts,
            mp.EnableHealthCheck,
            mp.HealthCheckIntervalSeconds,
            mp.MaxMemoryMB,
            mp.MaxCpuPercent,
            mp.MinCpuPercent,
            mp.UnhealthyThresholdSeconds
        );
    }

    private static bool IsRunning(ManagedProcess mp)
    {
        try
        {
            return mp.RunningProcess != null && !mp.RunningProcess.HasExited;
        }
        catch
        {
            return false;
        }
    }
}