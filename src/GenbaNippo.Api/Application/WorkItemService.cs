using GenbaNippo.Api.Contracts;
using GenbaNippo.Domain;
using GenbaNippo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GenbaNippo.Api.Application;

public sealed class WorkItemService(
    GenbaDbContext db, SiteService sites, BusinessClock clock, IOptions<ScheduleOptions> options)
{
    public async Task<IReadOnlyList<WorkItemResponse>> ListAsync(int siteId, CancellationToken ct)
    {
        await sites.FindAsync(siteId, ct);
        var items = await db.WorkItems.AsNoTracking()
            .Where(w => w.SiteId == siteId)
            .OrderBy(w => w.PlannedStart).ThenBy(w => w.Id)
            .ToListAsync(ct);
        return items.Select(ToResponse).ToList();
    }

    public async Task<WorkItemResponse> CreateAsync(int siteId, WorkItemRequest request, CancellationToken ct)
    {
        var site = await sites.FindAsync(siteId, ct);
        var item = new WorkItem(site, request.Name, request.PlannedStart!.Value, request.PlannedEnd!.Value);
        db.WorkItems.Add(item);
        await db.SaveChangesAsync(ct);
        return ToResponse(item);
    }

    public async Task<WorkItemResponse> UpdateAsync(int id, WorkItemRequest request, CancellationToken ct)
    {
        var item = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException($"工程（ID: {id}）が見つかりません。");
        var site = await sites.FindAsync(item.SiteId, ct);
        item.Reschedule(site, request.Name, request.PlannedStart!.Value, request.PlannedEnd!.Value);
        await db.SaveChangesAsync(ct);
        return ToResponse(item);
    }

    private WorkItemResponse ToResponse(WorkItem w)
    {
        var evaluation = w.Evaluate(clock.Today, options.Value.BehindTolerancePoints);
        return new WorkItemResponse(
            w.Id, w.SiteId, w.Name, w.PlannedStart, w.PlannedEnd, w.ProgressPercent, w.LastReportedOn,
            evaluation.PlannedPercent, evaluation.Status);
    }
}
