namespace GenbaNippo.Domain;

public enum ReportStatus
{
    /// <summary>下書き</summary>
    Draft,
    /// <summary>提出済み（承認待ち）</summary>
    Submitted,
    /// <summary>承認済み</summary>
    Approved,
    /// <summary>差戻し</summary>
    Rejected,
}

public enum Weather
{
    Sunny,
    Cloudy,
    Rainy,
    Snowy,
}

/// <summary>日報に書く「工程ごとの今日時点の進捗率」。</summary>
public class ProgressEntry
{
    public int WorkItemId { get; private set; }
    public int ProgressPercent { get; private set; }

    private ProgressEntry() { } // EF Core 用

    public ProgressEntry(int workItemId, int progressPercent)
    {
        WorkItemId = workItemId;
        ProgressPercent = Guard.Percent(progressPercent, "進捗率");
    }

    internal void ChangePercent(int progressPercent) =>
        ProgressPercent = Guard.Percent(progressPercent, "進捗率");
}

/// <summary>
/// 作業日報。状態遷移（下書き → 提出 → 承認 / 差戻し → 再提出）はこのクラスの中だけで行う。
/// 呼び出し側は「今どの状態か」を気にせずメソッドを呼び、許されない遷移なら例外を受け取る。
/// </summary>
public class DailyReport
{
    public const int SummaryMaxLength = 2000;
    public const int RejectionReasonMaxLength = 500;
    public const int MaxWorkerCount = 999;

    private readonly List<ProgressEntry> _progressEntries = [];

    public int Id { get; private set; }
    public int SiteId { get; private set; }
    public DateOnly ReportDate { get; private set; }
    public Weather Weather { get; private set; }
    public int WorkerCount { get; private set; }
    public string WorkSummary { get; private set; } = "";
    public ReportStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }

    /// <summary>楽観的同時実行制御の版番号。内容か状態が変わるたびに 1 増える。</summary>
    public int Version { get; private set; }

    public IReadOnlyList<ProgressEntry> ProgressEntries => _progressEntries;

    private DailyReport() { } // EF Core 用

    public DailyReport(
        Site site, DateOnly reportDate, Weather weather, int workerCount, string? workSummary,
        IEnumerable<ProgressEntry> progressEntries)
    {
        site.EnsureWithinPeriod(reportDate, reportDate, "日報の日付");
        SiteId = site.Id;
        ReportDate = reportDate;
        Status = ReportStatus.Draft;
        Version = 1;
        ApplyContent(weather, workerCount, workSummary, progressEntries);
    }

    public bool IsEditable => Status is ReportStatus.Draft or ReportStatus.Rejected;

    public void Edit(Weather weather, int workerCount, string? workSummary, IEnumerable<ProgressEntry> progressEntries)
    {
        if (!IsEditable)
        {
            throw new InvalidStateException($"{StatusLabel}の日報は編集できません。");
        }
        ApplyContent(weather, workerCount, workSummary, progressEntries);
        Version++;
    }

    public void Submit(DateTimeOffset now)
    {
        if (!IsEditable)
        {
            throw new InvalidStateException($"{StatusLabel}の日報は提出できません。");
        }
        if (string.IsNullOrWhiteSpace(WorkSummary))
        {
            throw new DomainRuleException("作業内容を入力してから提出してください。");
        }
        Status = ReportStatus.Submitted;
        SubmittedAt = now;
        RejectionReason = null; // 直して再提出したので、前回の差戻し理由は役目を終える
        Version++;
    }

    /// <summary>
    /// 承認し、日報に書かれた進捗率を工程へ反映する。
    /// </summary>
    /// <param name="workItems">この現場の工程（WorkItemId で引けるもの）。</param>
    public void Approve(DateTimeOffset now, IReadOnlyDictionary<int, WorkItem> workItems)
    {
        if (Status != ReportStatus.Submitted)
        {
            throw new InvalidStateException($"{StatusLabel}の日報は承認できません。提出済みの日報だけが承認できます。");
        }
        // 先に全行を確かめてから反映する（途中で例外になって一部の工程だけ更新される、を防ぐ）
        var targets = _progressEntries
            .Select(entry => (entry, workItem: workItems.GetValueOrDefault(entry.WorkItemId)))
            .ToList();
        var missing = targets.FirstOrDefault(t => t.workItem is null || t.workItem.SiteId != SiteId);
        if (missing.entry is not null)
        {
            throw new DomainRuleException($"工程（ID: {missing.entry.WorkItemId}）がこの現場に見つかりません。");
        }
        foreach (var (entry, workItem) in targets)
        {
            workItem!.ApplyProgress(entry.ProgressPercent, ReportDate);
        }
        Status = ReportStatus.Approved;
        ApprovedAt = now;
        Version++;
    }

    public void Reject(string? reason)
    {
        if (Status != ReportStatus.Submitted)
        {
            throw new InvalidStateException($"{StatusLabel}の日報は差し戻せません。提出済みの日報だけが差し戻せます。");
        }
        RejectionReason = Guard.RequiredText(reason, RejectionReasonMaxLength, "差戻しの理由");
        Status = ReportStatus.Rejected;
        Version++;
    }

    private string StatusLabel => Status switch
    {
        ReportStatus.Draft => "下書き",
        ReportStatus.Submitted => "提出済み",
        ReportStatus.Approved => "承認済み",
        ReportStatus.Rejected => "差戻し",
        _ => Status.ToString(),
    };

    private void ApplyContent(Weather weather, int workerCount, string? workSummary, IEnumerable<ProgressEntry> progressEntries)
    {
        if (!Enum.IsDefined(weather))
        {
            throw new DomainRuleException("天候の値が正しくありません。");
        }
        if (workerCount is < 0 or > MaxWorkerCount)
        {
            throw new DomainRuleException($"作業員数は0〜{MaxWorkerCount}人で入力してください。");
        }
        var summary = workSummary?.Trim() ?? "";
        if (summary.Length > SummaryMaxLength)
        {
            throw new DomainRuleException($"作業内容は{SummaryMaxLength}文字以内で入力してください。");
        }

        var entries = progressEntries.ToList();
        if (entries.GroupBy(e => e.WorkItemId).Any(g => g.Count() > 1))
        {
            throw new DomainRuleException("同じ工程の進捗が2回入力されています。");
        }

        Weather = weather;
        WorkerCount = workerCount;
        WorkSummary = summary;
        ReplaceEntries(entries);
    }

    /// <summary>
    /// 進捗の行を差し替える。全削除→全追加にせず、既存の行は値だけ更新する
    /// （EF Core が同じキーの行を「削除して追加」と解釈して衝突しないようにするため）。
    /// </summary>
    private void ReplaceEntries(List<ProgressEntry> entries)
    {
        _progressEntries.RemoveAll(existing => entries.All(e => e.WorkItemId != existing.WorkItemId));
        foreach (var entry in entries)
        {
            var existing = _progressEntries.FirstOrDefault(e => e.WorkItemId == entry.WorkItemId);
            if (existing is null)
            {
                _progressEntries.Add(entry);
            }
            else
            {
                existing.ChangePercent(entry.ProgressPercent);
            }
        }
    }
}
