using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chitti.Backend.Services;

public class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<OllamaService> _logger;

    // Avoid 4-second TCP timeout on every message when Ollama is not installed
    private static DateTime _ollamaRetryAfterUtc = DateTime.MinValue;

    public OllamaService(HttpClient httpClient, IConfiguration config, ILogger<OllamaService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<ChittiAiResponse> GenerateResponseAsync(
        string userPrompt,
        string conversationHistory = "",
        string? activeModule = "general",
        LiveDbSnapshot? liveContext = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var baseUrl = _config["Ollama:BaseUrl"] ?? "http://localhost:11434";
        var model = _config["Ollama:DefaultModel"] ?? "qwen2.5:7b";

        var contextSummary = liveContext == null
            ? "No live DB stats loaded."
            : $"Water today: {liveContext.WaterTodayMl}/{liveContext.WaterTargetMl} ml | " +
              $"Personal expenses today: Rs.{liveContext.PersonalExpensesToday:0.00} | " +
              $"Pending IT tasks ({liveContext.PendingTasksCount}): {string.Join(", ", liveContext.PendingTaskTitles)} | " +
              $"Office logged in today: {liveContext.IsLoggedInToday} | " +
              $"Business income: Rs.{liveContext.BusinessIncomeTotal:0.00}, expense: Rs.{liveContext.BusinessExpenseTotal:0.00}, vehicles: {liveContext.VehiclesCount}";

        var systemPrompt = $@"You are Chitti, a warm, intelligent Personal AI Secretary, IT Work Manager, Business Assistant, and Lifestyle Coach.
You communicate fluently in Tamil, English, and Tanglish.
Live user status right now: {contextSummary}
Recent conversation:
{conversationHistory}

When the user asks to record or change data, append an <action>...</action> JSON block at the end:
{{
  ""schema"": ""profession"" | ""business"" | ""habit"",
  ""action"": ""create_task"" | ""complete_task"" | ""record_attendance"" | ""record_logout"" | ""generate_eod_report"" | ""record_expense"" | ""log_water"" | ""log_sleep"" | ""record_transaction"",
  ""payload"": {{ ... }}
}}
Always answer respectfully using the live status numbers when relevant.";

        if (DateTime.UtcNow >= _ollamaRetryAfterUtc)
        {
            try
            {
                var requestBody = new
                {
                    model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    stream = false
                };

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/chat", requestBody, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseContent);
                    var root = doc.RootElement;
                    var rawMessage = root.GetProperty("message").GetProperty("content").GetString() ?? "";

                    stopwatch.Stop();
                    var promptTokens = root.TryGetProperty("prompt_eval_count", out var pe) ? pe.GetInt32() : 0;
                    var compTokens = root.TryGetProperty("eval_count", out var ec) ? ec.GetInt32() : 0;

                    return ParseChittiResponse(rawMessage, promptTokens, compTokens, (int)stopwatch.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                _ollamaRetryAfterUtc = DateTime.UtcNow.AddSeconds(60);
                _logger.LogWarning("Ollama unavailable ({Error}). Using built-in Chitti smart engine for 60s.", ex.Message);
            }
        }

        stopwatch.Stop();
        return GenerateSmartLocalResponse(userPrompt, liveContext, (int)stopwatch.ElapsedMilliseconds);
    }

    private ChittiAiResponse ParseChittiResponse(string rawMessage, int pTokens, int cTokens, int latencyMs)
    {
        var actionMatch = Regex.Match(rawMessage, @"<action>(.*?)</action>", RegexOptions.Singleline);
        if (actionMatch.Success)
        {
            try
            {
                var jsonStr = actionMatch.Groups[1].Value.Trim();
                using var doc = JsonDocument.Parse(jsonStr);
                var schema = doc.RootElement.GetProperty("schema").GetString();
                var action = doc.RootElement.GetProperty("action").GetString();
                var cleanReply = rawMessage.Replace(actionMatch.Value, "").Trim();

                return new ChittiAiResponse(cleanReply, action, schema, jsonStr, pTokens, cTokens, latencyMs);
            }
            catch
            {
                // Fallback to raw text
            }
        }

        return new ChittiAiResponse(rawMessage, null, null, null, pTokens, cTokens, latencyMs);
    }

    private ChittiAiResponse GenerateSmartLocalResponse(string prompt, LiveDbSnapshot? ctx, int latencyMs)
    {
        var lower = prompt.ToLowerInvariant().Trim();

        // ── 1. QUERY: Pending tasks / Work status ──────────────────────────────
        if ((lower.Contains("task") || lower.Contains("work")) &&
            (lower.Contains("enna") || lower.Contains("list") || lower.Contains("show") ||
             lower.Contains("kaattu") || lower.Contains("pending") || lower.Contains("what") ||
             lower.Contains("status") || lower.Contains("iruku")))
        {
            if (ctx == null || ctx.PendingTasksCount == 0)
            {
                return new ChittiAiResponse(
                    "Inniku pending IT tasks edhuvum illa! Pudhu task add pannanum-na 'add task: <task name>' nu sollunga.",
                    "query_tasks", "profession", null, 20, 35, latencyMs);
            }

            var list = string.Join(", ", ctx.PendingTaskTitles.Select((t, i) => $"{i + 1}) {t}"));
            return new ChittiAiResponse(
                $"Unga kitta {ctx.PendingTasksCount} pending task(s) irukku: {list}. Edhaavadhu complete pannanum-na 'complete task <name>' nu sollunga!",
                "query_tasks", "profession", null, 25, 45, latencyMs);
        }

        // ── 2. QUERY: Water / Expense / Today's summary ────────────────────────
        if ((lower.Contains("summary") || lower.Contains("status") || lower.Contains("evlo") ||
             lower.Contains("how much") || lower.Contains("today") || lower.Contains("inniku")) &&
            (lower.Contains("water") || lower.Contains("expense") || lower.Contains("selavu") ||
             lower.Contains("summary") || lower.Contains("day") || lower.Contains("habit")))
        {
            if (ctx != null)
            {
                var attText = ctx.IsLoggedInToday ? "Office Logged In ✓" : "Office Login pending";
                var reply = $"Inniku unga summary: Water {ctx.WaterTodayMl}/{ctx.WaterTargetMl} ml | " +
                            $"Personal Expenses ₹{ctx.PersonalExpensesToday:0.00} | " +
                            $"Pending Tasks: {ctx.PendingTasksCount} | {attText}.";
                return new ChittiAiResponse(reply, "daily_summary", "habit", null, 25, 50, latencyMs);
            }
        }

        // ── 3. QUERY: Business summary / Vehicles ──────────────────────────────
        if ((lower.Contains("business") || lower.Contains("vehicle") || lower.Contains("truck") ||
             lower.Contains("income") || lower.Contains("profit")) &&
            (lower.Contains("summary") || lower.Contains("kaattu") || lower.Contains("show") ||
             lower.Contains("list") || lower.Contains("enna") || lower.Contains("status") ||
             lower.Contains("evlo") || lower.Contains("how")))
        {
            if (ctx != null)
            {
                var net = ctx.BusinessIncomeTotal - ctx.BusinessExpenseTotal;
                var reply = $"Business Summary: Total Income ₹{ctx.BusinessIncomeTotal:0.00} | " +
                            $"Expenses ₹{ctx.BusinessExpenseTotal:0.00} | Net ₹{net:0.00} | " +
                            $"Registered Vehicles: {ctx.VehiclesCount}. Pudhu entry poda 'business income 5000 <note>' nu sollunga!";
                return new ChittiAiResponse(reply, "query_business", "business", null, 25, 50, latencyMs);
            }
        }

        // ── 4. ACTION: EOD / Morning Report ────────────────────────────────────
        if (lower.Contains("eod") || lower.Contains("morning report") || lower.Contains("daily report"))
        {
            var isEod = lower.Contains("eod");
            var pendingInfo = ctx != null ? $"{ctx.PendingTasksCount} pending task(s)" : "tasks reviewed";
            var reportType = isEod ? "eod_report" : "morning_plan";
            var content = isEod
                ? $"EOD Summary ({DateTime.Now:dd MMM yyyy}): {pendingInfo}. Office status: {(ctx?.IsLoggedInToday == true ? "Present" : "Not logged in")}."
                : $"Morning Plan ({DateTime.Now:dd MMM yyyy}): {pendingInfo}. Ready for today!";

            var reply = isEod
                ? $"EOD Report ready & saved! Summary: {content}"
                : $"Morning Report ready & saved! Summary: {content}";

            var actionJson = JsonSerializer.Serialize(new
            {
                schema = "profession",
                action = "generate_eod_report",
                payload = new { report_type = reportType, content }
            });
            return new ChittiAiResponse(reply, "generate_eod_report", "profession", actionJson, 25, 45, latencyMs);
        }

        // ── 5. ACTION: Create IT Task ──────────────────────────────────────────
        if (lower.Contains("add task") || lower.Contains("new task") || lower.Contains("create task") ||
            (lower.Contains("task") && (lower.Contains("add") || lower.Contains("pannanum") || lower.Contains("create"))))
        {
            var title = Regex.Replace(prompt, @"(?i)(chitti,?|please|new task|add task|create task|task add pannu|pannu|[-:])", "").Trim();
            if (string.IsNullOrWhiteSpace(title)) title = "Follow up on pending work";

            byte priority = lower.Contains("urgent") || lower.Contains("high") ? (byte)3 : (byte)2;
            var reply = $"Done! Pudhu task \"{title}\" unga IT Work list-la save pannitten.";
            var actionJson = JsonSerializer.Serialize(new
            {
                schema = "profession",
                action = "create_task",
                payload = new { task_title = title, priority }
            });
            return new ChittiAiResponse(reply, "create_task", "profession", actionJson, 25, 45, latencyMs);
        }

        // ── 6. ACTION: Complete IT Task ────────────────────────────────────────
        if (lower.Contains("complete task") || lower.Contains("finish task") ||
            (lower.Contains("task") && (lower.Contains("mudichitten") || lower.Contains("done") || lower.Contains("completed"))))
        {
            var titleHint = Regex.Replace(prompt, @"(?i)(chitti,?|complete task|finish task|task|mudichitten|completed|done|[-:])", "").Trim();
            var reply = string.IsNullOrWhiteSpace(titleHint)
                ? "Super! Unga latest pending task-ai completed-nu mark pannitten."
                : $"Super! \"{titleHint}\" task-ai completed-nu update pannitten.";

            var actionJson = JsonSerializer.Serialize(new
            {
                schema = "profession",
                action = "complete_task",
                payload = new { title_hint = titleHint }
            });
            return new ChittiAiResponse(reply, "complete_task", "profession", actionJson, 20, 40, latencyMs);
        }

        // ── 7. ACTION: Office Attendance (Login / Logout) ──────────────────────
        if (lower.Contains("login") || lower.Contains("attendance") || lower.Contains("logout") || lower.Contains("office"))
        {
            var isLogout = lower.Contains("logout");
            var time = DateTime.Now.ToString("hh:mm tt");
            var reply = isLogout
                ? $"Vanakkam! Ungal office logout ({time}) record aagiruchu. Good work today!"
                : $"Vanakkam! Inniku ungal office login ({time}) record aagiruchu. Today's tasks review pannalaama?";

            var actionJson = JsonSerializer.Serialize(new
            {
                schema = "profession",
                action = isLogout ? "record_logout" : "record_attendance",
                payload = new { work_date = DateTime.UtcNow.ToString("yyyy-MM-dd"), time = DateTime.UtcNow }
            });
            return new ChittiAiResponse(reply, isLogout ? "record_logout" : "record_attendance", "profession", actionJson, 25, 45, latencyMs);
        }

        // ── 8. ACTION: Business Transaction (Income / Expense) ─────────────────
        if (lower.Contains("business") && (lower.Contains("income") || lower.Contains("expense") || lower.Contains("payment") || Regex.IsMatch(lower, @"\d+")))
        {
            var isExpense = lower.Contains("expense") || lower.Contains("diesel") || lower.Contains("petrol") || lower.Contains("spent");
            var txType = isExpense ? "expense" : "income";
            decimal amount = 1000m;
            var match = Regex.Match(lower, @"(\d+(\.\d+)?)");
            if (match.Success && decimal.TryParse(match.Groups[1].Value, out var parsed)) amount = parsed;

            var reply = $"Business {txType} ₹{amount:0.00} (\"{prompt}\") successfully record pannitten!";
            var actionJson = JsonSerializer.Serialize(new
            {
                schema = "business",
                action = "record_transaction",
                payload = new { type = txType, amount, description = prompt, payment_method = "upi" }
            });
            return new ChittiAiResponse(reply, "record_transaction", "business", actionJson, 25, 45, latencyMs);
        }

        // ── 9. ACTION: Water Tracking ──────────────────────────────────────────
        if (lower.Contains("water") || lower.Contains("thanni") || lower.Contains("thannir") || lower.Contains("drink"))
        {
            var ml = 250;
            var match = Regex.Match(lower, @"(\d+)\s*(ml|litres|l)?");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var parsed))
            {
                ml = parsed > 10 ? parsed : parsed * 1000;
            }

            var newTotal = (ctx?.WaterTodayMl ?? 0) + ml;
            var reply = $"Super! {ml}ml water log pannitten. Inniku total: {newTotal} / {ctx?.WaterTargetMl ?? 3500} ml. Hydrated-ah irunga!";
            var actionJson = JsonSerializer.Serialize(new
            {
                schema = "habit",
                action = "log_water",
                payload = new { quantity_ml = ml, consumed_at = DateTime.UtcNow }
            });
            return new ChittiAiResponse(reply, "log_water", "habit", actionJson, 20, 35, latencyMs);
        }

        // ── 10. ACTION: Personal Expense ───────────────────────────────────────
        if (lower.Contains("tea") || lower.Contains("snack") || lower.Contains("coffee") || lower.Contains("lunch") ||
            lower.Contains("dinner") || lower.Contains("breakfast") || lower.Contains("spent") ||
            lower.Contains("selavu") || lower.Contains("₹") || lower.Contains("rs"))
        {
            decimal amount = 50.0m;
            var match = Regex.Match(lower, @"(\d+(\.\d+)?)");
            if (match.Success && decimal.TryParse(match.Groups[1].Value, out var parsed)) amount = parsed;

            var newTotal = (ctx?.PersonalExpensesToday ?? 0m) + amount;
            var reply = $"Seri, ₹{amount:0.00} personal expense record pannitten. Inniku total expenses: ₹{newTotal:0.00}.";
            var actionJson = JsonSerializer.Serialize(new
            {
                schema = "habit",
                action = "record_expense",
                payload = new { amount, remarks = prompt, is_essential = false }
            });
            return new ChittiAiResponse(reply, "record_expense", "habit", actionJson, 30, 40, latencyMs);
        }

        // ── 11. ACTION + ADVICE: Sleep / Tiredness ─────────────────────────────
        if (lower.Contains("sleep") || lower.Contains("thung") || lower.Contains("thoong") ||
            lower.Contains("wake") || lower.Contains("weak up") || lower.Contains("tired") ||
            lower.Contains("tried") || lower.Contains("fatigue") || lower.Contains("nap"))
        {
            var sleepAt = DateTime.UtcNow.Date.AddHours(1);  // default 1 AM
            var wakeAt  = DateTime.UtcNow.Date.AddHours(7);  // default 7 AM
            var hours   = (wakeAt - sleepAt).TotalHours;

            var reply = $"Unga sleep log (~{hours:0.#} hrs) record pannitten. Less than 7 hours sleep + mild dehydration naala dhaan morning-la irundhu tired-ah feel aagum. " +
                        "Ippo udane: 1) 250-500ml water kudinga, 2) 5 mins light stretching / sunlight walk pannunga, 3) Night 10:30 PM-kulla thoonga try pannunga!";
            var actionJson = JsonSerializer.Serialize(new
            {
                schema = "habit",
                action = "log_sleep",
                payload = new { sleep_at = sleepAt, wake_at = wakeAt, quality = (byte)6 }
            });
            return new ChittiAiResponse(reply, "log_sleep", "habit", actionJson, 25, 65, latencyMs);
        }

        // ── 12. Help / Capabilities ────────────────────────────────────────────
        if (lower.Contains("help") || lower.Contains("enna panna") || lower.Contains("what can you") || lower.Contains("commands"))
        {
            var reply = "Naan unga Personal AI Secretary Chitti! Enna-llaam pannuven:\n" +
                        "• IT Work: 'office login', 'add task fix API bug', 'pending tasks enna?', 'EOD report kudu'\n" +
                        "• Business: 'business summary', 'business income 5000 client payment', 'business expense 1200 diesel'\n" +
                        "• Lifestyle: '250ml water kudichen', '₹50 tea selavu', 'slept 1 am to 7 am', 'today summary'";
            return new ChittiAiResponse(reply, "help_guide", "assistant", null, 20, 55, latencyMs);
        }

        // ── 13. Greetings ──────────────────────────────────────────────────────
        if (Regex.IsMatch(lower, @"^(hi|hello|hey|vanakkam|gm|good morning|good afternoon|good evening|good night)[!.\s]*$"))
        {
            var waterInfo = ctx != null ? $"Inniku water: {ctx.WaterTodayMl}ml, pending tasks: {ctx.PendingTasksCount}." : "";
            var reply = $"Vanakkam! Naan Chitti. {waterInfo} Enna help venum? ('help' nu type panna all commands kaattuven)";
            return new ChittiAiResponse(reply, "general_chat", "assistant", null, 15, 30, latencyMs);
        }

        // ── 14. Smart Conversational Fallback ──────────────────────────────────
        var defaultReply = $"Purinjidhu: \"{prompt}\". Idha naan task-ah add pannattuma ('add task: {prompt}'), expense-ah log pannattuma, illa 'today summary' paakkanuma? All commands paakka 'help' nu type pannunga!";
        return new ChittiAiResponse(defaultReply, "general_chat", "assistant", null, 15, 45, latencyMs);
    }
}
