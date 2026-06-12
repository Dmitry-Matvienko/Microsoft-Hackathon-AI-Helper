using System.Text;

namespace FirstAttemptBot;

public static class Program
{
    private const int TelegramMessageLimit = 3900;

    public static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("TELEGRAM_BOT_TOKEN is missing.");
            Console.WriteLine("Set it in your environment or in Visual Studio launch settings.");
            return;
        }

        var assistant = CreateAssistant();
        using var bot = new TelegramBotClient(token);

        Console.WriteLine("Connecting to Telegram...");
        var me = await bot.GetMeAsync(cts.Token);
        Console.WriteLine($"Connected as @{me.Username ?? me.FirstName}");

        long offset = 0;
        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var updates = await bot.GetUpdatesAsync(offset, timeoutSeconds: 25, cts.Token);
                foreach (var update in updates)
                {
                    offset = update.UpdateId + 1;
                    if (update.Message?.Text is not string text)
                        continue;

                    var chatId = update.Message.Chat.Id;
                    var response = await HandleMessageAsync(text, assistant, cts.Token);

                    foreach (var chunk in SplitForTelegram(response))
                    {
                        await bot.SendMessageAsync(chatId, chunk, parseMode: null, cts.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[warn] {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);
            }
        }

        Console.WriteLine("Bot stopped.");
    }

    private static ITextAssistant CreateAssistant()
    {
        var mode = (Environment.GetEnvironmentVariable("AI_MODE") ?? "local").Trim().ToLowerInvariant();

        if (mode == "remote")
        {
            var endpoint = Environment.GetEnvironmentVariable("AI_API_URL")?.Trim();
            var key = Environment.GetEnvironmentVariable("AI_API_KEY")?.Trim();
            var model = Environment.GetEnvironmentVariable("AI_MODEL")?.Trim();

            if (!string.IsNullOrWhiteSpace(endpoint) &&
                !string.IsNullOrWhiteSpace(key) &&
                !string.IsNullOrWhiteSpace(model))
            {
                Console.WriteLine("AI mode: remote");
                return new RemoteTextAssistant(endpoint!, key!, model!);
            }

            Console.WriteLine("Remote AI settings are incomplete. Falling back to local mode.");
        }

        Console.WriteLine("AI mode: local");
        return new LocalTextAssistant();
    }

    private static async Task<string> HandleMessageAsync(string rawText, ITextAssistant assistant, CancellationToken cancellationToken)
    {
        var text = rawText.Trim();

        if (text.Equals("/start", StringComparison.OrdinalIgnoreCase))
        {
            return "FirstAttemptBot is live.\n\nSend me any text and I will summarize it.\n\nCommands\n- /summary <text>\n- /quiz <text>\n- /help\n- /about";
        }

        if (text.Equals("/help", StringComparison.OrdinalIgnoreCase))
        {
            return "Help\n\n- Send plain text to get a summary.\n- Use /summary to summarize specific text.\n- Use /quiz to turn text into a mini quiz.\n- Use /about to see the project description.";
        }

        if (text.Equals("/about", StringComparison.OrdinalIgnoreCase))
        {
            return "FirstAttemptBot\n\nA minimal .NET console Telegram bot built for the hackathon.\nIt demonstrates a complete flow:\nuser message -> bot -> assistant -> reply.";
        }

        if (text.StartsWith("/summary", StringComparison.OrdinalIgnoreCase))
        {
            var payload = ExtractPayload(text, "/summary");
            if (string.IsNullOrWhiteSpace(payload))
                return "Please send some text after /summary.";

            return await assistant.SummarizeAsync(payload, cancellationToken);
        }

        if (text.StartsWith("/quiz", StringComparison.OrdinalIgnoreCase))
        {
            var payload = ExtractPayload(text, "/quiz");
            if (string.IsNullOrWhiteSpace(payload))
                return "Please send some text after /quiz.";

            return await assistant.CreateQuizAsync(payload, cancellationToken);
        }

        if (text.Length < 6)
            return "Send a longer message or use /summary.";

        return await assistant.BuildDefaultResponseAsync(text, cancellationToken);
    }

    private static string ExtractPayload(string text, string command)
    {
        var payload = text.Substring(command.Length).Trim();
        if (payload.StartsWith("@"))
        {
            var spaceIndex = payload.IndexOf(' ');
            payload = spaceIndex >= 0 ? payload[(spaceIndex + 1)..].Trim() : string.Empty;
        }

        return payload.Trim();
    }

    private static IEnumerable<string> SplitForTelegram(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        for (var i = 0; i < text.Length; i += TelegramMessageLimit)
        {
            var len = Math.Min(TelegramMessageLimit, text.Length - i);
            yield return text.Substring(i, len);
        }
    }
}
