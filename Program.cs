using Serilog;
using AdhanService;
using AdhanService.Services;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(AppContext.BaseDirectory, "Logs", "adhan-.txt"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddSerilog();

    builder.Services.AddHostedService<Worker>();
    builder.Services.AddSingleton<PrayerScraperService>();
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "Prayer Reminder Service";
    });

    var host = builder.Build();
    host.Run();
}
finally
{
    Log.CloseAndFlush();
}
