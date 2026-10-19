using GenbaNippo.Api.Application;
using GenbaNippo.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GenbaNippo.Api.Infrastructure;

internal static class DatabaseInitializer
{
    /// <summary>
    /// 起動時にマイグレーションを当て、設定があればサンプルデータを入れる。
    /// 個人開発の手軽さを優先した作り。本番運用では CI/CD でマイグレーションを当てる方が安全。
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        var config = app.Configuration.GetSection("Database");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GenbaDbContext>();

        if (config.GetValue("ApplyMigrations", true))
        {
            await db.Database.MigrateAsync();
        }
        if (config.GetValue("SeedSampleData", false))
        {
            var clock = scope.ServiceProvider.GetRequiredService<BusinessClock>();
            await SampleDataSeeder.SeedAsync(db, clock.Today, clock.Now);
        }
    }
}
