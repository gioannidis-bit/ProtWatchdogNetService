namespace ProtWatchdog.Models;

public static class DtoMapper
{
    public static ManagedProcessDto ToDto(this ManagedProcess mp) =>
        new ManagedProcessDto(
            mp.Id,
            mp.Name,
            mp.ExecutablePath,
            mp.Parameters,
            mp.RestartDelaySeconds,
            mp.RestartCount,
            mp.LastStart,
            mp.LastExitCode
        );
}
