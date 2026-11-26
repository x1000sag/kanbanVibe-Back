using System.Text;
using Account.Domain;
using Account.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Kanban.Api.Account;

public sealed class AccountJwtBearerConfigureOptions(
    AccountOptions accountOptions,
    TimeProvider timeProvider) : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        options.MapInboundClaims = false;
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(accountOptions.JwtSigningKey));
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = AccessTokenIssuer.Issuer,
            ValidateAudience = true,
            ValidAudience = AccessTokenIssuer.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            LifetimeValidator = (notBefore, expires, _, _) =>
            {
                var now = timeProvider.GetUtcNow().UtcDateTime;
                return (notBefore is null || notBefore <= now) && now < expires;
            },
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await AccountExceptionMiddleware.WriteRefusalAsync(
                    context.HttpContext,
                    RefusalCodes.NoActiveAccess,
                    401);
            },
            OnAuthenticationFailed = context =>
            {
                context.NoResult();
                return Task.CompletedTask;
            },
        };
    }

    public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);
}