using GenbaNippo.Api.Contracts;
using GenbaNippo.Domain;
using GenbaNippo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GenbaNippo.Api.Application;

public sealed class SiteService(GenbaDbContext db, BusinessClock clock, IOptions<ScheduleOptions> options)
{
    public async Task<IReadOnlyList<SiteResponse>> ListAsync(CancellationToken ct)
    {
        var sites = await db.Sites.AsNoTracking().OrderByDescending(s => s.StartDate).ThenBy(s => s.Id).ToListAsync(ct);

        var pending = await db.DailyReports.AsNoTracking()
            .Where(r => r.Status == ReportStatus.Submitted)
            .GroupBy(r => r.SiteId)
            .Select(g => new { SiteId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SiteId, x => x.Count, ct);

        // 遅れ判定は「今日」に依存するので DB ではなくアプリ側で計算する。読むのは判定に必要な4列だけ
        var schedules = await db.WorkItems.AsNoTracking()
            .Select(w => new { w.SiteId, w.PlannedStart, w.PlannedEnd, w.ProgressPercent })
            .ToListAsync(ct);
        var delayed = schedules
            .Where(w => IsDelayed(ScheduleEvaluator.Evaluate(
                w.PlannedStart, w.PlannedEnd, w.ProgressPercent, clock.Today, options.Value.BehindTolerancePoints)))
            .GroupBy(w => w.SiteId)
            .ToDictionary(g => g.Key, g => g.Count());

        return sites.Select(s => ToResponse(s, pending.GetValueOrDefault(s.Id), delayed.GetValueOrDefault(s.Id))).ToList();
    }

    public async Task<SiteResponse> GetAsync(int id, CancellationToken ct)
    {
        var site = await FindAsync(id, ct);
        var pending = await db.DailyReports.CountAsync(r => r.SiteId == id && r.Status == ReportStatus.Submitted, ct);
        var workItems = await db.WorkItems.AsNoTracking().Where(w => w.SiteId == id).ToListAsync(ct);
        var delayed = workItems.Count(w => IsDelayed(w.Evaluate(clock.Today, options.Value.BehindTolerancePoints)));
        return ToResponse(site, pending, delayed);
    }

    public async Task<SiteResponse> CreateAsync(CreateSiteRequest request, CancellationToken ct)
    {
        var site = new Site(request.Name, request.Address, request.StartDate!.Value, request.EndDate!.Value);
        db.Sites.Add(site);
        await db.SaveChangesAsync(ct);
        return ToResponse(site, 0, 0);
    }

    internal async Task<Site> FindAsync(int id, CancellationToken ct) =>
        await db.Sites.FirstOrDefaultAsync(s => s.Id == id, ct)
        ?? throw new NotFoundException($"現場（ID: {id}）が見つかりません。");

    private static bool IsDelayed(ScheduleEvaluation e) => e.Status is ScheduleStatus.Behind or ScheduleStatus.Overdue;

    private static SiteResponse ToResponse(Site s, int pending, int delayed) =>
        new(s.Id, s.Name, s.Address, s.StartDate, s.EndDate, pending, delayed);
}
