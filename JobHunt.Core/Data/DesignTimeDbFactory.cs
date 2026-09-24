using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobHunt.Core.Data;

/// <summary>Lets <c>dotnet ef migrations add</c> build the context without the app's host.</summary>
internal sealed class DesignTimeDbFactory : IDesignTimeDbContextFactory<JobHuntDb>
{
    public JobHuntDb CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<JobHuntDb>().UseSqlite("Data Source=design-time.db").Options);
}
