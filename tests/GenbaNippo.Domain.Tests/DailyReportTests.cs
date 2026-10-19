using static GenbaNippo.Domain.Tests.TestData;

namespace GenbaNippo.Domain.Tests;

public class DailyReportTests
{
    [Fact]
    public void 作成直後は下書きで版番号は1()
    {
        var report = Report(Site(), 18);

        Assert.Equal(ReportStatus.Draft, report.Status);
        Assert.Equal(1, report.Version);
        Assert.True(report.IsEditable);
    }

    [Fact]
    public void 下書きを提出すると提出済みになり版番号が上がる()
    {
        var report = Report(Site(), 18);

        report.Submit(Now);

        Assert.Equal(ReportStatus.Submitted, report.Status);
        Assert.Equal(Now, report.SubmittedAt);
        Assert.Equal(2, report.Version);
        Assert.False(report.IsEditable);
    }

    [Fact]
    public void 作業内容が空なら提出できない()
    {
        var report = new DailyReport(Site(), new DateOnly(2026, 10, 18), Weather.Rainy, 0, "", []);
        Assert.Throws<DomainRuleException>(() => report.Submit(Now));
    }

    [Fact]
    public void 下書きは承認できない()
    {
        var report = Report(Site(), 18);
        var ex = Assert.Throws<InvalidStateException>(() => report.Approve(Now, new Dictionary<int, WorkItem>()));
        Assert.Contains("下書き", ex.Message);
    }

    [Fact]
    public void 承認すると日報の進捗率が工程に反映される()
    {
        var site = Site();
        var foundation = WorkItem(site, 1, "基礎工事");
        var structure = WorkItem(site, 2, "躯体工事");
        var report = Report(site, 18, new ProgressEntry(1, 100), new ProgressEntry(2, 20));
        report.Submit(Now);

        report.Approve(Now, new Dictionary<int, WorkItem> { [1] = foundation, [2] = structure });

        Assert.Equal(ReportStatus.Approved, report.Status);
        Assert.Equal(100, foundation.ProgressPercent);
        Assert.Equal(20, structure.ProgressPercent);
    }

    [Fact]
    public void 古い日報をあとから承認しても進捗は巻き戻らない()
    {
        var site = Site();
        var structure = WorkItem(site, 1);
        var items = new Dictionary<int, WorkItem> { [1] = structure };
        var older = Report(site, 18, new ProgressEntry(1, 40));
        var newer = Report(site, 20, new ProgressEntry(1, 60));
        older.Submit(Now);
        newer.Submit(Now);

        newer.Approve(Now, items); // 20日の日報が先に承認される
        older.Approve(Now, items); // 18日の日報があとから承認される

        Assert.Equal(ReportStatus.Approved, older.Status);
        Assert.Equal(60, structure.ProgressPercent);
    }

    [Fact]
    public void 他の現場の工程が含まれていたら承認できない()
    {
        var site = Site(id: 1);
        var otherSiteItem = WorkItem(Site(id: 2), 9);
        var report = Report(site, 18, new ProgressEntry(9, 50));
        report.Submit(Now);

        Assert.Throws<DomainRuleException>(() => report.Approve(Now, new Dictionary<int, WorkItem> { [9] = otherSiteItem }));
        Assert.Equal(ReportStatus.Submitted, report.Status);
    }

    [Fact]
    public void 一部の工程が見つからないときは他の工程も更新しない()
    {
        var site = Site();
        var structure = WorkItem(site, 1);
        var report = Report(site, 18, new ProgressEntry(1, 50), new ProgressEntry(2, 30)); // 工程2は存在しない
        report.Submit(Now);

        Assert.Throws<DomainRuleException>(() => report.Approve(Now, new Dictionary<int, WorkItem> { [1] = structure }));

        Assert.Equal(0, structure.ProgressPercent); // 途中まで反映されていない
        Assert.Null(structure.LastReportedOn);
    }

    [Fact]
    public void 差戻しには理由が必要()
    {
        var report = Report(Site(), 18);
        report.Submit(Now);

        Assert.Throws<DomainRuleException>(() => report.Reject(" "));
        Assert.Equal(ReportStatus.Submitted, report.Status);
    }

    [Fact]
    public void 差し戻された日報は直して再提出できる()
    {
        var report = Report(Site(), 18);
        report.Submit(Now);
        report.Reject("作業員数を確認してください");

        Assert.Equal(ReportStatus.Rejected, report.Status);
        Assert.Equal("作業員数を確認してください", report.RejectionReason);

        report.Edit(Weather.Sunny, 12, "配筋作業（人数修正）", []);
        report.Submit(Now);

        Assert.Equal(ReportStatus.Submitted, report.Status);
        Assert.Equal(12, report.WorkerCount);
        Assert.Null(report.RejectionReason); // 再提出したら前回の差戻し理由は消える
    }

    [Fact]
    public void 承認済みの日報は編集も差戻しもできない()
    {
        var report = Report(Site(), 18);
        report.Submit(Now);
        report.Approve(Now, new Dictionary<int, WorkItem>());

        Assert.Throws<InvalidStateException>(() => report.Edit(Weather.Cloudy, 5, "変更", []));
        Assert.Throws<InvalidStateException>(() => report.Reject("やり直し"));
        Assert.Throws<InvalidStateException>(() => report.Submit(Now));
    }

    [Fact]
    public void 編集すると版番号が上がり進捗行は差し替わる()
    {
        var report = Report(Site(), 18, new ProgressEntry(1, 10), new ProgressEntry(2, 20));
        var kept = report.ProgressEntries.Single(e => e.WorkItemId == 1);

        report.Edit(Weather.Cloudy, 8, "型枠", [new ProgressEntry(1, 15), new ProgressEntry(3, 5)]);

        Assert.Equal(2, report.Version);
        Assert.Equal(new[] { 1, 3 }, report.ProgressEntries.Select(e => e.WorkItemId).Order());
        Assert.Equal(15, report.ProgressEntries.Single(e => e.WorkItemId == 1).ProgressPercent);
        Assert.Same(kept, report.ProgressEntries.Single(e => e.WorkItemId == 1)); // 既存の行は作り直さない
    }

    [Fact]
    public void 同じ工程を2回入力するとエラー()
    {
        Assert.Throws<DomainRuleException>(() =>
            Report(Site(), 18, new ProgressEntry(1, 10), new ProgressEntry(1, 20)));
    }

    [Fact]
    public void 日付は現場の工期の中でなければならない()
    {
        Assert.Throws<DomainRuleException>(() =>
            new DailyReport(Site(), new DateOnly(2027, 1, 5), Weather.Sunny, 3, "片付け", []));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000)]
    public void 作業員数の範囲チェック(int workers)
    {
        Assert.Throws<DomainRuleException>(() =>
            new DailyReport(Site(), new DateOnly(2026, 10, 18), Weather.Sunny, workers, "配筋", []));
    }
}
