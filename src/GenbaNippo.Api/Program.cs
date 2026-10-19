using System.Reflection;
using System.Text.Json.Serialization;
using GenbaNippo.Api.Application;
using GenbaNippo.Api.Infrastructure;
using GenbaNippo.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    // 列挙型を "Submitted" のような文字列でやり取りする（数値だとフロントで意味が読めない）
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// エラーはすべて ProblemDetails（RFC 9457）で返す
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
{
    if (ctx.ProblemDetails is Microsoft.AspNetCore.Mvc.ValidationProblemDetails)
    {
        ctx.ProblemDetails.Title = "入力内容に誤りがあります";
    }
});
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    o.IncludeXmlComments(xml);
});

builder.Services.AddOptions<ScheduleOptions>()
    .Bind(builder.Configuration.GetSection(ScheduleOptions.SectionName))
    .Validate(o => o.BehindTolerancePoints is >= 0 and <= 100, "Schedule:BehindTolerancePoints は 0〜100 で指定してください。")
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
    app.UseSwagger();
    app.UseSwaggerUI();
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
