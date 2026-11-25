using Microsoft.EntityFrameworkCore;

namespace Chitti.Backend.Data;

public class PersonalSecretaryDbContext : DbContext
{
    public PersonalSecretaryDbContext(DbContextOptions<PersonalSecretaryDbContext> options)
        : base(options)
    {
    }

    // AUTH SCHEMA
    public DbSet<AuthUser> Users => Set<AuthUser>();
    public DbSet<AuthRole> Roles => Set<AuthRole>();

    // ASSISTANT SCHEMA (CHITTI)
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<VoiceProfile> VoiceProfiles => Set<VoiceProfile>();
    public DbSet<VoicePreference> VoicePreferences => Set<VoicePreference>();
    public DbSet<VoiceTranscript> VoiceTranscripts => Set<VoiceTranscript>();
    public DbSet<VoiceSessionEvent> VoiceSessionEvents => Set<VoiceSessionEvent>();
    public DbSet<AiActionRequest> AiActionRequests => Set<AiActionRequest>();
    public DbSet<AiActionExecution> AiActionExecutions => Set<AiActionExecution>();
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();

    // PROFESSION SCHEMA (IT WORK)
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<ProfessionProject> Projects => Set<ProfessionProject>();
    public DbSet<ProfessionTask> ProfessionTasks => Set<ProfessionTask>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<DailyReport> DailyReports => Set<DailyReport>();

    // BUSINESS SCHEMA
    public DbSet<BusinessEntity> Businesses => Set<BusinessEntity>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<BusinessTransaction> BusinessTransactions => Set<BusinessTransaction>();
    public DbSet<Receivable> Receivables => Set<Receivable>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    // HABIT SCHEMA (LIFESTYLE)
    public DbSet<HabitProfile> HabitProfiles => Set<HabitProfile>();
    public DbSet<HabitItem> Habits => Set<HabitItem>();
    public DbSet<HabitLog> HabitLogs => Set<HabitLog>();
    public DbSet<WaterLog> WaterLogs => Set<WaterLog>();
    public DbSet<SleepLog> SleepLogs => Set<SleepLog>();
    public DbSet<HabitExpense> HabitExpenses => Set<HabitExpense>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Map Schemas & Primary Keys
        modelBuilder.Entity<AuthUser>().ToTable("users", "auth").HasKey(e => e.user_id);
        modelBuilder.Entity<AuthRole>().ToTable("roles", "auth").HasKey(e => e.role_id);

        modelBuilder.Entity<Conversation>().ToTable("conversations", "assistant").HasKey(e => e.conversation_id);
        modelBuilder.Entity<Message>().ToTable("messages", "assistant").HasKey(e => e.message_id);
        modelBuilder.Entity<VoiceProfile>().ToTable("voice_profiles", "assistant").HasKey(e => e.voice_profile_id);
        modelBuilder.Entity<VoicePreference>().ToTable("voice_preferences", "assistant").HasKey(e => e.preference_id);
        modelBuilder.Entity<VoiceTranscript>().ToTable("voice_transcripts", "assistant").HasKey(e => e.transcript_id);
        modelBuilder.Entity<VoiceSessionEvent>().ToTable("voice_session_events", "assistant").HasKey(e => e.event_id);
        modelBuilder.Entity<AiActionRequest>().ToTable("ai_action_requests", "assistant").HasKey(e => e.action_request_id);
        modelBuilder.Entity<AiActionExecution>().ToTable("ai_action_executions", "assistant").HasKey(e => e.execution_id);
        modelBuilder.Entity<AiUsageLog>().ToTable("ai_usage_logs", "assistant").HasKey(e => e.usage_id);

        modelBuilder.Entity<Employee>().ToTable("employees", "profession").HasKey(e => e.employee_id);
        modelBuilder.Entity<ProfessionProject>().ToTable("projects", "profession").HasKey(e => e.project_id);
        modelBuilder.Entity<ProfessionTask>().ToTable("tasks", "profession").HasKey(e => e.task_id);
        modelBuilder.Entity<Attendance>().ToTable("attendance", "profession").HasKey(e => e.attendance_id);
        modelBuilder.Entity<DailyReport>().ToTable("daily_reports", "profession").HasKey(e => e.report_id);

        modelBuilder.Entity<BusinessEntity>().ToTable("businesses", "business").HasKey(e => e.business_id);
        modelBuilder.Entity<Customer>().ToTable("customers", "business").HasKey(e => e.customer_id);
        modelBuilder.Entity<BusinessTransaction>().ToTable("transactions", "business").HasKey(e => e.transaction_id);
        modelBuilder.Entity<Receivable>().ToTable("receivables", "business").HasKey(e => e.receivable_id);
        modelBuilder.Entity<Vehicle>().ToTable("vehicles", "business").HasKey(e => e.vehicle_id);

        modelBuilder.Entity<HabitProfile>().ToTable("profiles", "habit").HasKey(e => e.profile_id);
        modelBuilder.Entity<HabitItem>().ToTable("habits", "habit").HasKey(e => e.habit_id);
        modelBuilder.Entity<HabitLog>().ToTable("habit_logs", "habit").HasKey(e => e.habit_log_id);
        modelBuilder.Entity<WaterLog>().ToTable("water_logs", "habit").HasKey(e => e.water_log_id);
        modelBuilder.Entity<SleepLog>().ToTable("sleep_logs", "habit").HasKey(e => e.sleep_log_id);
        modelBuilder.Entity<HabitExpense>().ToTable("expenses", "habit").HasKey(e => e.expense_id);
    }
}

// -----------------------------------------------------------------------------
// ENTITY MODELS
// -----------------------------------------------------------------------------

public class AuthUser
{
    public long user_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public string email { get; set; } = string.Empty;
    public string? phone_number { get; set; }
    public string password_hash { get; set; } = string.Empty;
    public string preferred_language { get; set; } = "ta-IN";
    public bool is_active { get; set; } = true;
    public DateTime created_at { get; set; } = DateTime.UtcNow;
}

public class AuthRole
{
    public long role_id { get; set; }
    public string role_name { get; set; } = string.Empty;
    public string? description { get; set; }
}

public class Conversation
{
    public long conversation_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public Guid user_public_id { get; set; } = Guid.Empty;
    public string title { get; set; } = "New Conversation with Chitti";
    public string active_module { get; set; } = "general";
    public string conversation_mode { get; set; } = "text";
    public DateTime created_at { get; set; } = DateTime.UtcNow;
    public DateTime updated_at { get; set; } = DateTime.UtcNow;
}

public class Message
{
    public long message_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public long conversation_id { get; set; }
    public string sender_type { get; set; } = "user"; // user, assistant, tool, system
    public string message_content { get; set; } = string.Empty;
    public string content_type { get; set; } = "text";
    public string? audio_storage_path { get; set; }
    public int? duration_ms { get; set; }
    public int tokens_input { get; set; }
    public int tokens_output { get; set; }
    public DateTime created_at { get; set; } = DateTime.UtcNow;
    public DateTime? device_recorded_at { get; set; }
}

public class VoiceProfile
{
    public long voice_profile_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public string engine_name { get; set; } = "whisper_local";
    public string voice_code { get; set; } = "ta_IN-valluvar";
    public string display_name { get; set; } = string.Empty;
    public string gender { get; set; } = "male";
    public string locale { get; set; } = "ta-IN";
    public int sample_rate_hz { get; set; } = 22050;
    public bool is_offline_capable { get; set; } = true;
    public bool is_active { get; set; } = true;
    public DateTime created_at { get; set; } = DateTime.UtcNow;
}

public class VoicePreference
{
    public long preference_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public Guid user_public_id { get; set; }
    public long preferred_voice_id { get; set; }
    public string preferred_locale { get; set; } = "ta-IN";
    public decimal speech_rate { get; set; } = 1.00m;
    public decimal pitch { get; set; } = 1.00m;
    public bool auto_speak_responses { get; set; } = true;
    public bool interruption_enabled { get; set; } = true;
}

public class VoiceTranscript
{
    public long transcript_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public long message_id { get; set; }
    public string stt_engine { get; set; } = "whisper_local";
    public string transcript_text { get; set; } = string.Empty;
    public string detected_locale { get; set; } = "ta-IN";
    public decimal confidence_score { get; set; } = 0.95m;
    public int audio_duration_ms { get; set; }
    public string? audio_file_path { get; set; }
    public DateTime created_at { get; set; } = DateTime.UtcNow;
}

public class VoiceSessionEvent
{
    public long event_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public long conversation_id { get; set; }
    public string event_type { get; set; } = string.Empty;
    public string? event_details_json { get; set; }
    public int? latency_ms { get; set; }
    public DateTime occurred_at { get; set; } = DateTime.UtcNow;
}

public class AiActionRequest
{
    public long action_request_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public long conversation_id { get; set; }
    public string target_schema { get; set; } = string.Empty;
    public string target_action { get; set; } = string.Empty;
    public string action_payload_json { get; set; } = "{}";
    public bool approval_required { get; set; }
    public string approval_status { get; set; } = "pending";
    public DateTime? approved_at { get; set; }
    public DateTime created_at { get; set; } = DateTime.UtcNow;
}

public class AiActionExecution
{
    public long execution_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public long action_request_id { get; set; }
    public string execution_status { get; set; } = "success";
    public string? result_payload_json { get; set; }
    public string? error_message { get; set; }
    public DateTime executed_at { get; set; } = DateTime.UtcNow;
    public int execution_duration_ms { get; set; }
}

public class AiUsageLog
{
    public long usage_id { get; set; }
    public long conversation_id { get; set; }
    public string model_provider { get; set; } = "ollama_local";
    public string model_name { get; set; } = "qwen2.5:7b";
    public int prompt_tokens { get; set; }
    public int completion_tokens { get; set; }
    public int latency_ms { get; set; }
    public decimal cost_usd { get; set; } = 0.00m;
    public DateTime logged_at { get; set; } = DateTime.UtcNow;
}

public class Employee
{
    public long employee_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public Guid user_public_id { get; set; }
    public string employee_code { get; set; } = string.Empty;
    public string full_name { get; set; } = string.Empty;
    public string designation { get; set; } = string.Empty;
    public string department { get; set; } = string.Empty;
    public string company_name { get; set; } = string.Empty;
    public DateTime joining_date { get; set; }
    public string work_email { get; set; } = string.Empty;
    public bool is_active { get; set; } = true;
}

public class ProfessionProject
{
    public long project_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public long employee_id { get; set; }
    public string project_name { get; set; } = string.Empty;
    public string? description { get; set; }
    public DateTime start_date { get; set; }
    public string status { get; set; } = "active";
}

public class ProfessionTask
{
    public long task_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public long employee_id { get; set; }
    public long? project_id { get; set; }
    public string task_title { get; set; } = string.Empty;
    public string? task_description { get; set; }
    public string task_source { get; set; } = "morning_plan";
    public byte priority { get; set; } = 2; // 1=Low, 2=Med, 3=High
    public string status { get; set; } = "pending";
    public DateTime planned_date { get; set; }
    public DateTime? due_at { get; set; }
    public int estimated_minutes { get; set; }
    public int actual_minutes { get; set; }
    public DateTime? completed_at { get; set; }
    public bool is_active { get; set; } = true;
    public DateTime created_at { get; set; } = DateTime.UtcNow;
}

public class Attendance
{
    public long attendance_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public long employee_id { get; set; }
    public DateTime work_date { get; set; }
    public DateTime login_at { get; set; }
    public DateTime? logout_at { get; set; }
    public string work_mode { get; set; } = "office";
    public string attendance_status { get; set; } = "present";
    public string? remarks { get; set; }
}

public class DailyReport
{
    public long report_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public long employee_id { get; set; }
    public DateTime report_date { get; set; }
    public string report_type { get; set; } = "morning_plan";
    public string report_content { get; set; } = string.Empty;
    public string? expected_outcome { get; set; }
    public string? blockers { get; set; }
    public string review_status { get; set; } = "draft";
    public DateTime created_at { get; set; } = DateTime.UtcNow;
}

public class BusinessEntity
{
    public long business_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public string business_name { get; set; } = string.Empty;
    public string business_type { get; set; } = string.Empty;
    public string owner_name { get; set; } = string.Empty;
    public string? phone_number { get; set; }
    public bool is_active { get; set; } = true;
}

public class Customer
{
    public long customer_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public long business_id { get; set; }
    public string customer_name { get; set; } = string.Empty;
    public string phone_number { get; set; } = string.Empty;
    public bool is_active { get; set; } = true;
}

public class BusinessTransaction
{
    public long transaction_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public long business_id { get; set; }
    public long category_id { get; set; }
    public string transaction_type { get; set; } = "income";
    public decimal amount { get; set; }
    public DateTime transaction_date { get; set; }
    public string payment_method { get; set; } = "cash";
    public string? reference_number { get; set; }
    public string? description { get; set; }
}

public class Receivable
{
    public long receivable_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public long business_id { get; set; }
    public long customer_id { get; set; }
    public string reference_number { get; set; } = string.Empty;
    public decimal total_amount { get; set; }
    public decimal paid_amount { get; set; }
    public DateTime issued_date { get; set; }
    public DateTime due_date { get; set; }
    public string status { get; set; } = "unpaid";
}

public class Vehicle
{
    public long vehicle_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public long business_id { get; set; }
    public string registration_number { get; set; } = string.Empty;
    public string vehicle_name { get; set; } = string.Empty;
    public string vehicle_type { get; set; } = "truck";
    public int odometer_reading { get; set; }
    public string status { get; set; } = "active";
}

public class HabitProfile
{
    public long profile_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public Guid user_public_id { get; set; }
    public string full_name { get; set; } = string.Empty;
    public DateTime date_of_birth { get; set; }
    public decimal? current_weight_kg { get; set; }
    public int water_target_ml { get; set; } = 3500;
    public bool is_active { get; set; } = true;
}

public class HabitItem
{
    public long habit_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public long profile_id { get; set; }
    public string habit_name { get; set; } = string.Empty;
    public string habit_category { get; set; } = "health";
    public decimal target_value { get; set; } = 1.0m;
    public string target_unit { get; set; } = "count";
    public bool is_active { get; set; } = true;
}

public class HabitLog
{
    public long habit_log_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public long habit_id { get; set; }
    public DateTime log_date { get; set; }
    public decimal recorded_value { get; set; }
    public string status { get; set; } = "completed";
}

public class WaterLog
{
    public long water_log_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public long profile_id { get; set; }
    public int quantity_ml { get; set; }
    public DateTime consumed_at { get; set; }
    public DateTime created_at { get; set; } = DateTime.UtcNow;
}

public class SleepLog
{
    public long sleep_log_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public long profile_id { get; set; }
    public DateTime sleep_at { get; set; }
    public DateTime wake_at { get; set; }
    public byte sleep_quality { get; set; } = 7;
}

public class HabitExpense
{
    public long expense_id { get; set; }
    public Guid public_id { get; set; } = Guid.NewGuid();
    public long category_id { get; set; }
    public decimal amount { get; set; }
    public DateTime expense_date { get; set; }
    public string payment_method { get; set; } = "upi";
    public bool is_essential { get; set; } = false;
    public string? remarks { get; set; }
}
