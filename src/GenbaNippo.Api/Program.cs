using System.Text.Json.Serialization;
using GenbaNippo.Api.Application;
using GenbaNippo.Api.Infrastructure;
using GenbaNippo.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        // 列挙型を "Submitted" のような文字列でやり取りする（数値だとフロントで意味が読めない）
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // JSON の変換エラーで内部の型名（System.Nullable`1[...]）を利用者に見せない
        o.AllowInputFormatterExceptionMessages = false;
    });

// エラーはすべて ProblemDetails（RFC 9457）で返す
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
{
    if (ctx.ProblemDetails is Microsoft.AspNetCore.Mvc.ValidationProblemDetails validation)
    {
        validation.Title = "入力内容に誤りがあります";
        // フレームワーク由来の英語メッセージを日本語にそろえる（中身は ValidationMessageLocalizer）
        var localized = ValidationMessageLocalizer.Localize(validation.Errors);
        validation.Errors.Clear();
        foreach (var (key, messages) in localized)
        {
            validation.Errors[key] = messages;
        }
    }
});
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

// OpenAPI ドキュメント（/openapi/v1.json）。XML コメントは .NET 10 のソース生成で自動的に反映される
builder.Services.AddOpenApi(o => o.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "現場日報ボード API";
    document.Info.Description = "建設現場の作業日報・工程の進捗・承認フローを扱う API。エラーは RFC 9457 ProblemDetails で返す。";
    return Task.CompletedTask;
}));
// OpenAPI のスキーマ生成もコントローラーと同じく列挙型を文字列として扱う（ドキュメントと実際の応答をそろえる）
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOptions<ScheduleOptions>()
    .Bind(builder.Configuration.GetSection(ScheduleOptions.SectionName))
    .Validate(o => o.BehindTolerancePoints is >= 0 and <= 100, "Schedule:BehindTolerancePoints は 0〜100 で指定してください。")
    .Validate(o => TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZoneId, out _), "Schedule:TimeZoneId に存在しないタイムゾーンが指定されています。")
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<BusinessClock>();

builder.Services.AddDbContext<GenbaDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<SiteService>();
builder.Services.AddScoped<WorkItemService>();
builder.Services.AddScoped<DailyReportService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "現場日報ボード API"));
}

// `npm run build` したフロントを wwwroot から配信する（1プロセスで画面と API を出せる）
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

// 存在しない API は index.html ではなく 404 の ProblemDetails を返す
app.MapFallback("/api/{**path}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "API が見つかりません"));
// それ以外のパスは SPA のルーティングに任せる
app.MapFallbackToFile("index.html");

await app.InitializeDatabaseAsync();

app.Run();

/// <summary>結合テスト（WebApplicationFactory）から参照するための宣言。</summary>
public partial class Program;
