using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FirstAttemptBot;

public sealed class RemoteTextAssistant : ITextAssistant
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly string _model;

    public RemoteTextAssistant(string endpoint, string apiKey, string model, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("AI API endpoint is required.", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("AI API key is required.", nameof(apiKey));
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("AI model is required.", nameof(model));

        _endpoint = endpoint;
        _apiKey = apiKey;
        _model = model;
        _http = httpClient ?? new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(90);
    }

    public Task<string> SummarizeAsync(string text, CancellationToken cancellationToken)
        => SendPromptAsync(BuildSummarizePrompt(text), cancellationToken);

    public Task<string> CreateQuizAsync(string text, CancellationToken cancellationToken)
        => SendPromptAsync(BuildQuizPrompt(text), cancellationToken);

    public Task<string> BuildDefaultResponseAsync(string text, CancellationToken cancellationToken)
        => SendPromptAsync(BuildDefaultPrompt(text), cancellationToken);

    private async Task<string> SendPromptAsync(string prompt, CancellationToken cancellationToken)
    {
        var request = new ChatCompletionRequest(
            _model,
            new List<ChatMessage>
            {
                new("system", "You are a concise assistant for a hackathon demo. Reply in clear Markdown."),
                new("user", prompt)
            },
            0.3);

        var payload = JsonSerializer.Serialize(request, JsonOptions);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Remote AI request failed: {response.StatusCode} {body}");

        var result = JsonSerializer.Deserialize<ChatCompletionResponse>(body, JsonOptions)
                     ?? throw new InvalidOperationException("Remote AI returned invalid JSON.");

        var content = result.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Remote AI response did not contain text.");

        return content.Trim();
    }

    private static string BuildSummarizePrompt(string text) =>
        "Summarize the following text in 3 concise bullet points.\n" +
        "Then provide 3 short next steps.\n\n" +
        "Text:\n" + text;

    private static string BuildQuizPrompt(string text) =>
        "Create a mini quiz from the text below.\n" +
        "Give 3 questions and a short answer hint.\n\n" +
        "Text:\n" + text;

    private static string BuildDefaultPrompt(string text) =>
        "The user sent the message below.\n" +
        "Respond with a short helpful summary and 3 next steps.\n\n" +
        "Text:\n" + text;
}
