
using System.Text.Json.Serialization;

namespace FirstAttemptBot;

public sealed record TgApiResponse<T>(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("result")] T? Result,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("error_code")] int? ErrorCode = null);

public sealed record TgUpdate(
    [property: JsonPropertyName("update_id")] int UpdateId,
    [property: JsonPropertyName("message")] TgMessage? Message);

public sealed record TgMessage(
    [property: JsonPropertyName("message_id")] long MessageId,
    [property: JsonPropertyName("chat")] TgChat Chat,
    [property: JsonPropertyName("from")] TgUser? From,
    [property: JsonPropertyName("text")] string? Text);

public sealed record TgChat(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("type")] string? Type);

public sealed record TgUser(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("is_bot")] bool IsBot,
    [property: JsonPropertyName("first_name")] string FirstName,
    [property: JsonPropertyName("username")] string? Username);

public sealed record SendMessageRequest(
    [property: JsonPropertyName("chat_id")] long ChatId,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("parse_mode")] string? ParseMode = null,
    [property: JsonPropertyName("disable_web_page_preview")] bool DisableWebPagePreview = true);

public sealed record ChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

public sealed record ChatCompletionRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] List<ChatMessage> Messages,
    [property: JsonPropertyName("temperature")] double Temperature = 0.3);

public sealed record ChatCompletionResponse(
    [property: JsonPropertyName("choices")] List<ChatChoice>? Choices);

public sealed record ChatChoice(
    [property: JsonPropertyName("message")] ChatChoiceMessage? Message);

public sealed record ChatChoiceMessage(
    [property: JsonPropertyName("content")] string? Content);
