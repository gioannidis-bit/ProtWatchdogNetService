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
builder.Services.AddHostedService<ProcessManagerService>();

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
    var mp = new ManagedProcess(dto.Name, dto.ExecutablePath, dto.Parameters, dto.RestartDelaySeconds);
    repo.Add(mp); // μέσα στο Add γίνεται Save()
    return Results.Ok(new { id = mp.Id });
});

app.MapGet("/api/processes", (ProcessRepository repo) =>
    repo.GetAll().Select(mp => mp.ToDto())
);

app.MapPost("/api/processes/remove", (IdDto dto, ProcessRepository repo) =>
{
    repo.Remove(dto.Id); // μέσα στο Remove γίνεται Save()
    return Results.Ok();
});

// Load saved config at startup
var repoInstance = app.Services.GetRequiredService<ProcessRepository>();
repoInstance.Load();

app.Run();

public record IdDto(Guid Id);
