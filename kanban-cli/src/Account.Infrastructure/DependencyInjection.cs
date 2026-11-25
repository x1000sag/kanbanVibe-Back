using Microsoft.Extensions.DependencyInjection;

namespace Account.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAccountServices(
        this IServiceCollection services,
        string jwtSigningKey)
    {
        services.AddSingleton<AccountPasswordHasher>();
        services.AddSingleton(new AccessTokenIssuer(jwtSigningKey));
        services.AddScoped<AuditWriter>();
        services.AddScoped<RegistrationService>();
        services.AddScoped<SignInService>();
        services.AddScoped<AccessGuard>();
        services.AddScoped<ProfileReader>();
        services.AddScoped<BoardWriter>();
        services.AddScoped<AccountUpdateService>();
        services.AddScoped<JournalReader>();
        services.AddScoped<SessionEndService>();
        return services;
    }
}