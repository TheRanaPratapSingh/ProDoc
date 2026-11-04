using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ProDoc.Infrastructure.Persistence;

public sealed class ProDocDbContextFactory : IDesignTimeDbContextFactory<ProDocDbContext>
{
    public ProDocDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(ResolveApiPath())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString(InfrastructureServiceCollectionExtensions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:ProDoc is not configured.");
        }

        var options = new DbContextOptionsBuilder<ProDocDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(ProDocDbContext).Assembly.GetName().Name))
            .Options;
        return new ProDocDbContext(options);
    }

    private static string ResolveApiPath()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var nested = Path.Combine(directory.FullName, "src", "ProDoc.Api", "appsettings.json");
            if (File.Exists(nested))
            {
                return Path.GetDirectoryName(nested)!;
            }

            if (directory.Name == "ProDoc.Api" && File.Exists(Path.Combine(directory.FullName, "appsettings.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate src/ProDoc.Api/appsettings.json.");
    }
}
