using GenbaNippo.Api.Infrastructure;

namespace GenbaNippo.Api.Tests;

public class ValidationMessageLocalizerTests
{
    [Fact]
    public void フレームワークの英語メッセージを日本語に置き換え自分で書いた日本語はそのまま()
    {
        Assert.Equal("入力の形式が正しくありません。", ValidationMessageLocalizer.Translate("The input was not valid."));
        Assert.Equal("値「abc」の形式が正しくありません。", ValidationMessageLocalizer.Translate("The value 'abc' is not valid."));
        Assert.Equal("現場名を入力してください。", ValidationMessageLocalizer.Translate("現場名を入力してください。"));
    }

    [Fact]
    public void JSONパスを項目名にし本文全体の重複エラーは落とす()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["$.startDate"] = ["The input was not valid."],
            ["request"] = ["The request field is required."],
        };

        var result = ValidationMessageLocalizer.Localize(errors);

        Assert.Equal(["startDate"], result.Keys);
        Assert.Equal(["入力の形式が正しくありません。"], result["startDate"]);
    }

    [Fact]
    public void 本文が丸ごと読めないときだけは本文のエラーとして返す()
    {
        var result = ValidationMessageLocalizer.Localize(new Dictionary<string, string[]> { ["request"] = ["The request field is required."] });
        Assert.Equal(["送信内容を読み取れませんでした。"], result["request"]);
    }
}
