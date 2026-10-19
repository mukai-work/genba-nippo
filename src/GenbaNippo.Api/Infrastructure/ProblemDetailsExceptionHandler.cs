using GenbaNippo.Api.Application;
using GenbaNippo.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenbaNippo.Api.Infrastructure;

/// <summary>
/// ProblemDetails の type。同じ 409 でも「同時編集の衝突（再読み込みすれば直る）」と「状態が違う」を
/// 画面がメッセージ文字列ではなく type で見分けられるようにする。
/// </summary>
public static class ProblemTypes
{
    public const string BusinessRule = "urn:genba-nippo:problem:business-rule";
    public const string NotFound = "urn:genba-nippo:problem:not-found";
    public const string InvalidState = "urn:genba-nippo:problem:invalid-state";
    public const string Duplicate = "urn:genba-nippo:problem:duplicate";
    public const string ConcurrencyConflict = "urn:genba-nippo:problem:concurrency-conflict";
}

/// <summary>
/// 例外を RFC 9457 の ProblemDetails に変換する（.NET 8 以降の IExceptionHandler）。
/// コントローラーには try-catch を書かず、業務エラーの HTTP ステータスはここで一元管理する。
/// </summary>
internal sealed class ProblemDetailsExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        (int status, string type, string title)? mapped = exception switch
        {
            DomainRuleException => (StatusCodes.Status400BadRequest, ProblemTypes.BusinessRule, "入力内容が業務ルールに合いません"),
            NotFoundException => (StatusCodes.Status404NotFound, ProblemTypes.NotFound, "データが見つかりません"),
            InvalidStateException => (StatusCodes.Status409Conflict, ProblemTypes.InvalidState, "今の状態ではこの操作はできません"),
            DuplicateException => (StatusCodes.Status409Conflict, ProblemTypes.Duplicate, "既に登録されています"),
            ConcurrencyConflictException or DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict, ProblemTypes.ConcurrencyConflict, "他の人が先に更新しました"),
            _ => null,
        };

        if (mapped is null)
        {
            return false; // 想定外の例外は既定の 500 処理に任せる（詳細は外に出さない）
        }

        var (status, type, title) = mapped.Value;
        var detail = exception is DbUpdateConcurrencyException
            ? new ConcurrencyConflictException().Message
            : exception.Message;
        logger.LogInformation("業務エラー {Status} {Type}: {Detail}", status, type, detail);

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Type = type, Title = title, Detail = detail },
        });
    }
}
