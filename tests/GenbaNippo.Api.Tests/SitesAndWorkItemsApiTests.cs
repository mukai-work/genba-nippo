using System.Net;
using GenbaNippo.Api.Contracts;
using GenbaNippo.Domain;
using Microsoft.AspNetCore.Mvc;

namespace GenbaNippo.Api.Tests;

public class SitesAndWorkItemsApiTests(GenbaApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task 現場を登録すると201とLocationを返し詳細を取得できる()
    {
        var response = await Client.PostJsonAsync("/api/sites", new
        {
            name = "  みどり町 倉庫新築工事  ",
            startDate = "2026-10-01",
            endDate = "2027-03-31",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<SiteResponse>();
        Assert.Equal("みどり町 倉庫新築工事", created.Name); // 前後の空白は落とす
        Assert.NotNull(response.Headers.Location);

        var fetched = await (await Client.GetAsync(response.Headers.Location)).ReadAsync<SiteResponse>();
        Assert.Equal(created.Id, fetched.Id);
    }

    [Fact]
    public async Task 必須項目が足りなければ400で項目ごとのメッセージを返す()
    {
        var response = await Client.PostJsonAsync("/api/sites", new { name = "", startDate = "2026-10-01" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadAsync<ValidationProblemDetails>();
        Assert.Equal("入力内容に誤りがあります", problem.Title);
        Assert.Contains("現場名を入力してください。", problem.Errors["Name"]);
        Assert.Contains("工期の終了日を入力してください。", problem.Errors["EndDate"]);
    }

    [Fact]
    public async Task 工期の終了日が開始日より前なら400()
    {
        var response = await Client.PostJsonAsync("/api/sites", new { name = "逆転現場", startDate = "2026-10-10", endDate = "2026-10-01" });

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Contains("終了日は開始日以降", problem.Detail);
    }

    [Fact]
    public async Task 存在しない現場は404()
    {
        var problem = await AssertProblemAsync(await Client.GetAsync("/api/sites/99999"), HttpStatusCode.NotFound);
        Assert.Contains("99999", problem.Detail);
    }

    [Fact]
    public async Task 存在しないAPIパスはSPAのindexではなく404のProblemDetails()
    {
        await AssertProblemAsync(await Client.GetAsync("/api/no-such-endpoint"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task 工程一覧は今日時点の遅れ判定つきで予定開始日順に返す()
    {
        var site = await CreateSiteAsync();
        // 今日 = 10/18
        await CreateWorkItemAsync(site.Id, "躯体工事", "2026-10-10", "2026-10-29"); // 20日間の9日目 → 予定45%・実績0% → 遅れ
        await CreateWorkItemAsync(site.Id, "基礎工事", "2026-10-01", "2026-10-10"); // 終了予定日を過ぎて未完了 → 期限超過
        await CreateWorkItemAsync(site.Id, "内装工事", "2026-11-01", "2026-11-30"); // まだ始まっていない → 未着手

        var items = await (await Client.GetAsync($"/api/sites/{site.Id}/work-items")).ReadAsync<List<WorkItemResponse>>();

        Assert.Equal(new[] { "基礎工事", "躯体工事", "内装工事" }, items.Select(i => i.Name).ToArray());
        Assert.Equal(ScheduleStatus.Overdue, items[0].ScheduleStatus);
        Assert.Equal(ScheduleStatus.Behind, items[1].ScheduleStatus);
        Assert.Equal(45, items[1].PlannedPercent);
        Assert.Equal(ScheduleStatus.NotStarted, items[2].ScheduleStatus);

        var summary = await (await Client.GetAsync($"/api/sites/{site.Id}")).ReadAsync<SiteResponse>();
        Assert.Equal(2, summary.DelayedWorkItemCount);
    }

    [Fact]
    public async Task 工程の予定期間が現場の工期をはみ出すと400()
    {
        var site = await CreateSiteAsync();
        var response = await Client.PostJsonAsync($"/api/sites/{site.Id}/work-items", new
        {
            name = "外構工事",
            plannedStart = "2026-12-01",
            plannedEnd = "2027-01-31",
        });

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Contains("工期", problem.Detail);
    }

    [Fact]
    public async Task 工程の予定を変更できる()
    {
        var site = await CreateSiteAsync();
        var item = await CreateWorkItemAsync(site.Id, "躯体工事", "2026-10-10", "2026-10-29");

        var response = await Client.PutJsonAsync($"/api/work-items/{item.Id}", new
        {
            name = "躯体工事（1〜3階）",
            plannedStart = "2026-10-10",
            plannedEnd = "2026-11-30",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<WorkItemResponse>();
        var fetched = await (await Client.GetAsync($"/api/work-items/{item.Id}")).ReadAsync<WorkItemResponse>();
        Assert.Equal(updated.Name, fetched.Name);
        Assert.Equal("躯体工事（1〜3階）", updated.Name);
        Assert.Equal(new DateOnly(2026, 11, 30), updated.PlannedEnd);
    }
}
