using System.Net;
using System.Text;
using GenbaNippo.Api.Contracts;
using GenbaNippo.Domain;
using GenbaNippo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GenbaNippo.Api.Tests;

public class DailyReportsApiTests(GenbaApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task 下書き作成から提出と承認まで通すと工程の進捗が更新される()
    {
        var site = await CreateSiteAsync();
        var structure = await CreateWorkItemAsync(site.Id, "躯体工事", "2026-10-10", "2026-10-29");

        var draft = await CreateReportAsync(site.Id, "2026-10-18", (structure.Id, 30));
        Assert.Equal(ReportStatus.Draft, draft.Status);
        Assert.Equal(1, draft.Version);
        Assert.Equal("躯体工事", draft.ProgressEntries.Single().WorkItemName);

        var submitted = await ActAsync(draft, "submit");
        Assert.Equal(ReportStatus.Submitted, submitted.Status);
        Assert.Equal(new DateTimeOffset(2026, 10, 18, 9, 0, 0, TimeSpan.FromHours(9)), submitted.SubmittedAt); // 日本時間で記録

        var siteBeforeApproval = await (await Client.GetAsync($"/api/sites/{site.Id}")).ReadAsync<SiteResponse>();
        Assert.Equal(1, siteBeforeApproval.PendingApprovalCount);

        var approved = await ActAsync(submitted, "approve");
        Assert.Equal(ReportStatus.Approved, approved.Status);
        Assert.Equal(3, approved.Version);

        var items = await (await Client.GetAsync($"/api/sites/{site.Id}/work-items")).ReadAsync<List<WorkItemResponse>>();
        Assert.Equal(30, items.Single().ProgressPercent);
        Assert.Equal(new DateOnly(2026, 10, 18), items.Single().LastReportedOn);
    }

    [Fact]
    public async Task 承認前の日報は工程の進捗に影響しない()
    {
        var site = await CreateSiteAsync();
        var item = await CreateWorkItemAsync(site.Id, "躯体工事", "2026-10-10", "2026-10-29");
        var report = await CreateReportAsync(site.Id, "2026-10-18", (item.Id, 50));
        await ActAsync(report, "submit");

        var items = await (await Client.GetAsync($"/api/sites/{site.Id}/work-items")).ReadAsync<List<WorkItemResponse>>();
        Assert.Equal(0, items.Single().ProgressPercent);
    }

    [Fact]
    public async Task 古い日付の日報をあとから承認しても進捗は巻き戻らない()
    {
        var site = await CreateSiteAsync();
        var item = await CreateWorkItemAsync(site.Id, "躯体工事", "2026-10-10", "2026-10-29");
        var older = await ActAsync(await CreateReportAsync(site.Id, "2026-10-16", (item.Id, 40)), "submit");
        var newer = await ActAsync(await CreateReportAsync(site.Id, "2026-10-17", (item.Id, 60)), "submit");

        await ActAsync(newer, "approve");
        await ActAsync(older, "approve");

        var items = await (await Client.GetAsync($"/api/sites/{site.Id}/work-items")).ReadAsync<List<WorkItemResponse>>();
        Assert.Equal(60, items.Single().ProgressPercent);
        Assert.Equal(new DateOnly(2026, 10, 17), items.Single().LastReportedOn);
    }

    [Fact]
    public async Task 古い版番号で更新すると409で再読み込みを促す()
    {
        var site = await CreateSiteAsync();
        var report = await CreateReportAsync(site.Id, "2026-10-18");

        // Aさんが先に編集して版番号が 2 になる
        var first = await Client.PutJsonAsync($"/api/reports/{report.Id}", new
        {
            version = 1, weather = Weather.Cloudy, workerCount = 8, workSummary = "Aさんの編集", progressEntries = Array.Empty<object>(),
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Bさんは版番号 1 の画面のまま保存しようとする
        var second = await Client.PutJsonAsync($"/api/reports/{report.Id}", new
        {
            version = 1, weather = Weather.Rainy, workerCount = 3, workSummary = "Bさんの編集", progressEntries = Array.Empty<object>(),
        });

        var problem = await AssertProblemAsync(second, HttpStatusCode.Conflict);
        Assert.Contains("再読み込み", problem.Detail);

        var current = await (await Client.GetAsync($"/api/reports/{report.Id}")).ReadAsync<DailyReportResponse>();
        Assert.Equal("Aさんの編集", current.WorkSummary); // Bさんの内容で上書きされていない
    }

    [Fact]
    public async Task 読み込みから保存までの間に他で更新されたらDBの同時実行トークンで検出する()
    {
        var site = await CreateSiteAsync();
        var report = await CreateReportAsync(site.Id, "2026-10-18");

        // 1人目: DB から読み込んだところで止まっている
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GenbaDbContext>();
        var loaded = await db.DailyReports.SingleAsync(r => r.Id == report.Id);

        // 2人目: その間に API から編集を確定させる
        var other = await Client.PutJsonAsync($"/api/reports/{report.Id}", new
        {
            version = 1, weather = Weather.Cloudy, workerCount = 8, workSummary = "先に保存", progressEntries = Array.Empty<object>(),
        });
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);

        // 1人目が保存すると、UPDATE ... WHERE Version = 1 が 0 件になって例外
        loaded.Edit(Weather.Rainy, 3, "あとから保存", []);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task 下書きを承認しようとすると409()
    {
        var site = await CreateSiteAsync();
        var report = await CreateReportAsync(site.Id, "2026-10-18");

        var response = await Client.PostJsonAsync($"/api/reports/{report.Id}/approve", new { version = report.Version });

        var problem = await AssertProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Contains("提出済みの日報だけが承認できます", problem.Detail);
    }

    [Fact]
    public async Task 同じ日の日報は2枚作れない()
    {
        var site = await CreateSiteAsync();
        await CreateReportAsync(site.Id, "2026-10-18");

        var response = await Client.PostJsonAsync($"/api/sites/{site.Id}/reports", new
        {
            reportDate = "2026-10-18", weather = Weather.Sunny, workerCount = 1, workSummary = "二重登録",
        });

        var problem = await AssertProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Contains("既にあります", problem.Detail);
    }

    [Fact]
    public async Task 差戻しには理由が必要で差し戻した日報は直して再提出できる()
    {
        var site = await CreateSiteAsync();
        var submitted = await ActAsync(await CreateReportAsync(site.Id, "2026-10-18"), "submit");

        var noReason = await Client.PostJsonAsync($"/api/reports/{submitted.Id}/reject", new { version = submitted.Version, reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        var rejected = await ActAsync(submitted, "reject", new { version = submitted.Version, reason = "作業員数を確認してください" });
        Assert.Equal(ReportStatus.Rejected, rejected.Status);
        Assert.Equal("作業員数を確認してください", rejected.RejectionReason);

        var edited = await Client.PutJsonAsync($"/api/reports/{rejected.Id}", new
        {
            version = rejected.Version, weather = Weather.Sunny, workerCount = 12, workSummary = "配筋作業（人数修正）",
            progressEntries = Array.Empty<object>(),
        });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);

        var resubmitted = await ActAsync(await edited.ReadAsync<DailyReportResponse>(), "submit");
        Assert.Equal(ReportStatus.Submitted, resubmitted.Status);
    }

    [Fact]
    public async Task 他の現場の工程を指定すると400()
    {
        var siteA = await CreateSiteAsync("A現場");
        var siteB = await CreateSiteAsync("B現場");
        var itemOfB = await CreateWorkItemAsync(siteB.Id, "基礎工事", "2026-10-01", "2026-10-31");

        var response = await Client.PostJsonAsync($"/api/sites/{siteA.Id}/reports", new
        {
            reportDate = "2026-10-18", weather = Weather.Sunny, workerCount = 5, workSummary = "配筋",
            progressEntries = new[] { new { workItemId = itemOfB.Id, progressPercent = 10 } },
        });

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Contains("この現場にない工程", problem.Detail);
    }

    [Fact]
    public async Task 月を指定して日報一覧を取得できる()
    {
        var site = await CreateSiteAsync();
        await CreateReportAsync(site.Id, "2026-11-02");
        await CreateReportAsync(site.Id, "2026-10-31");
        await CreateReportAsync(site.Id, "2026-10-01");

        var october = await (await Client.GetAsync($"/api/sites/{site.Id}/reports?month=2026-10")).ReadAsync<List<DailyReportSummaryResponse>>();
        Assert.Equal(new[] { new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31) }, october.Select(r => r.ReportDate).ToArray());

        var withoutMonth = await (await Client.GetAsync($"/api/sites/{site.Id}/reports")).ReadAsync<List<DailyReportSummaryResponse>>();
        Assert.Equal(2, withoutMonth.Count); // 省略時は今月（2026-10）

        var badMonth = await Client.GetAsync($"/api/sites/{site.Id}/reports?month=2026/10");
        await AssertProblemAsync(badMonth, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task 月次CSVはBOM付きUTF8でExcel向けに出力する()
    {
        var site = await CreateSiteAsync();
        var item = await CreateWorkItemAsync(site.Id, "躯体工事", "2026-10-10", "2026-10-29");
        await CreateReportAsync(site.Id, "2026-10-18", (item.Id, 30));

        var response = await Client.GetAsync($"/api/sites/{site.Id}/reports/export?month=2026-10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal($"nippo_site{site.Id}_2026-10.csv", response.Content.Headers.ContentDisposition?.FileName);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);

        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("日付,天候,作業員数,状態,作業内容,工程別進捗", lines[0]);
        Assert.Equal("2026/10/18,晴れ,10,下書き,配筋作業,躯体工事 30%", lines[1]);
    }
}
