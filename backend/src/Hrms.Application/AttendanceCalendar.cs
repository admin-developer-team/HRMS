using Hrms.Domain;
using Hrms.Domain.Common;

namespace Hrms.Application;

public static class AttendanceCalendar
{
    public static TimeZoneInfo Zone(string? id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id ?? "UTC"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }

    public static DateOnly WorkDate(DateTimeOffset timestamp, TimeZoneInfo zone, TimeOnly start, TimeOnly end)
    {
        var local = TimeZoneInfo.ConvertTime(timestamp, zone).DateTime;
        var date = DateOnly.FromDateTime(local);
        // The after-midnight part of an overnight shift belongs to the prior workday.
        return end < start && TimeOnly.FromDateTime(local) < end ? date.AddDays(-1) : date;
    }

    public static (DateTime Start, DateTime End) Schedule(DateOnly date, TimeOnly start, TimeOnly end) =>
        (date.ToDateTime(start), (end < start ? date.AddDays(1) : date).ToDateTime(end));

    public static int DurationMinutes(TimeOnly start, TimeOnly end)
    {
        if (start == end) throw new DomainException("Office start and end times must be different.");
        return ((int)(end.ToTimeSpan() - start.ToTimeSpan()).TotalMinutes + 1440) % 1440;
    }

    public static void ValidateInterval(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start) throw new DomainException("Check-out must be after check-in.");
        if (end - start > TimeSpan.FromHours(24)) throw new DomainException("Attendance sessions cannot exceed 24 hours. Submit a correction for a missed check-out.");
        if (end > DateTimeOffset.UtcNow.AddMinutes(5)) throw new DomainException("Attendance time cannot be in the future.");
    }
}
