using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Chitti.Backend.Data;

namespace Chitti.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BusinessController : ControllerBase
{
    private readonly PersonalSecretaryDbContext _db;

    public BusinessController(PersonalSecretaryDbContext db)
    {
        _db = db;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var businesses = await _db.Businesses.Take(5).ToListAsync();
        var recentTransactions = await _db.BusinessTransactions
            .OrderByDescending(t => t.transaction_date)
            .Take(10)
            .ToListAsync();
        var pendingReceivables = await _db.Receivables
            .Where(r => r.status != "paid")
            .OrderBy(r => r.due_date)
            .Take(10)
            .ToListAsync();
        var vehicles = await _db.Vehicles.Take(10).ToListAsync();

        return Ok(new
        {
            businesses,
            recentTransactions,
            pendingReceivables,
            vehicles
        });
    }

    public record AddTransactionDto(long BusinessId, long CategoryId, string Type, decimal Amount, string PaymentMethod, string? Description);

    [HttpPost("transactions")]
    public async Task<IActionResult> AddTransaction([FromBody] AddTransactionDto dto)
    {
        var tx = new BusinessTransaction
        {
            public_id = Guid.NewGuid(),
            business_id = dto.BusinessId,
            category_id = dto.CategoryId,
            transaction_type = dto.Type,
            amount = dto.Amount,
            transaction_date = DateTime.UtcNow.Date,
            payment_method = dto.PaymentMethod,
            description = dto.Description
        };

        _db.BusinessTransactions.Add(tx);
        await _db.SaveChangesAsync();

        return Ok(tx);
    }
}
