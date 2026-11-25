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
                title = userPrompt.Length > 40 ? userPrompt[..40] + "..." : userPrompt,
                active_module = "general",
                conversation_mode = contentType == "audio_transcript" ? "voice_chat" : "text",
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow
            };
            _db.Conversations.Add(conv);
            await _db.SaveChangesAsync();
        }

        // 2. Load recent conversation history (last 8 messages)
        var recentMsgs = await _db.Messages
            .Where(m => m.conversation_id == conv.conversation_id)
            .OrderByDescending(m => m.message_id)
            .Take(8)
            .ToListAsync();
        recentMsgs.Reverse();
        var historyText = string.Join("\n", recentMsgs.Select(m => $"{m.sender_type}: {m.message_content}"));

        // 3. Save User Message
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

        // 4. Build Live DB Snapshot for context-aware replies
        var liveContext = await BuildLiveSnapshotAsync();

        // 5. Invoke Chitti's AI Core (Ollama local / built-in smart engine)
        var aiResponse = await _ollamaService.GenerateResponseAsync(userPrompt, historyText, conv.active_module, liveContext);

        // 6. Save Chitti Assistant Response Message
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

        conv.updated_at = DateTime.UtcNow;

        // 7. Save AI Usage Log
        _db.AiUsageLogs.Add(new AiUsageLog
        {
            conversation_id = conv.conversation_id,
            model_provider = "chitti_local",
            model_name = "qwen2.5:7b/rules",
            prompt_tokens = aiResponse.PromptTokens,
            completion_tokens = aiResponse.CompletionTokens,
            latency_ms = aiResponse.LatencyMs,
            cost_usd = 0.00m,
            logged_at = DateTime.UtcNow
        });

        // 8. Execute Extracted Action If Any
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

            actionSucceeded = await ExecuteDomainActionAsync(aiResponse.TargetSchema, aiResponse.DetectedIntent, aiResponse.ActionPayloadJson);

            _db.AiActionExecutions.Add(new AiActionExecution
            {
                public_id = Guid.NewGuid(),
                action_request_id = actionReq.action_request_id,
                execution_status = actionSucceeded ? "success" : "failed",
                result_payload_json = JsonSerializer.Serialize(new { status = actionSucceeded ? "executed" : "no_db_write", timestamp = DateTime.UtcNow }),
                executed_at = DateTime.UtcNow,
                execution_duration_ms = 10
            });
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

    private async Task<LiveDbSnapshot> BuildLiveSnapshotAsync()
    {
        var today = DateTime.UtcNow.Date;

        var waterToday = await _db.WaterLogs
            .Where(w => w.consumed_at >= today)
            .SumAsync(w => (int?)w.quantity_ml) ?? 0;

        var expensesToday = await _db.HabitExpenses
            .Where(e => e.expense_date >= today)
            .SumAsync(e => (decimal?)e.amount) ?? 0m;

        var pendingTasks = await _db.ProfessionTasks
            .Where(t => t.is_active && t.status != "completed")
            .OrderByDescending(t => t.priority)
            .Select(t => t.task_title)
            .Take(5)
            .ToListAsync();

        var pendingCount = await _db.ProfessionTasks
            .CountAsync(t => t.is_active && t.status != "completed");

        var todayAtt = await _db.Attendances
            .FirstOrDefaultAsync(a => a.employee_id == 1 && a.work_date == today);

        var bizIncome = await _db.BusinessTransactions
            .Where(t => t.transaction_type == "income")
            .SumAsync(t => (decimal?)t.amount) ?? 0m;

        var bizExpense = await _db.BusinessTransactions
            .Where(t => t.transaction_type == "expense")
            .SumAsync(t => (decimal?)t.amount) ?? 0m;

        var vehicleCount = await _db.Vehicles.CountAsync();

        return new LiveDbSnapshot(
            WaterTodayMl: waterToday,
            WaterTargetMl: 3500,
            PersonalExpensesToday: expensesToday,
            PendingTasksCount: pendingCount,
            PendingTaskTitles: pendingTasks,
            IsLoggedInToday: todayAtt != null,
            LoginAtUtc: todayAtt?.login_at,
            BusinessIncomeTotal: bizIncome,
            BusinessExpenseTotal: bizExpense,
            VehiclesCount: vehicleCount
        );
    }

    private async Task<bool> ExecuteDomainActionAsync(string schema, string action, string? payloadJson)
    {
        try
        {
            if (string.IsNullOrEmpty(payloadJson)) return true; // query-only action
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement.TryGetProperty("payload", out var pl) ? pl : doc.RootElement;

            // HABIT: Log Water
            if (schema == "habit" && action == "log_water")
            {
                int ml = root.TryGetProperty("quantity_ml", out var q) ? q.GetInt32() : 250;
                _db.WaterLogs.Add(new WaterLog
                {
                    public_id = Guid.NewGuid(),
                    profile_id = 1,
                    quantity_ml = ml,
                    consumed_at = DateTime.UtcNow,
                    created_at = DateTime.UtcNow
                });
                return true;
            }

            // HABIT: Record Expense
            if (schema == "habit" && action == "record_expense")
            {
                decimal amount = root.TryGetProperty("amount", out var a) ? a.GetDecimal() : 50.0m;
                string remarks = root.TryGetProperty("remarks", out var r) ? r.GetString() ?? "Personal Expense" : "Personal Expense";
                _db.HabitExpenses.Add(new HabitExpense
                {
                    public_id = Guid.NewGuid(),
                    category_id = 1,
                    amount = amount,
                    expense_date = DateTime.UtcNow,
                    payment_method = "upi",
                    is_essential = false,
                    remarks = remarks
                });
                return true;
            }

            // HABIT: Log Sleep
            if (schema == "habit" && action == "log_sleep")
            {
                var sleepAt = root.TryGetProperty("sleep_at", out var s) && s.TryGetDateTime(out var st) ? st : DateTime.UtcNow.Date.AddHours(1);
                var wakeAt  = root.TryGetProperty("wake_at", out var w) && w.TryGetDateTime(out var wt) ? wt : DateTime.UtcNow.Date.AddHours(7);
                byte quality = root.TryGetProperty("quality", out var q) ? q.GetByte() : (byte)7;
                _db.SleepLogs.Add(new SleepLog
                {
                    public_id = Guid.NewGuid(),
                    profile_id = 1,
                    sleep_at = sleepAt,
                    wake_at = wakeAt,
                    sleep_quality = quality
                });
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

            // PROFESSION: Create Task
            if (schema == "profession" && action == "create_task")
            {
                var title = root.TryGetProperty("task_title", out var t) ? t.GetString() ?? "New Task" : "New Task";
                byte priority = root.TryGetProperty("priority", out var p) ? p.GetByte() : (byte)2;
                _db.ProfessionTasks.Add(new ProfessionTask
                {
                    public_id = Guid.NewGuid(),
                    employee_id = 1,
                    task_title = title,
                    task_source = "chitti_chat",
                    priority = priority,
                    status = "pending",
                    planned_date = DateTime.UtcNow.Date,
                    created_at = DateTime.UtcNow
                });
                return true;
            }

            // PROFESSION: Complete Task
            if (schema == "profession" && action == "complete_task")
            {
                var hint = root.TryGetProperty("title_hint", out var h) ? (h.GetString() ?? "").ToLowerInvariant() : "";
                var pending = await _db.ProfessionTasks
                    .Where(t => t.is_active && t.status != "completed")
                    .OrderByDescending(t => t.created_at)
                    .ToListAsync();

                var match = string.IsNullOrWhiteSpace(hint)
                    ? pending.FirstOrDefault()
                    : pending.FirstOrDefault(t => t.task_title.ToLowerInvariant().Contains(hint)) ?? pending.FirstOrDefault();

                if (match != null)
                {
                    match.status = "completed";
                    match.completed_at = DateTime.UtcNow;
                    return true;
                }
                return false;
            }

            // PROFESSION: Generate Daily / EOD Report
            if (schema == "profession" && action == "generate_eod_report")
            {
                var reportType = root.TryGetProperty("report_type", out var rt) ? rt.GetString() ?? "eod_report" : "eod_report";
                var content = root.TryGetProperty("content", out var c) ? c.GetString() ?? "Daily Report" : "Daily Report";
                _db.DailyReports.Add(new DailyReport
                {
                    public_id = Guid.NewGuid(),
                    employee_id = 1,
                    report_date = DateTime.UtcNow.Date,
                    report_type = reportType,
                    report_content = content,
                    review_status = "submitted",
                    created_at = DateTime.UtcNow
                });
                return true;
            }

            // BUSINESS: Record Transaction
            if (schema == "business" && action == "record_transaction")
            {
                var txType = root.TryGetProperty("type", out var tt) ? tt.GetString() ?? "income" : "income";
                var amount = root.TryGetProperty("amount", out var am) ? am.GetDecimal() : 0m;
                var desc = root.TryGetProperty("description", out var d) ? d.GetString() : "Business Transaction";
                var method = root.TryGetProperty("payment_method", out var pm) ? pm.GetString() ?? "upi" : "upi";
                _db.BusinessTransactions.Add(new BusinessTransaction
                {
                    public_id = Guid.NewGuid(),
                    business_id = 1,
                    category_id = 1,
                    transaction_type = txType,
                    amount = amount,
                    transaction_date = DateTime.UtcNow.Date,
                    payment_method = method,
                    description = desc
                });
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
