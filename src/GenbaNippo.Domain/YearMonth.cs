using System.Globalization;

namespace GenbaNippo.Domain;

/// <summary>「2026-10」のような年月。日報の月次一覧と CSV 出力で使う。</summary>
public readonly record struct YearMonth : IParsable<YearMonth>
{
    public int Year { get; }
    public int Month { get; }

    public YearMonth(int year, int month)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
        {
            throw new DomainRuleException("年月の値が正しくありません。");
        }
        Year = year;
        Month = month;
    }

    public static YearMonth From(DateOnly date) => new(date.Year, date.Month);

    public DateOnly FirstDay => new(Year, Month, 1);
    public DateOnly LastDay => FirstDay.AddMonths(1).AddDays(-1);

    /// <summary>"yyyy-MM" 形式だけを受け付ける。</summary>
    public static bool TryParse(string? text, out YearMonth value)
    {
        value = default;
        if (!DateOnly.TryParseExact(text, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || date.Year is < 2000 or > 2100)
        {
            return false;
        }
        value = new YearMonth(date.Year, date.Month);
        return true;
    }

    // IParsable を実装すると、コントローラーの [FromQuery] YearMonth をフレームワークが変換してくれる
    public static YearMonth Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out var value) ? value : throw new FormatException("yyyy-MM 形式で指定してください。");

    public static bool TryParse(string? s, IFormatProvider? provider, out YearMonth result) => TryParse(s, out result);

    public override string ToString() => $"{Year:D4}-{Month:D2}";
}
