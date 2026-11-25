namespace Chitti.Backend.Services;

public record LiveDbSnapshot(
    int WaterTodayMl,
    int WaterTargetMl,
    decimal PersonalExpensesToday,
    int PendingTasksCount,
    List<string> PendingTaskTitles,
    bool IsLoggedInToday,
    DateTime? LoginAtUtc,
    decimal BusinessIncomeTotal,
    decimal BusinessExpenseTotal,
    int VehiclesCount
);

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
    Task<ChittiAiResponse> GenerateResponseAsync(
        string userPrompt,
        string conversationHistory = "",
        string? activeModule = "general",
        LiveDbSnapshot? liveContext = null);
}
