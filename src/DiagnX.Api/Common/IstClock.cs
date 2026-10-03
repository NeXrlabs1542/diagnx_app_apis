namespace DiagnX.Api.Common;

/// <summary>
/// India has no daylight saving, so a fixed +05:30 offset is exact and avoids depending on
/// the container's time-zone database. Dates the user sees (slots, licence expiry, age)
/// are evaluated in IST; everything stored is UTC.
/// </summary>
public static class IstClock
{
    public static readonly TimeSpan Offset = TimeSpan.FromMinutes(330);

    public static DateTimeOffset Now => DateTimeOffset.UtcNow.ToOffset(Offset);
    public static DateOnly Today => DateOnly.FromDateTime(Now.DateTime);
    public static TimeOnly TimeOfDay => TimeOnly.FromDateTime(Now.DateTime);

    /// <summary>UTC instant for a local IST date + time.</summary>
    public static DateTime ToUtc(DateOnly date, TimeOnly time) =>
        new DateTimeOffset(date.ToDateTime(time), Offset).UtcDateTime;

    public static DateTime AddBusinessDays(DateTime utc, int days)
    {
        var d = utc;
        var added = 0;
        while (added < days)
        {
            d = d.AddDays(1);
            var local = d + Offset;
            if (local.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) added++;
        }
        return d;
    }

    public static int AgeOn(DateOnly dob, DateOnly today)
    {
        var age = today.Year - dob.Year;
        if (today < dob.AddYears(age)) age--;
        return age;
    }

    public static bool TryParseDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", out date);

    public static bool TryParseTime(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, "HH:mm", out time);

    public static string FormatTime(TimeOnly t) => t.ToString("HH:mm");
    public static string FormatDate(DateOnly d) => d.ToString("yyyy-MM-dd");
}
