namespace GenbaNippo.Domain.Tests;

public class YearMonthTests
{
    [Fact]
    public void yyyy_MM形式を読み取れる()
    {
        Assert.True(YearMonth.TryParse("2026-10", out var ym));
        Assert.Equal(new DateOnly(2026, 10, 1), ym.FirstDay);
        Assert.Equal(new DateOnly(2026, 10, 31), ym.LastDay);
        Assert.Equal("2026-10", ym.ToString());
    }

    [Fact]
    public void うるう年の2月末を正しく出す()
    {
        Assert.Equal(new DateOnly(2028, 2, 29), new YearMonth(2028, 2).LastDay);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026/10")]
    [InlineData("2026-13")]
    [InlineData("2026-10-01")]
    [InlineData("1999-12")]
    public void 形式が違えば読み取らない(string? text)
    {
        Assert.False(YearMonth.TryParse(text, out _));
    }
}
