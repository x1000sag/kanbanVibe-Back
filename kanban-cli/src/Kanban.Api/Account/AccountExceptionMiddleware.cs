using System.Text.Json;
using Account.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Account;

public sealed class AccountExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<AccountExceptionMiddleware> _logger;

    public AccountExceptionMiddleware(RequestDelegate next, ILogger<AccountExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/v1"))
        {
            await _next(context);
            return;
        }

        try
        {
            await _next(context);
        }
        catch (AccountRefusalException ex)
        {
            await WriteRefusalAsync(context, ex.Code, RefusalHttpStatus.ForCode(ex.Code));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "account_store_error");
            await WriteRefusalAsync(context, RefusalCodes.StoreUnavailable, 503);
        }
        catch (Exception ex) when (context.Request.Path.StartsWithSegments("/v1"))
        {
            _logger.LogError(ex, "account_unhandled_error");
            await WriteRefusalAsync(context, RefusalCodes.StoreUnavailable, 503);
        }
    }

    internal static async Task WriteRefusalAsync(HttpContext context, string code, int status)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { code }, JsonOptions));
    }
}