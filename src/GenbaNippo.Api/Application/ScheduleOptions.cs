using GenbaNippo.Domain;

namespace GenbaNippo.Api.Application;

/// <summary>appsettings.json の "Schedule" セクション。</summary>
public sealed class ScheduleOptions
{
    public const string SectionName = "Schedule";

    /// <summary>予定より何ポイント遅れたら「遅れ」とするか。</summary>
    public int BehindTolerancePoints { get; set; } = ScheduleEvaluator.DefaultBehindTolerancePoints;

    /// <summary>「今日」を決めるタイムゾーン。サーバーが UTC で動いていても日本の日付で判定する。</summary>
    public string TimeZoneId { get; set; } = "Asia/Tokyo";
}
