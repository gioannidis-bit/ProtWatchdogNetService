namespace ProtWatchdog.Models;

public record ManagedProcessDto(
    Guid Id,
    string Name,
    string ExecutablePath,
    string Parameters,
    int RestartDelaySeconds,
    int RestartCount,
    DateTime? LastStart,
    int? LastExitCode
);
