using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GenbaNippo.Infrastructure;

/// <summary>
/// `dotnet ef migrations add` のときだけ使われる。API を起動せずにマイグレーションを作れるようにする。
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GenbaDbContext>
{
    public GenbaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<GenbaDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new GenbaDbContext(options);
    }
}
