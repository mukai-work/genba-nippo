using GenbaNippo.Api.Application;
using GenbaNippo.Api.Contracts;
using GenbaNippo.Domain;
using Microsoft.AspNetCore.Mvc;

namespace GenbaNippo.Api.Controllers;

/// <summary>作業日報と承認フロー。</summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class DailyReportsController(DailyReportService service, BusinessClock clock) : ControllerBase
{
    /// <summary>月ごとの日報一覧。month を省略すると今月。</summary>
    /// <param name="siteId">現場 ID</param>
    /// <param name="month">yyyy-MM（例: 2026-10）</param>
    /// <param name="ct">キャンセル用トークン</param>
    [HttpGet("sites/{siteId:int}/reports")]
    [ProducesResponseType<IReadOnlyList<DailyReportSummaryResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<DailyReportSummaryResponse>> List(int siteId, [FromQuery] YearMonth? month, CancellationToken ct) =>
        service.ListAsync(siteId, ParseMonth(month), ct);

    /// <summary>日報を下書きで作成する。同じ現場・同じ日の日報は1枚まで。</summary>
    [HttpPost("sites/{siteId:int}/reports")]
    [ProducesResponseType<DailyReportResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DailyReportResponse>> Create(int siteId, CreateReportRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(siteId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>日報の詳細。</summary>
    [HttpGet("reports/{id:int}")]
    [ProducesResponseType<DailyReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<DailyReportResponse> Get(int id, CancellationToken ct) => service.GetAsync(id, ct);

    /// <summary>日報を編集する（下書き・差戻しのときだけ）。version が古いと 409。</summary>
    [HttpPut("reports/{id:int}")]
    [ProducesResponseType<DailyReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<DailyReportResponse> Update(int id, UpdateReportRequest request, CancellationToken ct) =>
        service.UpdateAsync(id, request, ct);

    /// <summary>提出する（下書き・差戻し → 提出済み）。</summary>
    [HttpPost("reports/{id:int}/submit")]
    [ProducesResponseType<DailyReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<DailyReportResponse> Submit(int id, ReportActionRequest request, CancellationToken ct) =>
        service.SubmitAsync(id, request.Version, ct);

    /// <summary>承認する（提出済み → 承認済み）。日報の進捗率が工程に反映される。</summary>
    [HttpPost("reports/{id:int}/approve")]
    [ProducesResponseType<DailyReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<DailyReportResponse> Approve(int id, ReportActionRequest request, CancellationToken ct) =>
        service.ApproveAsync(id, request.Version, ct);

    /// <summary>差し戻す（提出済み → 差戻し）。理由は必須。</summary>
    [HttpPost("reports/{id:int}/reject")]
    [ProducesResponseType<DailyReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<DailyReportResponse> Reject(int id, RejectReportRequest request, CancellationToken ct) =>
        service.RejectAsync(id, request.Version, request.Reason, ct);

    /// <summary>月ごとの日報を CSV（UTF-8 BOM 付き・Excel 向け）で出力する。</summary>
    [HttpGet("sites/{siteId:int}/reports/export")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(byte[]))]
    public async Task<IActionResult> Export(int siteId, [FromQuery] YearMonth? month, CancellationToken ct)
    {
        var (content, fileName) = await service.ExportCsvAsync(siteId, ParseMonth(month), ct);
        return File(content, "text/csv; charset=utf-8", fileName);
    }

    /// <summary>month の指定がなければ今月。形式が違えばフレームワークが 400 を返す（YearMonth は IParsable）。</summary>
    private YearMonth ParseMonth(YearMonth? month) => month ?? YearMonth.From(clock.Today);
}
