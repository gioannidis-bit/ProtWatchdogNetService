using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProtWatchdog.Models;
using ProtWatchdog.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "ProtWatchdogNetService";
});

builder.Services.AddRazorPages();
builder.Services.AddSingleton<ProcessRepository>();
builder.Services.AddSingleton<ProcessManagerService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<ProcessManagerService>());

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

// Minimal APIs
app.MapPost("/api/processes/add", (ProcessDto dto, ProcessRepository repo) =>
{
    try
    {
        var mp = new ManagedProcess(dto.Name, dto.ExecutablePath, dto.Parameters, dto.RestartDelaySeconds, dto.MaxRestartAttempts, dto.RestartTimeWindowMinutes)
        {
            EnableHealthCheck = dto.EnableHealthCheck,
            HealthCheckIntervalSeconds = dto.HealthCheckIntervalSeconds,
            MaxMemoryMB = dto.MaxMemoryMB,
            MaxCpuPercent = dto.MaxCpuPercent,
            MinCpuPercent = dto.MinCpuPercent,
            UnhealthyThresholdSeconds = dto.UnhealthyThresholdSeconds
        };
        repo.Add(mp);
        return Results.Ok(new { id = mp.Id, message = "Process added successfully" });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to add process");
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/processes", (ProcessRepository repo) =>
{
    try
    {
        return Results.Ok(repo.GetAll().Select(mp => mp.ToApiDto()));
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to get processes");
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/api/processes/remove", (IdDto dto, ProcessRepository repo) =>
{
    try
    {
        repo.Remove(dto.Id);
        return Results.Ok(new { message = "Process removed successfully" });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to remove process");
        return Results.BadRequest(new { error = ex.Message });
    }
});

// NEW: Manual control endpoints
app.MapPost("/api/processes/start", (IdDto dto, ProcessRepository repo, ProcessManagerService manager) =>
{
    try
    {
        var mp = repo.GetById(dto.Id);
        if (mp == null) return Results.NotFound(new { error = "Process not found" });

        mp.AutoRestart = true;
        manager.ManualStart(mp);
        repo.Save();
        return Results.Ok(new { message = "Process start command sent" });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to start process");
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/processes/stop", (IdDto dto, ProcessRepository repo, ProcessManagerService manager) =>
{
    try
    {
        var mp = repo.GetById(dto.Id);
        if (mp == null) return Results.NotFound(new { error = "Process not found" });

        mp.AutoRestart = false;
        manager.ManualStop(mp);
        repo.Save();
        return Results.Ok(new { message = "Process stopped" });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to stop process");
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Load saved config at startup
var repoInstance = app.Services.GetRequiredService<ProcessRepository>();
repoInstance.Load();

app.Run();

public record IdDto(Guid Id);