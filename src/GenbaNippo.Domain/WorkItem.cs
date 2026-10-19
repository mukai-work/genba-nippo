namespace GenbaNippo.Domain;

/// <summary>工程（工種ごとの作業予定）。実績の進捗率は承認済みの日報からだけ更新される。</summary>
public class WorkItem
{
    public const int NameMaxLength = 50;

    public int Id { get; private set; }
    public int SiteId { get; private set; }
    public string Name { get; private set; } = null!;
    public DateOnly PlannedStart { get; private set; }
    public DateOnly PlannedEnd { get; private set; }
    public int ProgressPercent { get; private set; }

    /// <summary>最後に進捗を反映した日報の日付。これより古い日報では進捗を上書きしない。</summary>
    public DateOnly? LastReportedOn { get; private set; }

    /// <summary>
    /// 楽観的同時実行制御の版番号。別々の日報が同時に承認されて、同じ工程の進捗を取り合う場合に備える
    /// （後から保存した側が古い値で上書きしないよう、DB の UPDATE 時に検出する）。
    /// </summary>
    public int Version { get; private set; } = 1;

    private WorkItem() { } // EF Core 用

    public WorkItem(Site site, string name, DateOnly plannedStart, DateOnly plannedEnd)
    {
        SiteId = site.Id;
        Reschedule(site, name, plannedStart, plannedEnd);
        Version = 1;
    }

    public void Reschedule(Site site, string name, DateOnly plannedStart, DateOnly plannedEnd)
    {
        if (site.Id != SiteId)
        {
            throw new DomainRuleException("工程は別の現場へ移せません。");
        }
        Name = Guard.RequiredText(name, NameMaxLength, "工程名");
        Guard.Period(plannedStart, plannedEnd, "予定期間");
        site.EnsureWithinPeriod(plannedStart, plannedEnd, "予定期間");
        PlannedStart = plannedStart;
        PlannedEnd = plannedEnd;
        Version++;
    }

    /// <summary>
    /// 承認された日報の進捗率を反映する。
    /// 承認の順番が日付どおりとは限らないため、反映済みの日報より古い日付なら何もしない（進捗の巻き戻りを防ぐ）。
    /// </summary>
    /// <returns>反映したら true、古い日報なので無視したら false。</returns>
    public bool ApplyProgress(int percent, DateOnly reportDate)
    {
        Guard.Percent(percent, "進捗率");
        if (LastReportedOn is { } last && reportDate < last)
        {
            return false;
        }
        ProgressPercent = percent;
        LastReportedOn = reportDate;
        Version++;
        return true;
    }

    public ScheduleEvaluation Evaluate(DateOnly today, int behindTolerancePoints) =>
        ScheduleEvaluator.Evaluate(PlannedStart, PlannedEnd, ProgressPercent, today, behindTolerancePoints);
}
