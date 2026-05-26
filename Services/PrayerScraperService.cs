using HtmlAgilityPack;
using AdhanService.Models;

namespace AdhanService.Services;

public class PrayerScraperService
{
    private readonly string _url;
    private readonly ILogger<PrayerScraperService> _logger;

    public PrayerScraperService(IConfiguration configuration, ILogger<PrayerScraperService> logger)
    {
        _url = configuration["PrayerSettings:Url"] ?? throw new InvalidOperationException("PrayerSettings:Url is not configured in appsettings.json");
        _logger = logger;
    }

    public async Task<DailyPrayerTimes> ScrapeTodaysPrayerTimesAsync()
    {
        try
        {
            var web = new HtmlWeb();
            var doc = await web.LoadFromWebAsync(_url);

            var times = new DailyPrayerTimes
            {
                Fajr = ExtractTime(doc, "fajr"),
                Sunrise = ExtractTime(doc, "shuruk"),
                Dhuhr = ExtractTime(doc, "dhuhr"),
                Asr = ExtractTime(doc, "asr"),
                Maghrib = ExtractTime(doc, "maghrib"),
                Isha = ExtractTime(doc, "ishaa"),
                Date = ExtractDate(doc)
            };

            _logger.LogInformation($"Scraped prayer times: Fajr = {times.Fajr}, Dhuhr = {times.Dhuhr}, Asr = {times.Asr}, Maghrib = {times.Maghrib}, Isha = {times.Isha}");

            return times;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape prayer times from {Url}", _url);
            throw;
        }
    }

    private static string ExtractTime(HtmlDocument doc, string prayerKey)
    {
        var node = doc.DocumentNode.SelectSingleNode($"//a[contains(@href, 'today.{prayerKey}.')]/span");
        return node != null ? HtmlEntity.DeEntitize(node.InnerText.Trim()) : string.Empty;
    }

    private static string ExtractDate(HtmlDocument doc)
    {
        var node = doc.DocumentNode.SelectSingleNode("//p[@class='pt-1 text-sm text-gray-500 dark:text-gray-400']");
        return node != null ? HtmlEntity.DeEntitize(node.InnerText.Trim()) : DateTime.Now.ToString("dd. MMMM yyyy");
    }
}
