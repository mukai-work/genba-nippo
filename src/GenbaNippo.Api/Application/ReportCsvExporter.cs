using System.Text;
using GenbaNippo.Domain;

namespace GenbaNippo.Api.Application;

/// <summary>
/// 日報を Excel でそのまま開ける CSV にする。
/// - UTF-8 の BOM を付ける（付けないと Excel が Shift_JIS と誤認して文字化けする）
/// - 改行は CRLF、カンマ・改行・ダブルクォートを含む値はクォートで囲む
/// - 先頭が = + - @ の自由入力は ' を付けて数式として実行されないようにする（CSV インジェクション対策）
/// </summary>
public static class ReportCsvExporter
{
    private static readonly string[] Header = ["日付", "天候", "作業員数", "状態", "作業内容", "工程別進捗"];

    public static byte[] ToExcelCsv(IEnumerable<DailyReport> reports, IReadOnlyDictionary<int, string> workItemNames)
    {
        var sb = new StringBuilder();
        AppendRow(sb, Header);
        foreach (var r in reports)
        {
            var progress = string.Join(" / ", r.ProgressEntries
                .OrderBy(e => e.WorkItemId)
                .Select(e => $"{workItemNames.GetValueOrDefault(e.WorkItemId, "?")} {e.ProgressPercent}%"));
            AppendRow(sb,
            [
                r.ReportDate.ToString("yyyy/MM/dd"),
                Labels.Weather(r.Weather),
                r.WorkerCount.ToString(),
                Labels.Status(r.Status),
                Sanitize(r.WorkSummary),
                Sanitize(progress),
            ]);
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        return [.. preamble, .. body];
    }

    /// <summary>表計算ソフトに数式として解釈される先頭文字を無害化する。</summary>
    public static string Sanitize(string value) =>
        value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;

    public static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    private static void AppendRow(StringBuilder sb, IEnumerable<string> fields)
    {
        sb.Append(string.Join(",", fields.Select(Escape)));
        sb.Append("\r\n");
    }
}

/// <summary>画面・CSV 共通の日本語表示名。</summary>
public static class Labels
{
    public static string Weather(Weather w) => w switch
    {
        Domain.Weather.Sunny => "晴れ",
        Domain.Weather.Cloudy => "曇り",
        Domain.Weather.Rainy => "雨",
        Domain.Weather.Snowy => "雪",
        _ => w.ToString(),
    };

    public static string Status(ReportStatus s) => s switch
    {
        ReportStatus.Draft => "下書き",
        ReportStatus.Submitted => "提出済み",
        ReportStatus.Approved => "承認済み",
        ReportStatus.Rejected => "差戻し",
        _ => s.ToString(),
    };
}
