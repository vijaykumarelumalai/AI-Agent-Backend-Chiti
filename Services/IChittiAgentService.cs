namespace Chitti.Backend.Services;

public record AgentChatResult(
    Guid ConversationPublicId,
    long MessageId,
    Guid MessagePublicId,
    string UserPrompt,
    string ChittiReply,
    string? ExecutedAction,
    string? TargetSchema,
    bool ActionSucceeded,
    int LatencyMs
);

public interface IChittiAgentService
{
    Task<AgentChatResult> ProcessUserMessageAsync(Guid? conversationPublicId, string userPrompt, string contentType = "text");
}
