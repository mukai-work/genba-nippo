using Microsoft.Extensions.Options;

namespace GenbaNippo.Api.Application;

/// <summary>
/// 業務上の「今日」と「今」を返す。.NET 8 以降の TimeProvider を使うので、テストでは FakeTimeProvider で日付を固定できる。
/// </summary>
public sealed class BusinessClock(TimeProvider timeProvider, IOptions<ScheduleOptions> options)
{
    private readonly TimeZoneInfo _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId);

    public DateTimeOffset Now => TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), _timeZone);

    public DateOnly Today => DateOnly.FromDateTime(Now.DateTime);
}
