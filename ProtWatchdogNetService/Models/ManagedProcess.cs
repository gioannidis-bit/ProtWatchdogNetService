using System.Diagnostics;
using System.Text.Json.Serialization;

namespace ProtWatchdog.Models;

public class ManagedProcess
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
    public string ExecutablePath { get; set; }
    public string Parameters { get; set; }
    public int RestartDelaySeconds { get; set; } = 5;
    public int MaxRestartAttempts { get; set; } = 10;
    public int RestartTimeWindowMinutes { get; set; } = 5;

    // Health monitoring settings
    public bool EnableHealthCheck { get; set; } = false;
    public int HealthCheckIntervalSeconds { get; set; } = 30;
    public double MaxMemoryMB { get; set; } = 0; // 0 = disabled
    public double MaxCpuPercent { get; set; } = 0; // 0 = disabled
    public double MinCpuPercent { get; set; } = 0; // 0 = disabled (zombie detection)
    public int UnhealthyThresholdSeconds { get; set; } = 60; // How long unhealthy before restart

    public int RestartCount { get; set; }
    public DateTime? LastStart { get; set; }
    public int? LastExitCode { get; set; }
    public bool IsFirstStart { get; set; } = true;
    public bool AutoRestart { get; set; } = true;
    public List<DateTime> RecentRestarts { get; set; } = new();

    [JsonIgnore]
    public Process? RunningProcess { get; set; }

    [JsonIgnore]
    public DateTime? SuppressAutoRestartUntil { get; set; }

    [JsonIgnore]
    public bool CircuitBreakerTripped { get; set; }

    [JsonIgnore]
    public DateTime? FirstUnhealthyTime { get; set; } // Track when process became unhealthy

    [JsonIgnore]
    public DateTime? LastHealthCheck { get; set; }

    [JsonIgnore]
    public double LastCpuPercent { get; set; }

    [JsonIgnore]
    public double LastMemoryMB { get; set; }

    public ManagedProcess()
    {
        Name = "";
        ExecutablePath = "";
        Parameters = "";
    }

    public ManagedProcess(string name, string exe, string parameters, int restartDelaySeconds = 5, int maxRestartAttempts = 10, int restartTimeWindowMinutes = 5)
    {
        Name = name;
        ExecutablePath = exe;
        Parameters = parameters;
        RestartDelaySeconds = restartDelaySeconds;
        MaxRestartAttempts = maxRestartAttempts;
        RestartTimeWindowMinutes = restartTimeWindowMinutes;
    }
}

public record ProcessDto(
    string Name,
    string ExecutablePath,
    string Parameters,
    int RestartDelaySeconds = 5,
    int MaxRestartAttempts = 10,
    int RestartTimeWindowMinutes = 5,
    bool EnableHealthCheck = false,
    int HealthCheckIntervalSeconds = 30,
    double MaxMemoryMB = 0,
    double MaxCpuPercent = 0,
    double MinCpuPercent = 0,
    int UnhealthyThresholdSeconds = 60
);