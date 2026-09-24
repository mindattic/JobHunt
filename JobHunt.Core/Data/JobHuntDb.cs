using System.Text.Json;
using JobHunt.Core.Applications;
using JobHunt.Core.Hunting;
using JobHunt.Core.Jobs;
using JobHunt.Core.Profile;
using JobHunt.Core.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JobHunt.Core.Data;

/// <summary>
/// The JobHunt database: one SQLite file holding the applicant profile, saved searches, every job
/// found, and every application with its outcome history. Schema changes ship as EF migrations, so
/// a user upgrading the app keeps all of it.
/// </summary>
public sealed class JobHuntDb(DbContextOptions<JobHuntDb> options) : DbContext(options)
{
    /// <summary>The users. Deleted users stay, hidden (see <see cref="UserProfile.DeletedAt"/>).</summary>
    public DbSet<UserProfile> Profiles => Set<UserProfile>();
    public DbSet<SearchProfile> Searches => Set<SearchProfile>();
    public DbSet<HuntRun> HuntRuns => Set<HuntRun>();
    public DbSet<JobPosting> Jobs => Set<JobPosting>();
    public DbSet<JobApplication> Applications => Set<JobApplication>();
    public DbSet<ApplicationEvent> ApplicationEvents => Set<ApplicationEvent>();
    public DbSet<AppSettings> Settings => Set<AppSettings>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // SQLite has no native DateTimeOffset. Stored as UTC ticks — the instant, not the wall
        // clock — so ORDER BY and range comparisons in SQL are correct even when values were made
        // with different offsets (EF's DateTimeOffsetToBinaryConverter keeps the local clock time,
        // which made "applications since local midnight" count the wrong day outside UTC).
        builder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
        builder.Properties<DateTimeOffset?>().HaveConversion<UtcTicksConverter>();
        // decimal as TEXT keeps exact money values (SQLite REAL would round).
        builder.Properties<decimal>().HaveConversion<string>();
        builder.Properties<decimal?>().HaveConversion<string>();

        // Enums as their names: readable in any SQLite browser, and reordering an enum can't
        // silently remap stored rows.
        foreach (var enumType in new[]
                 {
                     typeof(WorkplaceType), typeof(EmploymentType), typeof(ContractTerms), typeof(PayPeriod),
                     typeof(JobStatus), typeof(ApplicationOutcome), typeof(OutcomeSource), typeof(SkillKind),
                     typeof(AccomplishmentKind),
                 })
        {
            builder.Properties(enumType).HaveConversion<string>();
            builder.Properties(typeof(Nullable<>).MakeGenericType(enumType)).HaveConversion<string>();
        }
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<UserProfile>(p =>
        {
            p.Ignore(x => x.FullName);
            p.Ignore(x => x.DisplayName);
            p.HasIndex(x => x.DeletedAt);
            // Value groups with no identity of their own are complex types: stored as columns on
            // the Profiles row (Contact_Address_City, …), compared by value, never a separate table.
            p.ComplexProperty(x => x.Contact, c => c.ComplexProperty(x => x.Address));
            p.ComplexProperty(x => x.Authorization);
            p.ComplexProperty(x => x.Compensation);
            p.ComplexProperty(x => x.Preferences);
            p.ComplexProperty(x => x.Summary);
            p.ComplexProperty(x => x.Disclosures);

            p.HasMany(x => x.BoardAccounts).WithOne().OnDelete(DeleteBehavior.Cascade);
            p.HasMany(x => x.Links).WithOne().OnDelete(DeleteBehavior.Cascade);
            p.HasMany(x => x.Experience).WithOne().OnDelete(DeleteBehavior.Cascade);
            p.HasMany(x => x.Education).WithOne().OnDelete(DeleteBehavior.Cascade);
            p.HasMany(x => x.Skills).WithOne().OnDelete(DeleteBehavior.Cascade);
            p.HasMany(x => x.Certifications).WithOne().OnDelete(DeleteBehavior.Cascade);
            p.HasMany(x => x.Languages).WithOne().OnDelete(DeleteBehavior.Cascade);
            p.HasMany(x => x.Projects).WithOne().OnDelete(DeleteBehavior.Cascade);
            p.HasMany(x => x.Accomplishments).WithOne().OnDelete(DeleteBehavior.Cascade);
            p.HasMany(x => x.References).WithOne().OnDelete(DeleteBehavior.Cascade);
            p.HasMany(x => x.KeywordLists).WithOne().OnDelete(DeleteBehavior.Cascade);
            p.HasMany(x => x.TextBlocks).WithOne().OnDelete(DeleteBehavior.Cascade);
            p.HasMany(x => x.CustomFields).WithOne().OnDelete(DeleteBehavior.Cascade);
            p.HasMany(x => x.Answers).WithOne().OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<WorkExperience>(e =>
        {
            e.Ignore(x => x.IsCurrent);
            e.HasMany(x => x.Bullets).WithOne().OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<Bullet>().Ignore(x => x.FactRef);
        model.Entity<Skill>().Ignore(x => x.FactRef);
        model.Entity<Project>().Ignore(x => x.FactRef);
        model.Entity<Accomplishment>().Ignore(x => x.FactRef);
        model.Entity<ScreeningAnswer>().HasIndex(x => x.NormalizedQuestion);
        model.Entity<BoardAccount>().HasIndex("UserProfileId", nameof(BoardAccount.BoardId)).IsUnique();

        model.Entity<SearchProfile>(s =>
        {
            s.HasOne<UserProfile>().WithMany().HasForeignKey(x => x.UserProfileId).OnDelete(DeleteBehavior.Cascade);
            s.Property(x => x.Filters).HasJson();
            s.Property(x => x.Weights).HasJson();
        });
        model.Entity<HuntRun>().HasIndex(x => x.SearchProfileId);

        model.Entity<JobPosting>(j =>
        {
            j.HasOne<UserProfile>().WithMany().HasForeignKey(x => x.UserProfileId).OnDelete(DeleteBehavior.Cascade);
            j.HasIndex(x => new { x.UserProfileId, x.BoardId, x.ExternalId }).IsUnique();
            j.HasIndex(x => new { x.UserProfileId, x.Status, x.Score });
            j.Property(x => x.Requirements).HasJson();
            j.Property(x => x.ScoreBreakdown).HasJson();
            j.Property(x => x.Evidence).HasJson();
            j.HasMany(x => x.Applications).WithOne(x => x.JobPosting).HasForeignKey(x => x.JobPostingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<JobApplication>(a =>
        {
            a.Property(x => x.Answers).HasJson();
            a.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.JobApplicationId).OnDelete(DeleteBehavior.Cascade);
            a.HasIndex(x => x.AppliedAt);
        });
    }
}

/// <summary>DateTimeOffset ⇄ UTC ticks. Values come back in UTC (offset zero); the instant is exact.</summary>
internal sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
    v => v.UtcTicks,
    t => new DateTimeOffset(t, TimeSpan.Zero));

internal static class JsonColumnExtensions
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    /// <summary>
    /// Stores a complex property as one JSON text column. Used for shapes that are always read and
    /// written whole (filters, score breakdowns, requirement lists) and that change more often than
    /// is worth a migration each time. The comparer makes EF detect in-place edits.
    /// </summary>
    public static PropertyBuilder<T> HasJson<T>(this PropertyBuilder<T> property) where T : class, new()
    {
        property.HasConversion(
            new ValueConverter<T, string>(
                v => JsonSerializer.Serialize(v, Options),
                s => string.IsNullOrWhiteSpace(s) ? new T() : JsonSerializer.Deserialize<T>(s, Options) ?? new T()),
            new ValueComparer<T>(
                (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
                v => JsonSerializer.Serialize(v, Options).GetHashCode(),
                v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Options), Options)!));
        return property;
    }
}
