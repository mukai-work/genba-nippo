using System.ComponentModel.DataAnnotations;
using GenbaNippo.Domain;

namespace GenbaNippo.Api.Contracts;

// 入力の「形」（必須・桁数）はここで検証し、業務ルール（工期の中か・状態遷移など）は Domain に任せる。

public sealed class CreateSiteRequest
{
    [Required(ErrorMessage = "現場名を入力してください。")]
    [StringLength(Site.NameMaxLength, ErrorMessage = "現場名は{1}文字以内で入力してください。")]
    public string Name { get; init; } = "";

    [StringLength(Site.AddressMaxLength, ErrorMessage = "所在地は{1}文字以内で入力してください。")]
    public string? Address { get; init; }

    [Required(ErrorMessage = "工期の開始日を入力してください。")]
    public DateOnly? StartDate { get; init; }

    [Required(ErrorMessage = "工期の終了日を入力してください。")]
    public DateOnly? EndDate { get; init; }
}

public sealed record SiteResponse(
    int Id,
    string Name,
    string? Address,
    DateOnly StartDate,
    DateOnly EndDate,
    int PendingApprovalCount,
    int DelayedWorkItemCount);

public sealed class WorkItemRequest
{
    [Required(ErrorMessage = "工程名を入力してください。")]
    [StringLength(WorkItem.NameMaxLength, ErrorMessage = "工程名は{1}文字以内で入力してください。")]
    public string Name { get; init; } = "";

    [Required(ErrorMessage = "予定開始日を入力してください。")]
    public DateOnly? PlannedStart { get; init; }

    [Required(ErrorMessage = "予定終了日を入力してください。")]
    public DateOnly? PlannedEnd { get; init; }
}

public sealed record WorkItemResponse(
    int Id,
    int SiteId,
    string Name,
    DateOnly PlannedStart,
    DateOnly PlannedEnd,
    int ProgressPercent,
    DateOnly? LastReportedOn,
    int PlannedPercent,
    ScheduleStatus ScheduleStatus);

public sealed class ProgressEntryRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "工程を選んでください。")]
    public int WorkItemId { get; init; }

    [Range(0, 100, ErrorMessage = "進捗率は0〜100の範囲で入力してください。")]
    public int ProgressPercent { get; init; }
}

public abstract class ReportContentRequest
{
    [Required(ErrorMessage = "天候を選んでください。")]
    public Weather? Weather { get; init; }

    [Range(0, DailyReport.MaxWorkerCount, ErrorMessage = "作業員数は{1}〜{2}人で入力してください。")]
    public int WorkerCount { get; init; }

    [StringLength(DailyReport.SummaryMaxLength, ErrorMessage = "作業内容は{1}文字以内で入力してください。")]
    public string? WorkSummary { get; init; }

    public List<ProgressEntryRequest> ProgressEntries { get; init; } = [];
}

public sealed class CreateReportRequest : ReportContentRequest
{
    [Required(ErrorMessage = "日付を入力してください。")]
    public DateOnly? ReportDate { get; init; }
}

public sealed class UpdateReportRequest : ReportContentRequest
{
    /// <summary>画面が読み込んだときの版番号。</summary>
    [Range(1, int.MaxValue, ErrorMessage = "版番号（version）が必要です。")]
    public int Version { get; init; }
}

public class ReportActionRequest
{
    /// <summary>画面が読み込んだときの版番号。</summary>
    [Range(1, int.MaxValue, ErrorMessage = "版番号（version）が必要です。")]
    public int Version { get; init; }
}

public sealed class RejectReportRequest : ReportActionRequest
{
    [Required(ErrorMessage = "差戻しの理由を入力してください。")]
    [StringLength(DailyReport.RejectionReasonMaxLength, ErrorMessage = "差戻しの理由は{1}文字以内で入力してください。")]
    public string Reason { get; init; } = "";
}

public sealed record DailyReportSummaryResponse(
    int Id,
    DateOnly ReportDate,
    Weather Weather,
    int WorkerCount,
    ReportStatus Status,
    string WorkSummary,
    int Version);

public sealed record ProgressEntryResponse(int WorkItemId, string WorkItemName, int ProgressPercent);

public sealed record DailyReportResponse(
    int Id,
    int SiteId,
    DateOnly ReportDate,
    Weather Weather,
    int WorkerCount,
    string WorkSummary,
    ReportStatus Status,
    string? RejectionReason,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ApprovedAt,
    int Version,
    IReadOnlyList<ProgressEntryResponse> ProgressEntries);
