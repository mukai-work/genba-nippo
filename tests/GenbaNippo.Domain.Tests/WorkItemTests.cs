using static GenbaNippo.Domain.Tests.TestData;

namespace GenbaNippo.Domain.Tests;

public class WorkItemTests
{
    [Fact]
    public void 新しい日付の日報なら進捗を反映する()
    {
        var item = WorkItem(Site(), 1);

        Assert.True(item.ApplyProgress(30, new DateOnly(2026, 10, 10)));
        Assert.True(item.ApplyProgress(50, new DateOnly(2026, 10, 12)));

        Assert.Equal(50, item.ProgressPercent);
        Assert.Equal(new DateOnly(2026, 10, 12), item.LastReportedOn);
    }

    [Fact]
    public void 反映済みより古い日付の日報では進捗を巻き戻さない()
    {
        var item = WorkItem(Site(), 1);
        item.ApplyProgress(60, new DateOnly(2026, 10, 20));

        var applied = item.ApplyProgress(40, new DateOnly(2026, 10, 18));

        Assert.False(applied);
        Assert.Equal(60, item.ProgressPercent);
        Assert.Equal(new DateOnly(2026, 10, 20), item.LastReportedOn);
    }

    [Fact]
    public void 進捗を反映すると版番号が上がり無視したときは変わらない()
    {
        var item = WorkItem(Site(), 1);
        Assert.Equal(1, item.Version);

        item.ApplyProgress(60, new DateOnly(2026, 10, 20));
        Assert.Equal(2, item.Version);

        item.ApplyProgress(40, new DateOnly(2026, 10, 18)); // 古い日報なので無視
        Assert.Equal(2, item.Version);
    }

    [Fact]
    public void 同じ日付の日報なら上書きする()
    {
        var item = WorkItem(Site(), 1);
        item.ApplyProgress(40, new DateOnly(2026, 10, 18));

        Assert.True(item.ApplyProgress(45, new DateOnly(2026, 10, 18)));
        Assert.Equal(45, item.ProgressPercent);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void 進捗率は0から100まで(int percent)
    {
        var item = WorkItem(Site(), 1);
        Assert.Throws<DomainRuleException>(() => item.ApplyProgress(percent, new DateOnly(2026, 10, 18)));
    }

    [Fact]
    public void 予定期間は現場の工期の中でなければならない()
    {
        var site = Site();
        var ex = Assert.Throws<DomainRuleException>(() =>
            new WorkItem(site, "外構工事", new DateOnly(2026, 12, 1), new DateOnly(2027, 1, 15)));
        Assert.Contains("工期", ex.Message);
    }

    [Fact]
    public void 終了日が開始日より前ならエラー()
    {
        var site = Site();
        Assert.Throws<DomainRuleException>(() =>
            new WorkItem(site, "基礎工事", new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 9)));
    }

    [Fact]
    public void 工程名は空にできない()
    {
        var site = Site();
        Assert.Throws<DomainRuleException>(() =>
            new WorkItem(site, "  ", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 9)));
    }

    [Fact]
    public void 別の現場へは移せない()
    {
        var item = WorkItem(Site(id: 1), 1);
        Assert.Throws<DomainRuleException>(() =>
            item.Reschedule(Site(id: 2), "躯体工事", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 9)));
    }
}
