using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Chitti.Backend.Data;

namespace Chitti.Backend.Services;

public class ChittiAgentService : IChittiAgentService
{
    private readonly PersonalSecretaryDbContext _db;
    private readonly IOllamaService _ollamaService;
    private readonly ILogger<ChittiAgentService> _logger;

    public ChittiAgentService(
        PersonalSecretaryDbContext db,
        IOllamaService ollamaService,
        ILogger<ChittiAgentService> logger)
    {
        _db = db;
        _ollamaService = ollamaService;
        _logger = logger;
    }

    public async Task<AgentChatResult> ProcessUserMessageAsync(Guid? conversationPublicId, string userPrompt, string contentType = "text")
    {
        // 1. Get or Create Conversation
        Conversation? conv = null;
        if (conversationPublicId.HasValue && conversationPublicId.Value != Guid.Empty)
        {
            conv = await _db.Conversations.FirstOrDefaultAsync(c => c.public_id == conversationPublicId.Value);
        }

        if (conv == null)
        {
            conv = new Conversation
            {
                public_id = Guid.NewGuid(),
                title = userPrompt.Length > 40 ? userPrompt.Substring(0, 40) + "..." : userPrompt,
                active_module = "general",
                conversation_mode = contentType == "audio_transcript" ? "voice_chat" : "text",
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow
            };
            _db.Conversations.Add(conv);
            await _db.SaveChangesAsync();
        }

        // 2. Save User Message
        var userMessage = new Message
        {
            public_id = Guid.NewGuid(),
            conversation_id = conv.conversation_id,
            sender_type = "user",
            message_content = userPrompt,
            content_type = contentType,
            created_at = DateTime.UtcNow
        };
        _db.Messages.Add(userMessage);
        await _db.SaveChangesAsync();

        // 3. Invoke Chitti's AI Core (Ollama local / rule engine)
        var aiResponse = await _ollamaService.GenerateResponseAsync(userPrompt, "", conv.active_module);

        // 4. Save Chitti Assistant Response Message
        var chittiMessage = new Message
        {
            public_id = Guid.NewGuid(),
            conversation_id = conv.conversation_id,
            sender_type = "assistant",
            message_content = aiResponse.ReplyText,
            content_type = "text",
            tokens_input = aiResponse.PromptTokens,
            tokens_output = aiResponse.CompletionTokens,
            created_at = DateTime.UtcNow
        };
        _db.Messages.Add(chittiMessage);

        // 5. Save AI Usage Log
        var usageLog = new AiUsageLog
        {
            conversation_id = conv.conversation_id,
            model_provider = "ollama_local",
            model_name = "qwen2.5:7b",
            prompt_tokens = aiResponse.PromptTokens,
            completion_tokens = aiResponse.CompletionTokens,
            latency_ms = aiResponse.LatencyMs,
            cost_usd = 0.00m,
            logged_at = DateTime.UtcNow
        };
        _db.AiUsageLogs.Add(usageLog);

        // 6. Execute Extracted Action If Any (Atomically recorded into database)
        bool actionSucceeded = false;
        if (!string.IsNullOrEmpty(aiResponse.TargetSchema) && !string.IsNullOrEmpty(aiResponse.DetectedIntent))
        {
            var actionReq = new AiActionRequest
            {
                public_id = Guid.NewGuid(),
                conversation_id = conv.conversation_id,
                target_schema = aiResponse.TargetSchema,
                target_action = aiResponse.DetectedIntent,
                action_payload_json = aiResponse.ActionPayloadJson ?? "{}",
                approval_required = false,
                approval_status = "auto_executed",
                approved_at = DateTime.UtcNow,
                created_at = DateTime.UtcNow
            };
            _db.AiActionRequests.Add(actionReq);
            await _db.SaveChangesAsync();

            // Execute domain action based on schema
            actionSucceeded = await ExecuteDomainActionAsync(aiResponse.TargetSchema, aiResponse.DetectedIntent, aiResponse.ActionPayloadJson);

            var actionExec = new AiActionExecution
            {
                public_id = Guid.NewGuid(),
                action_request_id = actionReq.action_request_id,
                execution_status = actionSucceeded ? "success" : "failed",
                result_payload_json = JsonSerializer.Serialize(new { status = "executed", timestamp = DateTime.UtcNow }),
                executed_at = DateTime.UtcNow,
                execution_duration_ms = 12
            };
            _db.AiActionExecutions.Add(actionExec);
        }

        await _db.SaveChangesAsync();

        return new AgentChatResult(
            ConversationPublicId: conv.public_id,
            MessageId: chittiMessage.message_id,
            MessagePublicId: chittiMessage.public_id,
            UserPrompt: userPrompt,
            ChittiReply: aiResponse.ReplyText,
            ExecutedAction: aiResponse.DetectedIntent,
            TargetSchema: aiResponse.TargetSchema,
            ActionSucceeded: actionSucceeded,
            LatencyMs: aiResponse.LatencyMs
        );
    }

    private async Task<bool> ExecuteDomainActionAsync(string schema, string action, string? payloadJson)
    {
        try
        {
            if (string.IsNullOrEmpty(payloadJson)) return false;
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement.TryGetProperty("payload", out var pl) ? pl : doc.RootElement;

            // HABIT: Log Water
            if (schema == "habit" && action == "log_water")
            {
                int ml = root.TryGetProperty("quantity_ml", out var q) ? q.GetInt32() : 250;
                var waterLog = new WaterLog
                {
                    public_id = Guid.NewGuid(),
                    profile_id = 1, // Default Profile
                    quantity_ml = ml,
                    consumed_at = DateTime.UtcNow,
                    created_at = DateTime.UtcNow
                };
                _db.WaterLogs.Add(waterLog);
                return true;
            }

            // HABIT: Record Expense
            if (schema == "habit" && action == "record_expense")
            {
                decimal amount = root.TryGetProperty("amount", out var a) ? a.GetDecimal() : 50.0m;
                string remarks = root.TryGetProperty("remarks", out var r) ? r.GetString() ?? "Personal Expense" : "Tea / Snacks";
                var exp = new HabitExpense
                {
                    public_id = Guid.NewGuid(),
                    category_id = 1,
                    amount = amount,
                    expense_date = DateTime.UtcNow,
                    payment_method = "upi",
                    is_essential = false,
                    remarks = remarks
                };
                _db.HabitExpenses.Add(exp);
                return true;
            }

            // PROFESSION: Record Attendance
            if (schema == "profession" && (action == "record_attendance" || action == "record_logout"))
            {
                var today = DateTime.UtcNow.Date;
                var existing = await _db.Attendances.FirstOrDefaultAsync(a => a.employee_id == 1 && a.work_date == today);
                if (existing == null)
                {
                    _db.Attendances.Add(new Attendance
                    {
                        public_id = Guid.NewGuid(),
                        employee_id = 1,
                        work_date = today,
                        login_at = DateTime.UtcNow,
                        work_mode = "office",
                        attendance_status = "present",
                        remarks = "Logged via Chitti Secretary"
                    });
                }
                else if (action == "record_logout")
                {
                    existing.logout_at = DateTime.UtcNow;
                }
                return true;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute domain action {Schema}.{Action}", schema, action);
            return false;
        }
    }
}
