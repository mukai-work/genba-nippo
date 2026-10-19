using GenbaNippo.Api.Application;
using GenbaNippo.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenbaNippo.Api.Infrastructure;

/// <summary>
/// 例外を RFC 9457 の ProblemDetails に変換する（.NET 8 の IExceptionHandler）。
/// コントローラーには try-catch を書かず、業務エラーの HTTP ステータスはここで一元管理する。
/// </summary>
internal sealed class ProblemDetailsExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        (int status, string title)? mapped = exception switch
        {
            DomainRuleException => (StatusCodes.Status400BadRequest, "入力内容が業務ルールに合いません"),
            NotFoundException => (StatusCodes.Status404NotFound, "データが見つかりません"),
            InvalidStateException => (StatusCodes.Status409Conflict, "今の状態ではこの操作はできません"),
            DuplicateException => (StatusCodes.Status409Conflict, "既に登録されています"),
            ConcurrencyConflictException or DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict, "他の人が先に更新しました"),
            _ => null,
        };

        if (mapped is null)
        {
            return false; // 想定外の例外は既定の 500 処理に任せる（詳細は外に出さない）
        }

        var (status, title) = mapped.Value;
        var detail = exception is DbUpdateConcurrencyException
            ? new ConcurrencyConflictException().Message
            : exception.Message;
        logger.LogInformation("業務エラー {Status}: {Detail}", status, detail);

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail },
        });
    }
}
