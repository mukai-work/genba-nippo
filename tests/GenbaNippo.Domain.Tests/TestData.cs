namespace GenbaNippo.Domain.Tests;

internal static class TestData
{
    public static readonly DateOnly SiteStart = new(2026, 10, 1);
    public static readonly DateOnly SiteEnd = new(2026, 12, 31);
    public static readonly DateTimeOffset Now = new(2026, 10, 18, 9, 0, 0, TimeSpan.FromHours(9));

    public static Site Site(int id = 1) =>
        WithId(new Site("テスト現場", null, SiteStart, SiteEnd), id);

    public static WorkItem WorkItem(Site site, int id, string name = "躯体工事", int startDay = 1, int endDay = 31) =>
        WithId(new WorkItem(site, name, new DateOnly(2026, 10, startDay), new DateOnly(2026, 10, endDay)), id);

    public static DailyReport Report(Site site, int day, params ProgressEntry[] entries) =>
        new(site, new DateOnly(2026, 10, day), Weather.Sunny, 10, "配筋作業", entries);

    /// <summary>DB に保存すると振られる ID を、テストでは直接入れる。</summary>
    public static T WithId<T>(T entity, int id)
    {
        typeof(T).GetProperty("Id")!.SetValue(entity, id);
        return entity;
    }
}
