namespace FirstAttemptBot;

public interface ITextAssistant
{
    Task<string> SummarizeAsync(string text, CancellationToken cancellationToken);
    Task<string> CreateQuizAsync(string text, CancellationToken cancellationToken);
    Task<string> BuildDefaultResponseAsync(string text, CancellationToken cancellationToken);
}
