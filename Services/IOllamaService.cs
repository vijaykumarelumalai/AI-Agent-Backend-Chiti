namespace Chitti.Backend.Services;

public record ChittiAiResponse(
    string ReplyText,
    string? DetectedIntent,
    string? TargetSchema,
    string? ActionPayloadJson,
    int PromptTokens,
    int CompletionTokens,
    int LatencyMs
);

public interface IOllamaService
{
    Task<ChittiAiResponse> GenerateResponseAsync(string userPrompt, string conversationHistory = "", string? activeModule = "general");
}
