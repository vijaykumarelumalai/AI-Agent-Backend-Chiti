using Microsoft.AspNetCore.SignalR;
using Chitti.Backend.Services;

namespace Chitti.Backend.Hubs;

public class ChittiVoiceHub : Hub
{
    private readonly IChittiAgentService _agentService;
    private readonly ILogger<ChittiVoiceHub> _logger;

    public ChittiVoiceHub(IChittiAgentService agentService, ILogger<ChittiVoiceHub> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    // 1. Client connects
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Voice client connected: {ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("ConnectionReady", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    // 2. Client sends voice transcript or audio chunk
    public async Task ProcessSpokenCommand(string transcript, string? conversationPublicId)
    {
        _logger.LogInformation("Voice transcript received: '{Transcript}'", transcript);
        await Clients.Caller.SendAsync("TranscriptReceived", transcript);

        Guid? convId = null;
        if (!string.IsNullOrEmpty(conversationPublicId) && Guid.TryParse(conversationPublicId, out var parsed))
        {
            convId = parsed;
        }

        var result = await _agentService.ProcessUserMessageAsync(convId, transcript, contentType: "audio_transcript");

        // Broadcast Chitti's voice text reply back to caller
        await Clients.Caller.SendAsync("ChittiVoiceResponse", new
        {
            replyText = result.ChittiReply,
            executedAction = result.ExecutedAction,
            targetSchema = result.TargetSchema,
            conversationId = result.ConversationPublicId,
            latencyMs = result.LatencyMs
        });
    }

    // 3. User interrupted Chitti (Barge-in detection)
    public async Task InterruptChitti()
    {
        _logger.LogInformation("User interrupted Chitti playback on {ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("PlaybackCancelled");
    }
}
