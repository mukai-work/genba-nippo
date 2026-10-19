using System.Text.RegularExpressions;

namespace GenbaNippo.Api.Infrastructure;

/// <summary>
/// 入力エラー（ValidationProblemDetails.errors）を、画面にそのまま出せる形にそろえる。
/// - DataAnnotations のメッセージは Dtos.cs で日本語にしてある
/// - フレームワークが出す型変換エラー（"The input was not valid." など）は英語なので、ここで日本語に置き換える
/// - JSON 本文が読めなかったときに付く "request" の必須エラーは、項目ごとのエラーがあれば重複なので落とす
/// </summary>
public static partial class ValidationMessageLocalizer
{
    private const string RequestKey = "request";

    public static Dictionary<string, string[]> Localize(IDictionary<string, string[]> errors)
    {
        var hasFieldErrors = errors.Keys.Any(k => !string.Equals(k, RequestKey, StringComparison.OrdinalIgnoreCase) && k != "");
        var result = new Dictionary<string, string[]>();
        foreach (var (key, messages) in errors)
        {
            var isRequestKey = string.Equals(key, RequestKey, StringComparison.OrdinalIgnoreCase);
            if (isRequestKey && hasFieldErrors)
            {
                continue;
            }
            var name = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key; // JSON パス（$.startDate）を項目名に
            result[name] = messages
                .Select(m => isRequestKey ? "送信内容を読み取れませんでした。" : Translate(m))
                .Distinct()
                .ToArray();
        }
        return result;
    }

    public static string Translate(string message)
    {
        if (ContainsJapanese(message))
        {
            return message; // 自分で書いた日本語のメッセージはそのまま
        }
        var invalidValue = InvalidValuePattern().Match(message);
        if (invalidValue.Success)
        {
            return $"値「{invalidValue.Groups[1].Value}」の形式が正しくありません。";
        }
        // 型変換の失敗など、フレームワーク由来の英語メッセージ。内部の型名を含むことがあるので中身は出さない
        return "入力の形式が正しくありません。";
    }

    private static bool ContainsJapanese(string s) => s.Any(c => c is >= '぀' and <= 'ヿ' or >= '一' and <= '鿿');

    [GeneratedRegex(@"^The value '(.*)' is not valid", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidValuePattern();
}
