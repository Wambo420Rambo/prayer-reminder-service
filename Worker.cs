using System.Diagnostics;
using System.Runtime.InteropServices;
using AdhanService.Models;
using AdhanService.Services;

namespace AdhanService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly PrayerScraperService _scraper;

    public Worker(ILogger<Worker> logger, PrayerScraperService scraper)
    {
        _logger = logger;
        _scraper = scraper;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDailyScheduleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in prayer service. Retrying in 5 minutes.");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task RunDailyScheduleAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scraping today's prayer times...");
        var times = await _scraper.ScrapeTodaysPrayerTimesAsync();

        if (string.IsNullOrEmpty(times.Fajr))
        {
            _logger.LogWarning("Could not parse any prayer times. Will retry.");
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            return;
        }

        _logger.LogInformation("📅 Prayer times for {Date}: Fajr={Fajr}, Dhuhr={Dhuhr}, Asr={Asr}, Maghrib={Maghrib}, Isha={Isha}",
            times.Date, times.Fajr, times.Dhuhr, times.Asr, times.Maghrib, times.Isha);

        var prayers = new (string Name, string Time)[]
        {
            ("Fajr", times.Fajr),
            ("Dhuhr", times.Dhuhr),
            ("Asr", times.Asr),
            ("Maghrib", times.Maghrib),
            ("Isha", times.Isha)
        };

        foreach (var (name, timeStr) in prayers)
        {
            if (stoppingToken.IsCancellationRequested) return;

            if (!TimeSpan.TryParse(timeStr, out var timeOfDay))
            {
                _logger.LogWarning("Could not parse time for {Name}: {Time}", name, timeStr);
                continue;
            }

            var prayerTime = DateTime.Today.Add(timeOfDay);
            if (prayerTime <= DateTime.Now)
            {
                _logger.LogInformation("{Name} at {Time} has already passed", name, timeStr);
                continue;
            }

            var delay = prayerTime - DateTime.Now;
            _logger.LogInformation("Next prayer: {Name} at {Time} (in {Delay})", name, prayerTime.ToString("HH:mm"), delay.ToString(@"hh\:mm\:ss"));

            await Task.Delay(delay, stoppingToken);

            if (!stoppingToken.IsCancellationRequested)
            {
                ShowPrayerNotification(name, timeStr);
            }
        }

        var nextMidnight = DateTime.Today.AddDays(1);
        var waitUntilMidnight = nextMidnight - DateTime.Now;
        _logger.LogInformation("All prayers for today done. Waiting until midnight ({Wait}) to scrape next day.", waitUntilMidnight.ToString(@"hh\:mm\:ss"));
        await Task.Delay(waitUntilMidnight, stoppingToken);
    }

    private void ShowPrayerNotification(string prayerName, string time)
    {
        var (message, title) = prayerName switch
        {
            "Fajr" => ($"🌅 Fajr at {time} - Time to pray! 🕌", "Fajr - Dawn Prayer"),
            "Dhuhr" => ($"☀️ Dhuhr  at {time} - Time to pray! 🕌", "Dhuhr - Noon Prayer"),
            "Asr" => ($"🌤️ Asr at {time} - Time to pray! 🕌", "Asr - Afternoon Prayer"),
            "Maghrib" => ($"🌇 Maghrib at {time} - Time to pray! 🕌", "Maghrib - Sunset Prayer"),
            "Isha" => ($"🌙 Isha at {time} - Time to pray! 🕌", "Isha - Night Prayer"),
            _ => ($"🕌 {prayerName} - {time}", prayerName)
        };

        PlayNotificationSound();
        ShowPopup(title, message);
        _logger.LogInformation("✅ {Title}: {Message}", title, message);
    }

    private static void PlayNotificationSound()
    {
        try
        {
            using var ps = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -Command \"[System.Media.SystemSounds]::Asterisk.Play()\"",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            });
            ps?.WaitForExit(1000);
        }
        catch
        {
            // Sound not available
        }
    }

    private static void ShowPopup(string title, string message)
    {
        try
        {
            var sessionId = WTSGetActiveConsoleSessionId();
            if (sessionId == 0xFFFFFFFF)
            {
                _ = WTSSendMessage(WTS_CURRENT_SERVER_HANDLE, WTS_CURRENT_SESSION, title, title.Length * 2, message, message.Length * 2, 0, 30, out _, false);
                return;
            }

            _ = WTSSendMessage(WTS_CURRENT_SERVER_HANDLE, sessionId, title, title.Length * 2, message, message.Length * 2, 0, 30, out _, false);
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "msg",
                    Arguments = $"* \"{title}: {message}\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                });
            }
            catch
            {
                // Both methods failed
            }
        }
    }

    private const uint WTS_CURRENT_SESSION = 0xFFFFFFFF;
    private static readonly IntPtr WTS_CURRENT_SERVER_HANDLE = IntPtr.Zero;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool WTSSendMessage(IntPtr hServer, uint SessionId, string pTitle, int TitleLength, string pMessage, int MessageLength, int Style, int Timeout, out int pResponse, bool bWait);
}
