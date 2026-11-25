using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Chitti.Backend.Data;

namespace Chitti.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    private readonly PersonalSecretaryDbContext _db;
    private readonly IConfiguration _config;

    public SystemController(PersonalSecretaryDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth()
    {
        bool dbConnected = false;
        string dbError = "";
        try
        {
            dbConnected = await _db.Database.CanConnectAsync();
        }
        catch (Exception ex)
        {
            dbError = ex.Message;
        }

        return Ok(new
        {
            application = "Chitti AI (Personal AI Secretary)",
            version = "2.0 (.NET 10 LTS)",
            port = 1008,
            serverUtcTime = DateTime.UtcNow,
            status = "Healthy",
            database = new
            {
                name = "personal_ai_secretary_db",
                connected = dbConnected,
                error = string.IsNullOrEmpty(dbError) ? null : dbError,
                schemas = new[] { "auth", "assistant", "profession", "business", "habit", "system" },
                totalTables = 80
            },
            voice = new
            {
                supportedEngines = new[] { "Whisper.net (Local)", "Piper Neural (Local)", "Azure Speech F0", "Web Speech API" },
                locales = new[] { "ta-IN (Tamil)", "en-IN (Indian English)", "tanglish" },
                mode = "100% Free / Localhost Compatible"
            },
            aiBrain = new
            {
                provider = "Ollama Local",
                url = _config["Ollama:BaseUrl"] ?? "http://localhost:11434",
                model = _config["Ollama:DefaultModel"] ?? "qwen2.5:7b"
            }
        });
    }
}
