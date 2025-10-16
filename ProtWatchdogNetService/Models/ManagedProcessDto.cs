namespace ProtWatchdog.Models;

// DTO for API responses (with runtime info)
public record ManagedProcessDto(
    Guid Id,
    string Name,
    string ExecutablePath,
    string Parameters,
    int RestartDelaySeconds,
    int RestartCount,
    DateTime? LastStart,
    int? LastExitCode,
    int? CurrentPid = null,
    bool IsRunning = false,
    bool AutoRestart = true,
    int MaxRestartAttempts = 10,
    int RestartTimeWindowMinutes = 5,
    bool CircuitBreakerTripped = false,
    int RecentRestartCount = 0,
    bool EnableHealthCheck = false,
    double LastCpuPercent = 0,
    double LastMemoryMB = 0,
    DateTime? LastHealthCheck = null,
    bool IsHealthy = true,
    string? HealthStatus = null
);

// DTO for JSON persistence (only persistent fields)
public record PersistedProcessDto(
    Guid Id,
    string Name,
    string ExecutablePath,
    string Parameters,
    int RestartDelaySeconds,
    int RestartCount,
    DateTime? LastStart,
    int? LastExitCode,
    bool AutoRestart,
    int MaxRestartAttempts,
    int RestartTimeWindowMinutes,
    List<DateTime>? RecentRestarts = null,
    bool EnableHealthCheck = false,
    int HealthCheckIntervalSeconds = 30,
    double MaxMemoryMB = 0,
    double MaxCpuPercent = 0,
    double MinCpuPercent = 0,
    int UnhealthyThresholdSeconds = 60
);