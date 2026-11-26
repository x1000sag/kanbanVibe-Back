using Account.Domain;
using Account.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Account;

[ApiController]
[Route("v1/health")]
public sealed class HealthController : ControllerBase
{
    private readonly AccountDbContext _db;

    public HealthController(AccountDbContext db) => _db = db;

    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken ct)
    {
        try
        {
            if (!await _db.Database.CanConnectAsync(ct))
            {
                throw new AccountRefusalException(RefusalCodes.StoreUnavailable);
            }

            _ = await _db.Accounts.AsNoTracking().AnyAsync(ct);
            return Ok();
        }
        catch (AccountRefusalException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new AccountRefusalException(RefusalCodes.StoreUnavailable);
        }
    }
}