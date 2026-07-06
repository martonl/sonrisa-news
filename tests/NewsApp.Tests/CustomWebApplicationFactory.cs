using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NewsApp.Infrastructure.Data;

namespace NewsApp.Tests;

/// <summary>
/// Spins up the full ASP.NET Core host with a per-instance SQLite :memory: database
/// so tests never touch the real news.db file and provider registrations don't conflict.
/// </summary>
public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>
    where TProgram : class
{
    // Keep the connection open for the lifetime of the factory so the in-memory DB persists.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public CustomWebApplicationFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Override configuration so tests are fully self-contained
        builder.ConfigureAppConfiguration(config =>
        {
            // Only override AdminSeed — JWT config is read from appsettings.json so that
            // the key captured at startup by AddIdentityModule and the key used by
            // JwtTokenService at runtime are always in sync.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminSeed:Email"] = "admin@test.com",
                ["AdminSeed:Password"] = "Admin123!"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContextOptions<AppDbContext> descriptor so that
            // the original SQLite file-based connection is not used.
            var optionsDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (optionsDescriptor is not null)
                services.Remove(optionsDescriptor);

            // Register the context against the shared in-memory connection.
            // Same provider (SQLite) — no two-provider conflict.
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(_connection));
        });

        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
