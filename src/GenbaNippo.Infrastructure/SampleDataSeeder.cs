using GenbaNippo.Domain;
using Microsoft.EntityFrameworkCore;

namespace GenbaNippo.Infrastructure;

/// <summary>
/// 画面を開いてすぐ動きが分かるよう、架空の現場を1件だけ入れる。
/// 日付は「今日」を基準にずらすので、いつ起動しても遅れ・期限超過・未着手が揃って見える。
/// </summary>
public static class SampleDataSeeder
{
    public static async Task SeedAsync(GenbaDbContext db, DateOnly today, DateTimeOffset now, CancellationToken ct = default)
    {
        if (await db.Sites.AnyAsync(ct))
        {
            return;
        }

        var site = new Site("（サンプル）みどり町 集合住宅新築工事", "架空の所在地", today.AddDays(-60), today.AddDays(120));
        db.Sites.Add(site);
        await db.SaveChangesAsync(ct);

        WorkItem Item(string name, int startOffset, int endOffset) =>
            new(site, name, today.AddDays(startOffset), today.AddDays(endOffset));

        var temporary = Item("仮設工事", -60, -50);
        var earthwork = Item("土工事", -52, -35);
        var foundation = Item("基礎工事", -38, -3);
        var structure = Item("躯体工事", -12, 40);
        var interior = Item("内装工事", 30, 95);
        var exterior = Item("外構工事", 85, 120);
        db.WorkItems.AddRange(temporary, earthwork, foundation, structure, interior, exterior);
        await db.SaveChangesAsync(ct);

        var workItems = new[] { temporary, earthwork, foundation, structure, interior, exterior }.ToDictionary(w => w.Id);

        // 承認済み: 3日前・2日前（進捗が工程に反映される）
        var approved1 = new DailyReport(site, today.AddDays(-3), Weather.Sunny, 12,
            "基礎の型枠解体、躯体1階の墨出し。",
            [new(temporary.Id, 100), new(earthwork.Id, 100), new(foundation.Id, 90), new(structure.Id, 5)]);
        approved1.Submit(now);
        approved1.Approve(now, workItems);

        var approved2 = new DailyReport(site, today.AddDays(-2), Weather.Cloudy, 14,
            "躯体1階柱の配筋。基礎の埋戻しは雨のため延期。",
            [new(foundation.Id, 90), new(structure.Id, 10)]);
        approved2.Submit(now);
        approved2.Approve(now, workItems);

        // 差戻し: 前日（作業員数の記載漏れを想定）
        var rejected = new DailyReport(site, today.AddDays(-1), Weather.Rainy, 0,
            "雨天のため午前中止、午後から躯体1階の配筋。",
            [new(structure.Id, 12)]);
        rejected.Submit(now);
        rejected.Reject("作業員数が0人になっています。午後の作業人数を記入してください。");

        // 提出済み（承認待ち）と下書き
        var submitted = new DailyReport(site, today, Weather.Sunny, 15,
            "躯体1階柱の配筋完了、型枠建込み開始。",
            [new(foundation.Id, 95), new(structure.Id, 15)]);
        submitted.Submit(now);

        db.DailyReports.AddRange(approved1, approved2, rejected, submitted);
        await db.SaveChangesAsync(ct);
    }
}
