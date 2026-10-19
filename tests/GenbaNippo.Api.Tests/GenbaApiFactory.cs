using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace GenbaNippo.Api.Tests;

/// <summary>
/// テスト用に API を丸ごと起動する。
/// - DB は SQLite のインメモリ（本番と同じマイグレーションを当てる）
/// - 「今」は FakeTimeProvider で 2026-10-18 09:00（日本時間）に固定
/// </summary>
public sealed class GenbaApiFactory : WebApplicationFactory<Program>
{
    public static readonly DateOnly Today = new(2026, 10, 18);

    // インメモリ DB は最後の接続が閉じると消えるので、テスト中はこの接続を開いたままにしておく
    private readonly string _connectionString = $"Data Source=file:genba-{Guid.NewGuid():N}?mode=memory&cache=shared";
    private readonly SqliteConnection _keepAlive;

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero)); // = 日本時間 9:00

    public GenbaApiFactory()
    {
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("Database:SeedSampleData", "false");
        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Time));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAlive.Dispose();
        }
    }
}

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Options);
        return value ?? throw new InvalidOperationException("レスポンスが空です。");
    }

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object body) =>
        client.PostAsJsonAsync(url, body, Options);

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string url, object body) =>
        client.PutAsJsonAsync(url, body, Options);
}
