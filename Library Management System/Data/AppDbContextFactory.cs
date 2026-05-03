using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace System.Data;

/// <summary>
/// Used by EF Core tools (migrations) without requiring a running app.
/// Reads <c>ConnectionStrings:DefaultConnection</c> from appsettings.json.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var conn = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings:DefaultConnection in appsettings.json (replace YOUR_PASSWORD).");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        var serverVersion = ServerVersion.Parse("8.0.36-mysql");
        optionsBuilder.UseMySql(conn, serverVersion);
        return new AppDbContext(optionsBuilder.Options);
    }
}
