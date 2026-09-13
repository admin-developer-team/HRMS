using System.Net;
using System.Text.Json;
using Hrms.Application;
using Microsoft.Extensions.Caching.Memory;

namespace Hrms.Infrastructure;

/// <summary>Public suggestions only. Company holidays remain the authoritative work schedule.</summary>
public sealed class PublicHolidaySource(HttpClient client, IMemoryCache cache) : IPublicHolidaySource
{
    private const string IndiaIcs = "https://calendar.google.com/calendar/ical/en.indian%23holiday%40group.v.calendar.google.com/public/basic.ics";

    public async Task<(IReadOnlyList<CalendarObservance> Items, bool Available)> GetAsync(string countryCode, int year, CancellationToken ct)
    {
        if (countryCode.Length != 2 || !countryCode.All(char.IsLetter)) return ([], false);
        var key = $"public-holidays-{countryCode}-{year}";
        if (cache.TryGetValue(key, out CalendarObservance[]? cached) && cached is not null) return (cached, true);
        try
        {
            var url = countryCode == "IN" ? IndiaIcs : $"https://date.nager.at/api/v3/PublicHolidays/{year}/{countryCode}";
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is > 1_000_000) throw new InvalidDataException("Holiday feed too large.");
            var content = await response.Content.ReadAsStringAsync(ct);
            if (content.Length > 1_000_000) throw new InvalidDataException("Holiday feed too large.");
            var rows = (countryCode == "IN" ? Parse(content) : ParseNager(content)).Where(x => x.Date.Year == year).ToArray();
            if (rows.Length == 0) throw new InvalidDataException("Holiday feed has no dates for this year.");
            cache.Set(key, rows, TimeSpan.FromHours(12));
            return (rows, true);
        }
        catch (Exception e) when (e is HttpRequestException or InvalidDataException or TaskCanceledException or JsonException or FormatException)
        {
            return ([], false);
        }
    }

    public static IReadOnlyList<CalendarObservance> Parse(string ics)
    {
        var unfolded = ics.Replace("\r\n", "\n").Split('\n');
        var lines = new List<string>();
        foreach (var line in unfolded)
        {
            if ((line.StartsWith(' ') || line.StartsWith('\t')) && lines.Count > 0) lines[^1] += line[1..];
            else lines.Add(line);
        }
        var results = new List<CalendarObservance>();
        DateOnly? date = null;
        string? name = null, description = null;
        var inEvent = false;
        foreach (var line in lines)
        {
            if (line == "BEGIN:VEVENT") { inEvent = true; date = null; name = null; description = null; continue; }
            if (line == "END:VEVENT")
            {
                if (inEvent && date.HasValue && !string.IsNullOrWhiteSpace(name))
                    results.Add(new CalendarObservance(date.Value, WebUtility.HtmlDecode(name.Replace("\\,", ",").Replace("\\n", " ")),
                        description?.Contains("Public holiday", StringComparison.OrdinalIgnoreCase) == true));
                inEvent = false; continue;
            }
            if (!inEvent) continue;
            if (line.StartsWith("DTSTART", StringComparison.Ordinal))
            {
                var value = line[(line.IndexOf(':') + 1)..];
                if (value.Length >= 8 && DateOnly.TryParseExact(value[..8], "yyyyMMdd", out var parsed)) date = parsed;
            }
            else if (line.StartsWith("SUMMARY:", StringComparison.Ordinal)) name = line[8..];
            else if (line.StartsWith("DESCRIPTION:", StringComparison.Ordinal)) description = line[12..];
        }
        return results.DistinctBy(x => (x.Date, x.Name)).ToArray();
    }

    public static IReadOnlyList<CalendarObservance> ParseNager(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray().Select(x => new CalendarObservance(
            DateOnly.Parse(x.GetProperty("date").GetString()!),
            x.GetProperty("localName").GetString() ?? x.GetProperty("name").GetString() ?? "Holiday", true)).ToArray();
    }
}
