namespace GenbaNippo.Domain.Tests;

public class ScheduleEvaluatorTests
{
    // 10/1〜10/10 の10日間の工程
    private static readonly DateOnly Start = new(2026, 10, 1);
    private static readonly DateOnly End = new(2026, 10, 10);

    [Theory]
    [InlineData(9, 30, 0)]    // 開始前
    [InlineData(10, 1, 10)]   // 1日目が終わると10%
    [InlineData(10, 3, 30)]
    [InlineData(10, 10, 100)] // 最終日
    [InlineData(10, 20, 100)] // 終了後
    public void 予定進捗率は経過日数を工期の日数で割った値(int month, int day, int expected)
    {
        var today = new DateOnly(2026, month, day);
        Assert.Equal(expected, ScheduleEvaluator.PlannedPercent(Start, End, today));
    }

    [Fact]
    public void 予定進捗率は切り捨てる()
    {
        // 3日間の1日目 = 33.3…% → 33%
        Assert.Equal(33, ScheduleEvaluator.PlannedPercent(Start, Start.AddDays(2), Start));
    }

    [Fact]
    public void 一日だけの工程は当日に100パーセント()
    {
        Assert.Equal(100, ScheduleEvaluator.PlannedPercent(Start, Start, Start));
    }

    [Theory]
    [InlineData(5, 50, ScheduleStatus.OnTrack)]   // 予定50%・実績50%
    [InlineData(5, 41, ScheduleStatus.OnTrack)]   // 9ポイント遅れはまだ許容
    [InlineData(5, 40, ScheduleStatus.Behind)]    // 10ポイント遅れで「遅れ」
    [InlineData(5, 0, ScheduleStatus.Behind)]     // 着手していないが予定は50%
    [InlineData(1, 0, ScheduleStatus.Behind)]     // 1日目終了時点で予定10%・実績0% は閾値ちょうどで「遅れ」
    [InlineData(5, 100, ScheduleStatus.Completed)]
    [InlineData(11, 90, ScheduleStatus.Overdue)]  // 終了予定日を過ぎて未完了
    [InlineData(11, 100, ScheduleStatus.Completed)] // 期限後でも完了していれば完了
    public void 予定と実績から工程の状態を判定する(int day, int actual, ScheduleStatus expected)
    {
        var result = ScheduleEvaluator.Evaluate(Start, End, actual, new DateOnly(2026, 10, day), behindTolerancePoints: 10);
        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void 予定がわずかで実績ゼロなら未着手()
    {
        // 20日間の1日目 = 予定5%。実績0%でも閾値（10ポイント）未満なので「遅れ」ではなく「未着手」
        var result = ScheduleEvaluator.Evaluate(Start, Start.AddDays(19), 0, Start, 10);
        Assert.Equal(ScheduleStatus.NotStarted, result.Status);
        Assert.Equal(5, result.PlannedPercent);
    }

    [Fact]
    public void 開始前で実績ゼロなら未着手()
    {
        var result = ScheduleEvaluator.Evaluate(Start, End, 0, Start.AddDays(-5), 10);
        Assert.Equal(ScheduleStatus.NotStarted, result.Status);
        Assert.Equal(0, result.PlannedPercent);
    }

    [Fact]
    public void 遅れの閾値は設定で変えられる()
    {
        var day5 = new DateOnly(2026, 10, 5); // 予定50%
        Assert.Equal(ScheduleStatus.Behind, ScheduleEvaluator.Evaluate(Start, End, 35, day5, 10).Status);
        Assert.Equal(ScheduleStatus.OnTrack, ScheduleEvaluator.Evaluate(Start, End, 35, day5, 20).Status);
    }

    [Fact]
    public void 閾値ゼロでも予定どおりなら遅れにしない()
    {
        var day5 = new DateOnly(2026, 10, 5); // 予定50%
        Assert.Equal(ScheduleStatus.OnTrack, ScheduleEvaluator.Evaluate(Start, End, 50, day5, 0).Status);
        Assert.Equal(ScheduleStatus.Behind, ScheduleEvaluator.Evaluate(Start, End, 49, day5, 0).Status);
    }

    [Fact]
    public void 予定との差をマイナスで返す()
    {
        var result = ScheduleEvaluator.Evaluate(Start, End, 20, new DateOnly(2026, 10, 5), 10);
        Assert.Equal(-30, result.Gap);
    }
}
