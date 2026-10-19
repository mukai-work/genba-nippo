using GenbaNippo.Api.Contracts;
using GenbaNippo.Domain;
using GenbaNippo.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GenbaNippo.Api.Application;

/// <summary>
/// 日報のユースケース。ここは「読み込む → ドメインのメソッドを呼ぶ → 保存する」の手順だけを書き、
/// 状態遷移や進捗反映のルールそのものは DailyReport / WorkItem に置く。
/// </summary>
public sealed class DailyReportService(GenbaDbContext db, SiteService sites, BusinessClock clock)
{
    public async Task<IReadOnlyList<DailyReportSummaryResponse>> ListAsync(int siteId, YearMonth month, CancellationToken ct)
    {
        await sites.FindAsync(siteId, ct);
        var reports = await QueryMonth(siteId, month).AsNoTracking().ToListAsync(ct);
        return reports
            .Select(r => new DailyReportSummaryResponse(
                r.Id, r.ReportDate, r.Weather, r.WorkerCount, r.Status, r.WorkSummary, r.Version))
            .ToList();
    }

    public async Task<DailyReportResponse> GetAsync(int id, CancellationToken ct)
    {
        var report = await FindAsync(id, ct);
        return await ToResponseAsync(report, ct);
    }

    public async Task<DailyReportResponse> CreateAsync(int siteId, CreateReportRequest request, CancellationToken ct)
    {
        var site = await sites.FindAsync(siteId, ct);
        var date = request.ReportDate!.Value;
        if (await db.DailyReports.AnyAsync(r => r.SiteId == siteId && r.ReportDate == date, ct))
        {
            throw new DuplicateException($"{date:yyyy/MM/dd} の日報は既にあります。一覧から開いて編集してください。");
        }
        var entries = await ToEntriesAsync(siteId, request.ProgressEntries, ct);

        var report = new DailyReport(site, date, request.Weather!.Value, request.WorkerCount, request.WorkSummary, entries);
        db.DailyReports.Add(report);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // 上の存在チェックと保存の間に、別の人が同じ日の日報を作った場合（一意インデックスで検出）
            if (await db.DailyReports.AsNoTracking().AnyAsync(r => r.SiteId == siteId && r.ReportDate == date, ct))
            {
                throw new DuplicateException($"{date:yyyy/MM/dd} の日報は既にあります。一覧から開いて編集してください。");
            }
            throw;
        }
        return await ToResponseAsync(report, ct);
    }

    public async Task<DailyReportResponse> UpdateAsync(int id, UpdateReportRequest request, CancellationToken ct)
    {
        var report = await FindForUpdateAsync(id, request.Version, ct);
        var entries = await ToEntriesAsync(report.SiteId, request.ProgressEntries, ct);
        report.Edit(request.Weather!.Value, request.WorkerCount, request.WorkSummary, entries);
        await db.SaveChangesAsync(ct);
        return await ToResponseAsync(report, ct);
    }

    public async Task<DailyReportResponse> SubmitAsync(int id, int version, CancellationToken ct)
    {
        var report = await FindForUpdateAsync(id, version, ct);
        report.Submit(clock.Now);
        await db.SaveChangesAsync(ct);
        return await ToResponseAsync(report, ct);
    }

    public async Task<DailyReportResponse> ApproveAsync(int id, int version, CancellationToken ct)
    {
        var report = await FindForUpdateAsync(id, version, ct);
        var workItems = await db.WorkItems.Where(w => w.SiteId == report.SiteId).ToDictionaryAsync(w => w.Id, ct);

        // 日報の状態変更と工程の進捗更新は同じ SaveChanges で保存されるので、片方だけ反映されることはない
        report.Approve(clock.Now, workItems);
        await db.SaveChangesAsync(ct);
        return await ToResponseAsync(report, ct);
    }

    public async Task<DailyReportResponse> RejectAsync(int id, int version, string reason, CancellationToken ct)
    {
        var report = await FindForUpdateAsync(id, version, ct);
        report.Reject(reason);
        await db.SaveChangesAsync(ct);
        return await ToResponseAsync(report, ct);
    }

    public async Task<(byte[] Content, string FileName)> ExportCsvAsync(int siteId, YearMonth month, CancellationToken ct)
    {
        await sites.FindAsync(siteId, ct);
        var reports = await QueryMonth(siteId, month).AsNoTracking().ToListAsync(ct);
        var names = await WorkItemNamesAsync(siteId, ct);
        return (ReportCsvExporter.ToExcelCsv(reports, names), $"nippo_site{siteId}_{month}.csv");
    }

    private IQueryable<DailyReport> QueryMonth(int siteId, YearMonth month) =>
        db.DailyReports
            .Where(r => r.SiteId == siteId && r.ReportDate >= month.FirstDay && r.ReportDate <= month.LastDay)
            .OrderBy(r => r.ReportDate);

    private async Task<DailyReport> FindAsync(int id, CancellationToken ct) =>
        await db.DailyReports.FirstOrDefaultAsync(r => r.Id == id, ct)
        ?? throw new NotFoundException($"日報（ID: {id}）が見つかりません。");

    /// <summary>
    /// 画面が持っている版番号と DB の版番号を比べる。違えば、画面を開いている間に誰かが更新している。
    /// 比べた直後〜保存までの間の競合は、EF Core の同時実行トークン（Version）が UPDATE 時に検出する。
    /// </summary>
    private async Task<DailyReport> FindForUpdateAsync(int id, int version, CancellationToken ct)
    {
        var report = await FindAsync(id, ct);
        if (report.Version != version)
        {
            throw new ConcurrencyConflictException();
        }
        return report;
    }

    private async Task<List<ProgressEntry>> ToEntriesAsync(int siteId, List<ProgressEntryRequest> requests, CancellationToken ct)
    {
        var ids = requests.Select(r => r.WorkItemId).Distinct().ToList();
        var known = await db.WorkItems.Where(w => w.SiteId == siteId && ids.Contains(w.Id)).Select(w => w.Id).ToListAsync(ct);
        var unknown = ids.Except(known).ToList();
        if (unknown.Count > 0)
        {
            throw new DomainRuleException($"この現場にない工程が指定されています（ID: {string.Join(", ", unknown)}）。");
        }
        return requests.Select(r => new ProgressEntry(r.WorkItemId, r.ProgressPercent)).ToList();
    }

    private Task<Dictionary<int, string>> WorkItemNamesAsync(int siteId, CancellationToken ct) =>
        db.WorkItems.AsNoTracking().Where(w => w.SiteId == siteId).ToDictionaryAsync(w => w.Id, w => w.Name, ct);

    private async Task<DailyReportResponse> ToResponseAsync(DailyReport r, CancellationToken ct)
    {
        var names = await WorkItemNamesAsync(r.SiteId, ct);
        return new DailyReportResponse(
            r.Id, r.SiteId, r.ReportDate, r.Weather, r.WorkerCount, r.WorkSummary, r.Status, r.RejectionReason,
            r.SubmittedAt, r.ApprovedAt, r.Version,
            r.ProgressEntries
                .Select(e => new ProgressEntryResponse(e.WorkItemId, names.GetValueOrDefault(e.WorkItemId, "（削除された工程）"), e.ProgressPercent))
                .OrderBy(e => e.WorkItemId)
                .ToList());
    }
}
