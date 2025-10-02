using ProtWatchdog.Models;
using System.Text.Json;

namespace ProtWatchdog.Services;

public class ProcessRepository
{
    private readonly List<ManagedProcess> _list = new();
    private readonly object _lock = new();
    private readonly string _file = Path.Combine(AppContext.BaseDirectory, "processes.json");

    public IEnumerable<ManagedProcess> GetAll()
    {
        lock (_lock) { return _list.ToList(); }
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
            if (mp.RunningProcess != null && !mp.RunningProcess.HasExited)
                mp.RunningProcess.Kill(true);
        }
        catch { }
        finally { mp.RunningProcess = null; }
    }

    public void Save()
    {
        lock (_lock)
        {
            var dtos = _list.Select(x => x.ToDto()).ToList();
            var json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_file, json);
        }
    }

    public void Load()
    {
        if (File.Exists(_file))
        {
            var json = File.ReadAllText(_file);
            var dtos = JsonSerializer.Deserialize<List<ManagedProcessDto>>(json);
            if (dtos != null)
            {
                foreach (var d in dtos)
                {
                    _list.Add(new ManagedProcess(d.Name, d.ExecutablePath, d.Parameters, d.RestartDelaySeconds)
                    {
                        RestartCount = d.RestartCount,
                        LastStart = d.LastStart,
                        LastExitCode = d.LastExitCode
                    });
                }
            }
        }
    }
}
