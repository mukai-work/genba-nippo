using GenbaNippo.Domain;
using Microsoft.EntityFrameworkCore;

namespace GenbaNippo.Infrastructure;

public class GenbaDbContext(DbContextOptions<GenbaDbContext> options) : DbContext(options)
{
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<DailyReport> DailyReports => Set<DailyReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Site>(b =>
        {
            b.Property(s => s.Name).HasMaxLength(Site.NameMaxLength).IsRequired();
            b.Property(s => s.Address).HasMaxLength(Site.AddressMaxLength);
        });

        modelBuilder.Entity<WorkItem>(b =>
        {
            b.Property(w => w.Name).HasMaxLength(WorkItem.NameMaxLength).IsRequired();
            b.HasOne<Site>().WithMany().HasForeignKey(w => w.SiteId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(w => new { w.SiteId, w.PlannedStart });
            b.Property(w => w.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<DailyReport>(b =>
        {
            b.HasOne<Site>().WithMany().HasForeignKey(r => r.SiteId).OnDelete(DeleteBehavior.Cascade);

            // 1つの現場で同じ日の日報は1枚だけ
            b.HasIndex(r => new { r.SiteId, r.ReportDate }).IsUnique();

            b.Property(r => r.WorkSummary).HasMaxLength(DailyReport.SummaryMaxLength).IsRequired();
            b.Property(r => r.RejectionReason).HasMaxLength(DailyReport.RejectionReasonMaxLength);

            // 列挙型は数値でなく文字列で保存する（DB を直接見ても読める・並び順の変更に強い）
            b.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(r => r.Weather).HasConversion<string>().HasMaxLength(20);

            // UPDATE ... WHERE Version = @読み込んだ時の値 になり、他の人が先に更新していれば 0 件更新 → 例外
            b.Property(r => r.Version).IsConcurrencyToken();

            b.OwnsMany(r => r.ProgressEntries, e =>
            {
                e.ToTable("ReportProgressEntries");
                e.WithOwner().HasForeignKey("DailyReportId");
                e.HasKey("DailyReportId", nameof(ProgressEntry.WorkItemId));
                e.HasOne<WorkItem>().WithMany().HasForeignKey(p => p.WorkItemId).OnDelete(DeleteBehavior.Restrict);
            });
            b.Navigation(r => r.ProgressEntries).HasField("_progressEntries");
        });
    }
}
