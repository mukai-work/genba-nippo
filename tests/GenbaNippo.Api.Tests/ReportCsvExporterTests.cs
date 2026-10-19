using GenbaNippo.Api.Application;

namespace GenbaNippo.Api.Tests;

public class ReportCsvExporterTests
{
    [Theory]
    [InlineData("配筋作業", "配筋作業")]
    [InlineData("型枠,配筋", "\"型枠,配筋\"")]
    [InlineData("1行目\n2行目", "\"1行目\n2行目\"")]
    [InlineData("\"墨出し\"完了", "\"\"\"墨出し\"\"完了\"")]
    public void カンマ改行ダブルクォートを含む値はクォートで囲む(string value, string expected)
    {
        Assert.Equal(expected, ReportCsvExporter.Escape(value));
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://example.com\")")]
    [InlineData("+1+1")]
    [InlineData("-2+3")]
    [InlineData("@SUM(A1)")]
    public void 数式として解釈される先頭文字には引用符を付ける(string value)
    {
        Assert.Equal("'" + value, ReportCsvExporter.Sanitize(value));
    }

    [Fact]
    public void 普通の文章はそのまま()
    {
        Assert.Equal("雨天のため午前中止", ReportCsvExporter.Sanitize("雨天のため午前中止"));
        Assert.Equal("", ReportCsvExporter.Sanitize(""));
    }
}
