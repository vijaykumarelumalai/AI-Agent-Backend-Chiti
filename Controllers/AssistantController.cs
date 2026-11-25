using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Chitti.Backend.Data;
using Chitti.Backend.Services;

namespace Chitti.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssistantController : ControllerBase
{
    private readonly PersonalSecretaryDbContext _db;
    private readonly IChittiAgentService _agentService;

    public AssistantController(PersonalSecretaryDbContext db, IChittiAgentService agentService)
    {
        _db = db;
        _agentService = agentService;
    }

    public record ChatInputDto(Guid? ConversationId, string Message, string ContentType = "text");

    [HttpPost("chat")]
    public async Task<IActionResult> SendChat([FromBody] ChatInputDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Message))
            return BadRequest(new { error = "Message cannot be empty." });

        var result = await _agentService.ProcessUserMessageAsync(input.ConversationId, input.Message, input.ContentType);
        return Ok(result);
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var list = await _db.Conversations
            .OrderByDescending(c => c.updated_at)
            .Take(20)
            .Select(c => new
            {
                c.conversation_id,
                c.public_id,
                c.title,
                c.active_module,
                c.conversation_mode,
                c.updated_at
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("conversations/{id:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid id, [FromQuery] long? lastSeenMessageId = null, [FromQuery] int pageSize = 25)
    {
        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.public_id == id);
        if (conv == null) return NotFound(new { error = "Conversation not found" });

        var query = _db.Messages
            .Where(m => m.conversation_id == conv.conversation_id);

        if (lastSeenMessageId.HasValue)
        {
            query = query.Where(m => m.message_id < lastSeenMessageId.Value);
        }

        var messages = await query
            .OrderByDescending(m => m.message_id)
            .Take(pageSize)
            .Select(m => new
            {
                m.message_id,
                m.public_id,
                m.sender_type,
                m.message_content,
                m.content_type,
                m.created_at
            })
            .ToListAsync();

        return Ok(messages);
    }
}
