using System.IO;
using System.Windows;
using JobHunt.Core.Data;
using JobHunt.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace JobHunt.App;

/// <summary>
/// One DI registration point for the whole app (the same Host.CreateDefaultBuilder shape Automata
/// and KdpPublish use). Logging is Serilog behind Microsoft.Extensions.Logging, as in Prose: every
/// ILogger&lt;T&gt; in Core writes to a daily rolling file under %LocalAppData%\MindAttic\JobHunt\Logs.
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = new JobHuntPaths();
        paths.EnsureCreated();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            // JobHunt's own code logs at Debug; the framework (EF's SQL, HttpClient, hosting) only
            // when something is wrong, so a day's log stays readable.
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(paths.LogsDirectory, "jobhunt-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        // Any unhandled exception is logged in every build. The dispatcher case also recovers: one
        // unexpected error should not cost the user their session.
        DispatcherUnhandledException += (_, ex) =>
        {
            Log.Error(ex.Exception, "Unhandled UI exception");
            MessageBox.Show($"JobHunt hit an unexpected error and logged it to:\n{paths.LogsDirectory}\n\nYou can keep working.",
                "JobHunt", MessageBoxButton.OK, MessageBoxImage.Warning);
            ex.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => Log.Fatal(ex.ExceptionObject as Exception, "Unhandled exception");
        TaskScheduler.UnobservedTaskException += (_, ex) => { Log.Error(ex.Exception, "Unobserved task exception"); ex.SetObserved(); };

        var host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((_, services) => services.AddJobHuntCore(paths))
            .Build();
        Services = host.Services;

        MigrationOutcome migration;
        try
        {
            migration = Services.MigrateJobHuntDatabase();
        }
        catch (Exception ex)
        {
            // Without a database there is no app to keep working in — say so plainly and exit
            // rather than show the generic "you can keep working" message over an empty screen.
            Log.Fatal(ex, "The database could not be opened or upgraded");
            MessageBox.Show($"JobHunt couldn't open its database:\n{paths.DatabasePath}\n\n{ex.Message}\n\nDetails are in:\n{paths.LogsDirectory}",
                "JobHunt", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        Services.GetRequiredService<ILogger<App>>().LogInformation(
            "JobHunt {Version} started; data in {DataRoot}", typeof(App).Assembly.GetName().Version, paths.DataRoot);

        new MainWindow().Show();
        if (migration.SetAsidePath is { } old)
        {
            MessageBox.Show($"Your JobHunt database was made by an earlier test build and couldn't be upgraded, so a fresh one was started.\n\nThe old file was kept, untouched, at:\n{old}",
                "JobHunt", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("JobHunt exiting");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
