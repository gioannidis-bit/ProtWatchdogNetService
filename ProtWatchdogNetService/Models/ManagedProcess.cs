using System.Diagnostics;
using System.Text.Json.Serialization;

namespace ProtWatchdog.Models;

public class ManagedProcess
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; }
    public string ExecutablePath { get; init; }
    public string Parameters { get; init; }
    public int RestartDelaySeconds { get; init; } = 5;

    public int RestartCount { get; set; }
    public DateTime? LastStart { get; set; }
    public int? LastExitCode { get; set; }

    [JsonIgnore]
    public Process? RunningProcess { get; set; }

    public ManagedProcess(string name, string exe, string parameters, int restartDelaySeconds = 5)
    {
        Name = name;
        ExecutablePath = exe;
        Parameters = parameters;
        RestartDelaySeconds = restartDelaySeconds;
    }
}

public record ProcessDto(string Name, string ExecutablePath, string Parameters, int RestartDelaySeconds = 5);
