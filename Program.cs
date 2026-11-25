using Microsoft.EntityFrameworkCore;
using Chitti.Backend.Data;
using Chitti.Backend.Hubs;
using Chitti.Backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Explicitly bind to Port 1008 as specified by user
builder.WebHost.UseUrls("http://localhost:1008");

// 1. Database Context (SSMS / Microsoft SQL Server)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=localhost;Database=personal_ai_secretary_db;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;";

builder.Services.AddDbContext<PersonalSecretaryDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
    });
});

// 2. HTTP Clients & AI Services (100% Free Localhost AI via Ollama)
builder.Services.AddHttpClient<IOllamaService, OllamaService>(client =>
{
    var baseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<IChittiAgentService, ChittiAgentService>();

// 3. SignalR for Real-time Duplex Voice & Chat Streaming
builder.Services.AddSignalR();

// 4. CORS Policy for React Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("ChittiCorsPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173", // React Vite
                "http://localhost:3000", // React standard
                "http://localhost:8081"  // React Native Expo
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("ChittiCorsPolicy");

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

// Map SignalR Real-Time Voice & Chat Hubs
app.MapHub<ChittiVoiceHub>("/hubs/chitti-voice");
app.MapHub<ChittiChatHub>("/hubs/chitti-chat");

// Root test endpoint showing server banner
app.MapGet("/", () => Results.Ok(new
{
    agent = "Chitti AI (Personal AI Secretary)",
    platform = "ASP.NET Core .NET 10 LTS",
    port = 1008,
    status = "Active & Listening on port 1008",
    database = "personal_ai_secretary_db (80 Tables)",
    docs = "http://localhost:1008/openapi/v1.json"
}));

Console.WriteLine("====================================================================");
Console.WriteLine("  CHITTI AI SECRETARY BACKEND INITIALIZED SUCCESSFULLY             ");
Console.WriteLine("  Listening on Port: http://localhost:1008                          ");
Console.WriteLine("  Database: personal_ai_secretary_db (SSMS / Microsoft SQL Server)  ");
Console.WriteLine("  Voice Hub: http://localhost:1008/hubs/chitti-voice               ");
Console.WriteLine("====================================================================");

app.Run();
