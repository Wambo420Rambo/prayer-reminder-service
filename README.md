# Prayer Reminder Service

A Windows service that scrapes daily Islamic prayer times from [gebetszeiten.de](https://www.gebetszeiten.de) and shows desktop notifications at each prayer time.

## How It Works

- On startup and every midnight, scrapes today's prayer times (Fajr, Dhuhr, Asr, Maghrib, Isha)
- Schedules notifications at each prayer time using `WTSSendMessage` popups with sound
- Runs as a Windows background service

## Tech Stack

- .NET 8 Worker Service
- HtmlAgilityPack (web scraping)
- Serilog (file + console logging)
- Inno Setup (installer)

## Build

```
dotnet publish -c Release -r win-x64 --self-contained true
```

Or use the batch script which also compiles the installer:

```
build-and-package.bat
```

Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php) installed for the installer step.

## Install

Run the generated `PrayerReminder-Setup.exe` installer as Administrator. During setup, paste your gebetszeiten.de URL for your city (with MWL 2007 calculation method selected).

## Configuration

The service reads its settings from `appsettings.json`:

```json
{
  "PrayerSettings": {
    "Url": "https://www.gebetszeiten.de/..."
  }
}
```

The URL is configured during installation and written automatically by the installer.
