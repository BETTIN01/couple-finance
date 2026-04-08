using CoupleFinance.Application.Contracts;
using CoupleFinance.Application.Services;
using CoupleFinance.Infrastructure.Configuration;
using CoupleFinance.Infrastructure.Persistence;
using CoupleFinance.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CoupleFinance.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCoupleFinanceServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LocalStorageOptions>(configuration.GetSection("LocalStorage"));
        services.Configure<SupabaseOptions>(configuration.GetSection("Supabase"));

        services.AddSingleton<AppSessionStore>();
        services.AddSingleton<PasswordHasher>();
        services.AddHttpClient<SupabaseRestClient>();

        services.AddDbContextFactory<AppDbContext>((provider, options) =>
        {
            var sessionStore = provider.GetRequiredService<AppSessionStore>();
            var localOptions = provider.GetRequiredService<IOptions<LocalStorageOptions>>();
            var databasePath = Path.Combine(sessionStore.GetAppFolder(), localOptions.Value.DatabaseFileName);
            options.UseSqlite($"Data Source={databasePath}");
        });

        services.AddSingleton<IDashboardComposer, DashboardComposer>();
        services.AddSingleton<IProjectionService, ProjectionService>();
        services.AddSingleton<IInsightEngine, InsightEngine>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<ISyncCoordinator, SyncCoordinator>();
        services.AddSingleton<IFinanceWorkspaceService, FinanceWorkspaceService>();
        services.AddTransient(typeof(ILocalRepository<>), typeof(SqliteLocalRepository<>));

        return services;
    }
}
