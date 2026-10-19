namespace GenbaNippo.Domain;

/// <summary>現場（工事）。工期の中に工程と日報がぶら下がる。</summary>
public class Site
{
    public const int NameMaxLength = 100;
    public const int AddressMaxLength = 200;

    public int Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Address { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }

    private Site() { } // EF Core 用

    public Site(string name, string? address, DateOnly startDate, DateOnly endDate)
    {
        Name = Guard.RequiredText(name, NameMaxLength, "現場名");
        Address = Guard.OptionalText(address, AddressMaxLength, "所在地");
        Guard.Period(startDate, endDate, "工期");
        StartDate = startDate;
        EndDate = endDate;
    }

    /// <summary>工程や日報の日付がこの現場の工期に収まっているかを確かめる。</summary>
    public void EnsureWithinPeriod(DateOnly start, DateOnly end, string label)
    {
        if (start < StartDate || end > EndDate)
        {
            throw new DomainRuleException(
                $"{label}は現場の工期（{StartDate:yyyy/MM/dd}〜{EndDate:yyyy/MM/dd}）の中で指定してください。");
        }
    }
}
