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
                var items = _repo.GetAll().ToList();
                foreach (var mp in items)
                {
                    if (mp.RunningProcess == null || mp.RunningProcess.HasExited)
                    {
                        if (mp.RunningProcess != null)
                        {
                            mp.LastExitCode = mp.RunningProcess.ExitCode;
                            mp.RunningProcess.Dispose();
                            mp.RunningProcess = null;
                        }

                        try
                        {
                            var startInfo = new ProcessStartInfo
                            {
                                FileName = mp.ExecutablePath,
                                Arguments = mp.Parameters,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };

                            var p = Process.Start(startInfo);
                            mp.RunningProcess = p;
                            mp.LastStart = DateTime.UtcNow;
                            mp.RestartCount++;
                            _logger.LogInformation("Started {Name} (pid {Pid})", mp.Name, p?.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to start {Name}", mp.Name);
                        }

                        await Task.Delay(TimeSpan.FromSeconds(mp.RestartDelaySeconds), stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in monitor loop");
            }

            await Task.Delay(_monitorInterval, stoppingToken);
        }
    }
}
