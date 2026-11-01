namespace UFWebsite.Services;

// Match times are stored as Swedish local time. DateTime.Now on a cloud server is
// usually UTC, so we always ask for the time in Sweden explicitly.
public static class SwedishTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");

    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);
}