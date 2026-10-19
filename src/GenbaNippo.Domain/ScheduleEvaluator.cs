namespace GenbaNippo.Domain;

public enum ScheduleStatus
{
    /// <summary>未着手（まだ遅れとは言えない）</summary>
    NotStarted,
    /// <summary>予定どおり</summary>
    OnTrack,
    /// <summary>予定より遅れている</summary>
    Behind,
    /// <summary>終了予定日を過ぎても終わっていない</summary>
    Overdue,
    /// <summary>完了</summary>
    Completed,
}

/// <param name="PlannedPercent">今日時点で終わっているはずの割合</param>
/// <param name="ActualPercent">日報で報告された実績</param>
public sealed record ScheduleEvaluation(int PlannedPercent, int ActualPercent, ScheduleStatus Status)
{
    /// <summary>予定との差（マイナスなら遅れ）。</summary>
    public int Gap => ActualPercent - PlannedPercent;
}

/// <summary>
/// 予定と実績から工程の状態を判定する。「今日」は引数で受け取り、時計には触らない（テストで日付を固定するため）。
/// </summary>
public static class ScheduleEvaluator
{
    public const int DefaultBehindTolerancePoints = 10;

    /// <summary>
    /// 予定進捗率。予定期間を日数で均等に割り、今日の作業が終わった時点の割合を返す（開始日=1日目）。
    /// 例: 10日間の工程の3日目なら 30%。
    /// </summary>
    public static int PlannedPercent(DateOnly plannedStart, DateOnly plannedEnd, DateOnly today)
    {
        if (today < plannedStart)
        {
            return 0;
        }
        if (today >= plannedEnd)
        {
            return 100;
        }
        var totalDays = plannedEnd.DayNumber - plannedStart.DayNumber + 1;
        var elapsedDays = today.DayNumber - plannedStart.DayNumber + 1;
        return elapsedDays * 100 / totalDays; // 切り捨て。予定を甘く見積もらない
    }

    public static ScheduleEvaluation Evaluate(
        DateOnly plannedStart, DateOnly plannedEnd, int actualPercent, DateOnly today, int behindTolerancePoints)
    {
        if (behindTolerancePoints < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(behindTolerancePoints));
        }

        var planned = PlannedPercent(plannedStart, plannedEnd, today);
        var shortfall = planned - actualPercent;
        var isBehind = shortfall > 0 && shortfall >= behindTolerancePoints;

        var status = actualPercent >= 100 ? ScheduleStatus.Completed
            : today > plannedEnd ? ScheduleStatus.Overdue
            : isBehind ? ScheduleStatus.Behind
            : actualPercent == 0 ? ScheduleStatus.NotStarted
            : ScheduleStatus.OnTrack;

        return new ScheduleEvaluation(planned, actualPercent, status);
    }
}
