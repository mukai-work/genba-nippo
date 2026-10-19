using System.Net;
using GenbaNippo.Api.Contracts;
using GenbaNippo.Domain;
using Microsoft.AspNetCore.Mvc;

namespace GenbaNippo.Api.Tests;

/// <summary>
/// テストクラスごとに API を1つ起動し、テストごとに新しい現場を作ってデータが混ざらないようにする。
/// </summary>
public abstract class ApiTestBase(GenbaApiFactory factory) : IClassFixture<GenbaApiFactory>
{
    protected GenbaApiFactory Factory { get; } = factory;
    protected HttpClient Client { get; } = factory.CreateClient();

    /// <summary>工期 2026/10/01〜2026/12/31 の現場を作る。</summary>
    protected async Task<SiteResponse> CreateSiteAsync(string name = "テスト現場")
    {
        var response = await Client.PostJsonAsync("/api/sites", new
        {
            name,
            startDate = "2026-10-01",
            endDate = "2026-12-31",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<SiteResponse>();
    }

    protected async Task<WorkItemResponse> CreateWorkItemAsync(int siteId, string name, string start, string end)
    {
        var response = await Client.PostJsonAsync($"/api/sites/{siteId}/work-items", new { name, plannedStart = start, plannedEnd = end });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<WorkItemResponse>();
    }

    protected async Task<DailyReportResponse> CreateReportAsync(
        int siteId, string date, params (int WorkItemId, int Percent)[] progress)
    {
        var response = await Client.PostJsonAsync($"/api/sites/{siteId}/reports", new
        {
            reportDate = date,
            weather = Weather.Sunny,
            workerCount = 10,
            workSummary = "配筋作業",
            progressEntries = progress.Select(p => new { workItemId = p.WorkItemId, progressPercent = p.Percent }),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<DailyReportResponse>();
    }

    protected async Task<DailyReportResponse> ActAsync(DailyReportResponse report, string action, object? extra = null)
    {
        var body = extra is null ? (object)new { version = report.Version } : extra;
        var response = await Client.PostJsonAsync($"/api/reports/{report.Id}/{action}", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<DailyReportResponse>();
    }

    protected static async Task<ProblemDetails> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.ReadAsync<ProblemDetails>();
    }
}
