using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Chitti.Backend.Data;
using Chitti.Backend.Services;

namespace Chitti.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VoiceController : ControllerBase
{
    private readonly PersonalSecretaryDbContext _db;
    private readonly IChittiAgentService _agentService;

    public VoiceController(PersonalSecretaryDbContext db, IChittiAgentService agentService)
    {
        _db = db;
        _agentService = agentService;
    }

    [HttpGet("profiles")]
    public async Task<IActionResult> GetVoiceProfiles()
    {
        var profiles = await _db.VoiceProfiles
            .Where(v => v.is_active)
            .OrderBy(v => v.voice_profile_id)
            .ToListAsync();

        return Ok(profiles);
    }

    public record VoiceCommandDto(Guid? ConversationId, string SpokenText, string Locale = "ta-IN", int DurationMs = 2000);

    [HttpPost("command")]
    public async Task<IActionResult> ProcessVoiceCommand([FromBody] VoiceCommandDto command)
    {
        if (string.IsNullOrWhiteSpace(command.SpokenText))
            return BadRequest(new { error = "Spoken text cannot be empty." });

        var result = await _agentService.ProcessUserMessageAsync(command.ConversationId, command.SpokenText, contentType: "audio_transcript");

        // Log Voice Transcript
        var transcript = new VoiceTranscript
        {
            public_id = Guid.NewGuid(),
            message_id = result.MessageId,
            stt_engine = "whisper_local",
            transcript_text = command.SpokenText,
            detected_locale = command.Locale,
            confidence_score = 0.96m,
            audio_duration_ms = command.DurationMs,
            created_at = DateTime.UtcNow
        };
        _db.VoiceTranscripts.Add(transcript);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            transcript = command.SpokenText,
            chittiReply = result.ChittiReply,
            executedAction = result.ExecutedAction,
            targetSchema = result.TargetSchema,
            conversationId = result.ConversationPublicId,
            latencyMs = result.LatencyMs
        });
    }
}
