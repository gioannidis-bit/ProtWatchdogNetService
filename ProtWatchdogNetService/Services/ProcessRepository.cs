using ProtWatchdog.Models;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ProtWatchdog.Services;

public class ProcessRepository
{
    private readonly List<ManagedProcess> _list = new();
    private readonly object _lock = new();
    private readonly string _file = Path.Combine(AppContext.BaseDirectory, "processes.json");
    private readonly ILogger<ProcessRepository> _logger;

    public ProcessRepository(ILogger<ProcessRepository> logger)
    {
        _logger = logger;
    }

    public IEnumerable<ManagedProcess> GetAll()
    {
        lock (_lock) { return _list.ToList(); }
    }

    public ManagedProcess? GetById(Guid id)
    {
        lock (_lock) { return _list.FirstOrDefault(x => x.Id == id); }
    }

    public void Add(ManagedProcess mp)
    {
        lock (_lock) { _list.Add(mp); }
        Save();
    }

    public void Remove(Guid id)
    {
        lock (_lock)
        {
            var e = _list.FirstOrDefault(x => x.Id == id);
            if (e != null)
            {
                TryKill(e);
                _list.Remove(e);
            }
        }
        Save();
    }

    public void TryKill(ManagedProcess mp)
    {
        try
        {
            Process? processToKill = null;
            int? pidToKill = null;

            // Try to get the process - either from cache or by PID lookup
            if (mp.RunningProcess != null)
            {
                try
                {
                    if (!mp.RunningProcess.HasExited)
                    {
                        processToKill = mp.RunningProcess;
                        pidToKill = mp.RunningProcess.Id;
                    }
                }
                catch
                {
                    // Process handle might be invalid, try PID lookup
                }
            }

            // If we still don't have a process but we know the PID, try to get it
            if (processToKill == null && pidToKill == null && mp.RunningProcess != null)
            {
                try
                {
                    pidToKill = mp.RunningProcess.Id;
                    processToKill = Process.GetProcessById(pidToKill.Value);
                }
                catch
                {
                    // Process might have already exited
                }
            }

            if (processToKill != null && pidToKill.HasValue)
            {
                _logger.LogInformation("Killing process {Name} (PID: {Pid})", mp.Name, pidToKill.Value);

                // Kill the process tree (true = includeDescendants)
                processToKill.Kill(entireProcessTree: true);

                // Wait a bit for it to die
                processToKill.WaitForExit(2000);

                _logger.LogInformation("Process {Name} (PID: {Pid}) killed successfully", mp.Name, pidToKill.Value);
            }
            else
            {
                _logger.LogInformation("Process {Name} not running or already exited", mp.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to kill process {Name}", mp.Name);
        }
        finally
        {
            // Always cleanup
            try
            {
                mp.RunningProcess?.Dispose();
            }
            catch { }
            mp.RunningProcess = null;
        }
    }

    public void Save()
    {
        try
        {
            lock (_lock)
            {
                var dtos = _list.Select(x => x.ToPersistedDto()).ToList();
                var json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_file, json);
                _logger.LogDebug("Saved {Count} processes to {File}", dtos.Count, _file);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save processes to {File}", _file);
        }
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_file))
            {
                var json = File.ReadAllText(_file);
                var dtos = JsonSerializer.Deserialize<List<PersistedProcessDto>>(json);
                if (dtos != null)
                {
                    lock (_lock)
                    {
                        foreach (var d in dtos)
                        {
                            var mp = new ManagedProcess(
                                d.Name,
                                d.ExecutablePath,
                                d.Parameters,
                                d.RestartDelaySeconds,
                                d.MaxRestartAttempts,
                                d.RestartTimeWindowMinutes)
                            {
                                Id = d.Id,
                                RestartCount = d.RestartCount,
                                LastStart = d.LastStart,
                                LastExitCode = d.LastExitCode,
                                IsFirstStart = false,
                                AutoRestart = d.AutoRestart,
                                RecentRestarts = d.RecentRestarts ?? new List<DateTime>(),
                                EnableHealthCheck = d.EnableHealthCheck,
                                HealthCheckIntervalSeconds = d.HealthCheckIntervalSeconds,
                                MaxMemoryMB = d.MaxMemoryMB,
                                MaxCpuPercent = d.MaxCpuPercent,
                                MinCpuPercent = d.MinCpuPercent,
                                UnhealthyThresholdSeconds = d.UnhealthyThresholdSeconds
                            };
                            _list.Add(mp);
                        }
                    }
                    _logger.LogInformation("Loaded {Count} processes from {File}", dtos.Count, _file);
                }
            }
            else
            {
                _logger.LogInformation("No saved processes file found at {File}", _file);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load processes from {File}", _file);
        }
    }

    // NEW: Method to update runtime stats without triggering full save
    public void UpdateStats(ManagedProcess mp)
    {
        // Stats are updated in-memory; periodic save can be added if needed
    }
}