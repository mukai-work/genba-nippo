using GenbaNippo.Domain;
using GenbaNippo.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GenbaNippo.Api.Tests;

/// <summary>サンプルデータは画面のデモに使うので、業務ルールを通って作れること・見せたい状態が揃うことを確かめる。</summary>
public class SampleDataSeederTests
{
    [Fact]
    public async Task 起動日を基準に遅れや承認待ちが揃ったサンプルを作り二度目は何もしない()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<GenbaDbContext>().UseSqlite(connection).Options;
        await using var db = new GenbaDbContext(options);
        await db.Database.MigrateAsync();
        var today = new DateOnly(2026, 10, 19);

        await SampleDataSeeder.SeedAsync(db, today, TimeSpan.FromHours(9));
        await SampleDataSeeder.SeedAsync(db, today, TimeSpan.FromHours(9));

        Assert.Equal(4, await db.Sites.CountAsync());
        var statuses = (await db.WorkItems.ToListAsync()).Select(w => w.Evaluate(today, 10).Status).ToHashSet();
        Assert.Superset(new HashSet<ScheduleStatus> { ScheduleStatus.Behind, ScheduleStatus.Overdue, ScheduleStatus.NotStarted, ScheduleStatus.Completed }, statuses);
        Assert.Equal(2, await db.DailyReports.CountAsync(r => r.Status == ReportStatus.Submitted));
        Assert.Equal(1, await db.DailyReports.CountAsync(r => r.Status == ReportStatus.Rejected));

        // 提出は夕方の時刻（日報の日付の 17 時台まで）にそろっている
        var submittedHours = (await db.DailyReports.ToListAsync()).Select(r => r.SubmittedAt!.Value.Hour);
        Assert.All(submittedHours, h => Assert.InRange(h, 16, 17));
    }
}
