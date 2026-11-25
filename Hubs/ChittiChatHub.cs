using Microsoft.AspNetCore.SignalR;
using Chitti.Backend.Services;

namespace Chitti.Backend.Hubs;

public class ChittiChatHub : Hub
{
    private readonly IChittiAgentService _agentService;

    public ChittiChatHub(IChittiAgentService agentService)
    {
        _agentService = agentService;
    }

    public async Task SendMessage(string conversationPublicId, string userMessage)
    {
        Guid? convId = Guid.TryParse(conversationPublicId, out var g) ? g : null;
        var result = await _agentService.ProcessUserMessageAsync(convId, userMessage);

        await Clients.Caller.SendAsync("ReceiveMessage", new
        {
            conversationId = result.ConversationPublicId,
            messageId = result.MessagePublicId,
            reply = result.ChittiReply,
            action = result.ExecutedAction,
            schema = result.TargetSchema,
            latency = result.LatencyMs
        });
    }
}
