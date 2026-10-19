namespace GenbaNippo.Domain;

/// <summary>エンティティの入力チェックをまとめたもの。メッセージは画面にそのまま出す前提で日本語にしている。</summary>
internal static class Guard
{
    public static string RequiredText(string? value, int maxLength, string label)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainRuleException($"{label}を入力してください。");
        }
        if (trimmed.Length > maxLength)
        {
            throw new DomainRuleException($"{label}は{maxLength}文字以内で入力してください。");
        }
        return trimmed;
    }

    public static string? OptionalText(string? value, int maxLength, string label)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        if (trimmed.Length > maxLength)
        {
            throw new DomainRuleException($"{label}は{maxLength}文字以内で入力してください。");
        }
        return trimmed;
    }

    public static void Period(DateOnly start, DateOnly end, string label)
    {
        if (end < start)
        {
            throw new DomainRuleException($"{label}の終了日は開始日以降にしてください。");
        }
    }

    public static int Percent(int value, string label)
    {
        if (value is < 0 or > 100)
        {
            throw new DomainRuleException($"{label}は0〜100の範囲で入力してください。");
        }
        return value;
    }
}
