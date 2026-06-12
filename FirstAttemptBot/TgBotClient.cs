using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FirstAttemptBot;

public sealed class TelegramBotClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public TelegramBotClient(string token, HttpClient? httpClient = null, string? baseUrl = null)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Telegram bot token is required.", nameof(token));

        _http = httpClient ?? new HttpClient();
        _baseUrl = baseUrl?.TrimEnd('/') ?? $"https://api.telegram.org/bot{token}";
        if (httpClient is null)
        {
            _http.Timeout = TimeSpan.FromSeconds(70);
        }
    }

    public async Task<TgUser> GetMeAsync(CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync($"{_baseUrl}/getMe", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var api = JsonSerializer.Deserialize<TgApiResponse<TgUser>>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Telegram API returned invalid JSON.");

        if (!api.Ok || api.Result is null)
            throw new InvalidOperationException($"Telegram getMe failed: {api.Description ?? "unknown error"}");

        return api.Result;
    }

    public async Task<IReadOnlyList<TgUpdate>> GetUpdatesAsync(long offset, int timeoutSeconds, CancellationToken cancellationToken)
    {
        var url = new StringBuilder($"{_baseUrl}/getUpdates?timeout={timeoutSeconds}&allowed_updates=%5B%22message%22%5D");
        if (offset > 0)
            url.Append($"&offset={offset}");

        using var response = await _http.GetAsync(url.ToString(), cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var api = JsonSerializer.Deserialize<TgApiResponse<List<TgUpdate>>>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Telegram API returned invalid JSON.");

        if (!api.Ok)
            throw new InvalidOperationException($"Telegram getUpdates failed: {api.Description ?? "unknown error"}");

        return api.Result ?? [];
    }

    public async Task SendMessageAsync(long chatId, string text, string? parseMode, CancellationToken cancellationToken)
    {
        var request = new SendMessageRequest(chatId, text, parseMode);
        var payload = JsonSerializer.Serialize(request, JsonOptions);

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync($"{_baseUrl}/sendMessage", content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() => _http.Dispose();
}
