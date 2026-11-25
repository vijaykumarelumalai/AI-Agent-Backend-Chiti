using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Chitti.Backend.Data;

namespace Chitti.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HabitController : ControllerBase
{
    private readonly PersonalSecretaryDbContext _db;

    public HabitController(PersonalSecretaryDbContext db)
    {
        _db = db;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetTodaySummary()
    {
        var today = DateTime.UtcNow.Date;
        var waterTotal = await _db.WaterLogs
            .Where(w => w.consumed_at >= today)
            .SumAsync(w => (int?)w.quantity_ml) ?? 0;

        var expensesToday = await _db.HabitExpenses
            .Where(e => e.expense_date >= today)
            .SumAsync(e => (decimal?)e.amount) ?? 0.00m;

        var habits = await _db.Habits.Where(h => h.is_active).ToListAsync();

        return Ok(new
        {
            waterConsumedMl = waterTotal,
            waterTargetMl = 3500,
            expensesToday = expensesToday,
            habitsCount = habits.Count,
            activeHabits = habits
        });
    }

    public record LogWaterDto(int QuantityMl);

    [HttpPost("water")]
    public async Task<IActionResult> LogWater([FromBody] LogWaterDto dto)
    {
        var log = new WaterLog
        {
            public_id = Guid.NewGuid(),
            profile_id = 1,
            quantity_ml = dto.QuantityMl,
            consumed_at = DateTime.UtcNow,
            created_at = DateTime.UtcNow
        };

        _db.WaterLogs.Add(log);
        await _db.SaveChangesAsync();

        return Ok(log);
    }

    public record RecordExpenseDto(decimal Amount, string? Remarks, bool IsEssential = false);

    [HttpPost("expenses")]
    public async Task<IActionResult> RecordExpense([FromBody] RecordExpenseDto dto)
    {
        var exp = new HabitExpense
        {
            public_id = Guid.NewGuid(),
            category_id = 1,
            amount = dto.Amount,
            expense_date = DateTime.UtcNow,
            payment_method = "upi",
            is_essential = dto.IsEssential,
            remarks = dto.Remarks
        };

        _db.HabitExpenses.Add(exp);
        await _db.SaveChangesAsync();

        return Ok(exp);
    }
}
