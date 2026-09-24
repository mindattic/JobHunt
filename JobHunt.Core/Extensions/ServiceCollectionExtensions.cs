using JobHunt.Core.Boards;
using JobHunt.Core.Boards.LinkedIn;
using JobHunt.Core.Data;
using JobHunt.Core.Hunting;
using JobHunt.Core.Jobs;
using JobHunt.Core.Llm;
using JobHunt.Core.Profile;
using JobHunt.Core.Scoring;
using JobHunt.Core.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MindAttic.Legion;

namespace JobHunt.Core.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// The one service graph every JobHunt front door (the app, a future CLI) registers
    /// (HOUSE-LAW-6). Logging is the host's job — register Serilog (or anything) as the
    /// Microsoft.Extensions.Logging provider before or after this.
    /// </summary>
    public static IServiceCollection AddJobHuntCore(this IServiceCollection services, JobHuntPaths? paths = null)
    {
        paths ??= new JobHuntPaths();
        paths.EnsureCreated();

        services.AddSingleton(paths);
        services.AddSingleton(TimeProvider.System);
        services.AddDbContextFactory<JobHuntDb>(o => o.UseSqlite(paths.ConnectionString));

        services.AddSingleton<DatabaseMigrator>();
        services.AddSingleton<ProfileStore>();
        services.AddSingleton<JobStore>();
        services.AddSingleton<SettingsStore>();
        services.AddSingleton<DatabaseTransfer>();
        services.AddSingleton<SearchStore>();
        services.AddSingleton<HuntRunner>();
        services.AddSingleton<FitScorer>();

        services.AddSingleton<IJobBoard, LinkedInJobBoard>();
        services.AddSingleton<JobBoardRegistry>();
        services.AddSingleton<BoardPasswords>();
        services.AddSingleton<LinkedInSignIn>();

        services.AddSingleton<ByokKeys>();
        services.AddHttpClient("legion", c => c.Timeout = TimeSpan.FromMinutes(3));
        services.AddSingleton(sp => new LegionClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("legion"),
            options: null,
            keyResolver: sp.GetRequiredService<ByokKeys>().Resolve));
        services.AddSingleton<IJobHuntLlm>(sp => new LegionJobHuntLlm(
            sp.GetRequiredService<LegionClient>(),
            () => sp.GetRequiredService<SettingsStore>().Current.SelectedLlmProvider,
            sp.GetRequiredService<ILogger<LegionJobHuntLlm>>()));

        return services;
    }

    /// <summary>Creates the database or brings it up to the current schema. Call once at startup.
    /// See <see cref="DatabaseMigrator"/> for what happens to a database this build can't upgrade.</summary>
    public static MigrationOutcome MigrateJobHuntDatabase(this IServiceProvider services) =>
        services.GetRequiredService<DatabaseMigrator>().Migrate();
}
