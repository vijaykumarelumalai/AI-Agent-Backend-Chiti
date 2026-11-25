using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Chitti.Backend.Data;

namespace Chitti.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfessionController : ControllerBase
{
    private readonly PersonalSecretaryDbContext _db;

    public ProfessionController(PersonalSecretaryDbContext db)
    {
        _db = db;
    }

    [HttpGet("tasks")]
    public async Task<IActionResult> GetTasks([FromQuery] string? status)
    {
        var query = _db.ProfessionTasks.AsQueryable();
        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(t => t.status == status);
        }

        var tasks = await query
            .OrderByDescending(t => t.priority)
            .ThenBy(t => t.due_at)
            .Take(50)
            .ToListAsync();

        return Ok(tasks);
    }

    public record CreateTaskDto(string Title, string? Description, byte Priority, DateTime? DueAt);

    [HttpPost("tasks")]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskDto dto)
    {
        var task = new ProfessionTask
        {
            public_id = Guid.NewGuid(),
            employee_id = 1,
            task_title = dto.Title,
            task_description = dto.Description,
            priority = dto.Priority,
            planned_date = DateTime.UtcNow.Date,
            due_at = dto.DueAt,
            status = "pending",
            created_at = DateTime.UtcNow
        };

        _db.ProfessionTasks.Add(task);
        await _db.SaveChangesAsync();

        return Ok(task);
    }

    [HttpPost("attendance/login")]
    public async Task<IActionResult> CheckIn([FromQuery] string workMode = "office")
    {
        var today = DateTime.UtcNow.Date;
        var existing = await _db.Attendances.FirstOrDefaultAsync(a => a.employee_id == 1 && a.work_date == today);
        if (existing != null)
        {
            return Ok(new { message = "Already logged in today", attendance = existing });
        }

        var attendance = new Attendance
        {
            public_id = Guid.NewGuid(),
            employee_id = 1,
            work_date = today,
            login_at = DateTime.UtcNow,
            work_mode = workMode,
            attendance_status = "present",
            remarks = "Logged in via Chitti Secretary"
        };

        _db.Attendances.Add(attendance);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Login successful", attendance });
    }
}
