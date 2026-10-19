using GenbaNippo.Domain;
using Microsoft.EntityFrameworkCore;

namespace GenbaNippo.Infrastructure;

/// <summary>
/// 画面を開いてすぐ動きが分かるよう、架空の現場を入れる（開発環境のみ）。
/// 日付は「今日」を基準にずらすので、いつ起動しても遅れ・期限超過・未着手・完了が揃って見える。
/// </summary>
public static class SampleDataSeeder
{
    public static async Task SeedAsync(GenbaDbContext db, DateOnly today, TimeSpan utcOffset, CancellationToken ct = default)
    {
        if (await db.Sites.AnyAsync(ct))
        {
            return;
        }

        var seeder = new Builder(db, today, utcOffset);
        await seeder.ApartmentAsync(ct);
        await seeder.WarehouseAsync(ct);
        await seeder.ToiletRebuildAsync(ct);
        await seeder.ShopRenovationAsync(ct);
    }

    private sealed class Builder(GenbaDbContext db, DateOnly today, TimeSpan utcOffset)
    {
        /// <summary>現場の人が夕方に提出し、所長が翌朝に承認する、という時刻にそろえる。</summary>
        private DateTimeOffset At(DateOnly date, int hour, int minute) =>
            new(date.ToDateTime(new TimeOnly(hour, minute)), utcOffset);

        private async Task<Site> SiteAsync(string name, string address, int startOffset, int endOffset, CancellationToken ct)
        {
            var site = new Site(name, address, today.AddDays(startOffset), today.AddDays(endOffset));
            db.Sites.Add(site);
            await db.SaveChangesAsync(ct);
            return site;
        }

        private async Task<WorkItem[]> ItemsAsync(Site site, CancellationToken ct, params (string Name, int Start, int End)[] items)
        {
            var created = items.Select(i => new WorkItem(site, i.Name, today.AddDays(i.Start), today.AddDays(i.End))).ToArray();
            db.WorkItems.AddRange(created);
            await db.SaveChangesAsync(ct);
            return created;
        }

        private DailyReport Report(Site site, int dayOffset, Weather weather, int workers, string summary, params ProgressEntry[] entries)
        {
            var report = new DailyReport(site, today.AddDays(dayOffset), weather, workers, summary, entries);
            db.DailyReports.Add(report);
            return report;
        }

        private void SubmitAndApprove(DailyReport report, WorkItem[] items)
        {
            report.Submit(At(report.ReportDate, 17, 40));
            report.Approve(At(report.ReportDate.AddDays(1), 8, 15), items.ToDictionary(w => w.Id));
        }

        /// <summary>メインの現場。期限超過・遅れ・未着手・承認待ち・差戻しがすべて見える。</summary>
        public async Task ApartmentAsync(CancellationToken ct)
        {
            var site = await SiteAsync("みどり町 集合住宅新築工事", "みどり市みどり町1丁目（架空）", -60, 120, ct);
            var items = await ItemsAsync(site, ct,
                ("仮設工事", -60, -50), ("土工事", -52, -35), ("基礎工事", -38, -3),
                ("躯体工事", -12, 40), ("内装工事", 30, 95), ("外構工事", 85, 120));
            var (temporary, earthwork, foundation, structure) = (items[0], items[1], items[2], items[3]);

            SubmitAndApprove(Report(site, -3, Weather.Sunny, 12, "基礎の型枠解体。躯体1階の墨出し。",
                new ProgressEntry(temporary.Id, 100), new ProgressEntry(earthwork.Id, 100), new ProgressEntry(foundation.Id, 90), new ProgressEntry(structure.Id, 5)), items);
            SubmitAndApprove(Report(site, -2, Weather.Cloudy, 14, "躯体1階柱の配筋。基礎の埋戻しは雨の予報のため延期。",
                new ProgressEntry(foundation.Id, 90), new ProgressEntry(structure.Id, 10)), items);

            var rejected = Report(site, -1, Weather.Rainy, 0, "雨天のため午前中止。午後から躯体1階の配筋。", new ProgressEntry(structure.Id, 12));
            rejected.Submit(At(rejected.ReportDate, 17, 55));
            rejected.Reject("作業員数が0人になっています。午後の作業人数を記入してください。");

            var submitted = Report(site, 0, Weather.Sunny, 15, "躯体1階柱の配筋完了。型枠の建込み開始。",
                new ProgressEntry(foundation.Id, 95), new ProgressEntry(structure.Id, 15));
            submitted.Submit(At(submitted.ReportDate, 17, 32));

            await db.SaveChangesAsync(ct);
        }

        /// <summary>順調に進んでいる現場（遅れなし・承認待ちなし）。</summary>
        public async Task WarehouseAsync(CancellationToken ct)
        {
            var site = await SiteAsync("さくら台 物流倉庫新築工事", "さくら市さくら台3丁目（架空）", -40, 150, ct);
            var items = await ItemsAsync(site, ct, ("仮設工事", -40, -32), ("杭工事", -34, -10), ("基礎工事", -12, 30), ("鉄骨工事", 25, 90));

            SubmitAndApprove(Report(site, -1, Weather.Sunny, 22, "杭頭処理の完了確認。基礎の掘削を開始。",
                new ProgressEntry(items[0].Id, 100), new ProgressEntry(items[1].Id, 100), new ProgressEntry(items[2].Id, 30)), items);
            await db.SaveChangesAsync(ct);
        }

        /// <summary>着工したばかりの現場（承認待ちが1件）。</summary>
        public async Task ToiletRebuildAsync(CancellationToken ct)
        {
            var site = await SiteAsync("ひがし公園 公衆トイレ建替工事", "ひがし市ひがし町2丁目（架空）", -5, 70, ct);
            var items = await ItemsAsync(site, ct, ("解体工事", -5, 6), ("基礎工事", 7, 25), ("建築工事", 22, 60), ("設備工事", 40, 66));

            SubmitAndApprove(Report(site, -1, Weather.Sunny, 5, "仮囲いの設置。既存トイレの設備撤去。",
                new ProgressEntry(items[0].Id, 45)), items);
            var submitted = Report(site, 0, Weather.Cloudy, 6, "既存トイレの内装解体。産業廃棄物の搬出（2t車2台）。",
                new ProgressEntry(items[0].Id, 55));
            submitted.Submit(At(submitted.ReportDate, 16, 50));
            await db.SaveChangesAsync(ct);
        }

        /// <summary>工期を終えた現場（すべての工程が完了）。</summary>
        public async Task ShopRenovationAsync(CancellationToken ct)
        {
            var site = await SiteAsync("ひばり通り 店舗改修工事", "ひばり市ひばり通り5番（架空）", -75, -20, ct);
            var items = await ItemsAsync(site, ct, ("解体工事", -75, -66), ("内装工事", -65, -30), ("電気設備工事", -55, -25));

            SubmitAndApprove(Report(site, -21, Weather.Sunny, 4, "完了検査の指摘事項（巾木1か所）を是正。引き渡し準備。",
                new ProgressEntry(items[0].Id, 100), new ProgressEntry(items[1].Id, 100), new ProgressEntry(items[2].Id, 100)), items);
            await db.SaveChangesAsync(ct);
        }
    }
}
