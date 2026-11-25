using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chitti.Backend.Services;

public class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<OllamaService> _logger;

    public OllamaService(HttpClient httpClient, IConfiguration config, ILogger<OllamaService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<ChittiAiResponse> GenerateResponseAsync(string userPrompt, string conversationHistory = "", string? activeModule = "general")
    {
        var stopwatch = Stopwatch.StartNew();
        var baseUrl = _config["Ollama:BaseUrl"] ?? "http://localhost:11434";
        var model = _config["Ollama:DefaultModel"] ?? "qwen2.5:7b";

        var systemPrompt = @"You are Chitti, an enterprise-grade AI Personal Secretary, Work Manager, Business Assistant, and Lifestyle Coach.
You communicate warmly and fluently in Tamil, English, and Tanglish.
Your job is to understand instructions, summarize tasks, manage attendance, track business transactions/cashflow, and log lifestyle habits (sleep, water, expenses).

When the user asks you to record something, extract the action in JSON format at the end of your response inside <action>...</action> tags:
{
  ""schema"": ""profession"" | ""business"" | ""habit"",
  ""action"": ""create_task"" | ""record_attendance"" | ""record_expense"" | ""log_water"",
  ""payload"": { ...details... }
}
Always answer the user respectfully (e.g. 'Sure Vijay, unga request-ai execute panniten!')";

        try
        {
            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                stream = false
            };

            var response = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/chat", requestBody);
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
            _logger.LogWarning("Ollama local connection unavailable or failed ({Error}). Using rule-based fallback response engine.", ex.Message);
        }

        // Rule-based high-accuracy local fallback engine when Ollama is starting up or offline
        stopwatch.Stop();
        return GenerateRuleBasedFallback(userPrompt, (int)stopwatch.ElapsedMilliseconds);
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

                return new ChittiAiResponse(
                    ReplyText: cleanReply,
                    DetectedIntent: action,
                    TargetSchema: schema,
                    ActionPayloadJson: jsonStr,
                    PromptTokens: pTokens,
                    CompletionTokens: cTokens,
                    LatencyMs: latencyMs
                );
            }
            catch
            {
                // Fallback to raw text if parsing inner JSON failed
            }
        }

        return new ChittiAiResponse(
            ReplyText: rawMessage,
            DetectedIntent: null,
            TargetSchema: null,
            ActionPayloadJson: null,
            PromptTokens: pTokens,
            CompletionTokens: cTokens,
            LatencyMs: latencyMs
        );
    }

    private ChittiAiResponse GenerateRuleBasedFallback(string prompt, int latencyMs)
    {
        var lower = prompt.ToLower();

        // 1. Attendance detection
        if (lower.Contains("login") || lower.Contains("attendance") || lower.Contains("logout"))
        {
            var isLogout = lower.Contains("logout");
            var time = DateTime.Now.ToString("hh:mm tt");
            var reply = isLogout 
                ? $"Vanakkam! Ungal office logout ({time}) attendance table-la successfully record aagiruchu. Good work today!"
                : $"Vanakkam! Inniku ungal office login ({time}) profession.attendance table-la successfully record aagiruchu. Inniku task list-ai review pannalaama?";

            var actionJson = JsonSerializer.Serialize(new
            {
                schema = "profession",
                action = isLogout ? "record_logout" : "record_attendance",
                payload = new { work_date = DateTime.UtcNow.ToString("yyyy-MM-dd"), time = DateTime.UtcNow }
            });

            return new ChittiAiResponse(reply, "record_attendance", "profession", actionJson, 25, 45, latencyMs);
        }

        // 2. Water tracking detection
        if (lower.Contains("water") || lower.Contains("thanni") || lower.Contains("thannir") || lower.Contains("drink"))
        {
            var ml = 250;
            var match = Regex.Match(lower, @"(\d+)\s*(ml|litres|l)?");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var parsed))
            {
                ml = parsed > 10 ? parsed : parsed * 1000;
            }

            var reply = $"Super! Ungal {ml}ml water intake habit.water_logs table-la record aachu. Hydrated-ah irunga!";
            var actionJson = JsonSerializer.Serialize(new
            {
                schema = "habit",
                action = "log_water",
                payload = new { quantity_ml = ml, consumed_at = DateTime.UtcNow }
            });

            return new ChittiAiResponse(reply, "log_water", "habit", actionJson, 20, 35, latencyMs);
        }

        // 3. Tea / Snacks / Personal expense detection
        if (lower.Contains("tea") || lower.Contains("snack") || lower.Contains("coffee") || lower.Contains("spent") || lower.Contains("selavu") || lower.Contains("₹") || lower.Contains("rs"))
        {
            decimal amount = 50.0m;
            var match = Regex.Match(lower, @"(\d+(\.\d+)?)");
            if (match.Success && decimal.TryParse(match.Groups[1].Value, out var parsed))
            {
                amount = parsed;
            }

            var reply = $"Seri, ungal ₹{amount:0.00} spending habit.expenses table-la record pannitten. Monthly budget monitor pannittu irukken.";
            var actionJson = JsonSerializer.Serialize(new
            {
                schema = "habit",
                action = "record_expense",
                payload = new { amount = amount, remarks = prompt, is_essential = false }
            });

            return new ChittiAiResponse(reply, "record_expense", "habit", actionJson, 30, 40, latencyMs);
        }

        // Default General Assistant Greeting & Guidance
        var defaultReply = $"Vanakkam! Naan ungal Chitti AI Secretary. Ungal office tasks, family business transactions, and lifestyle habits (water, sleep, expenses) ellathaiyum naan manage panna ready. Enna command kudukka virumbureenga?";
        return new ChittiAiResponse(defaultReply, "general_chat", "assistant", null, 15, 30, latencyMs);
    }
}
