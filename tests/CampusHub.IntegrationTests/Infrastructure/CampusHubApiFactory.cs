using campushub.Services;
using CampusHub.Data;
using CampusHub.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace CampusHub.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API in memory against a real PostgreSQL started in Docker.
/// One instance is shared by the whole test assembly (see AssemblyInfo.cs), so the
/// container starts once; tests stay independent by creating their own users and data.
/// </summary>
public sealed class CampusHubApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Must be at least 32 bytes for HMAC-SHA256
    public const string JwtSecret = "integration-tests-jwt-secret-at-least-32-bytes";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <summary>Captures e-mails (e.g. verification codes) instead of sending them.</summary>
    public FakeEmailService Emails { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // Apply migrations before the host starts, so background services find the schema
        var options = new DbContextOptionsBuilder<DataContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var db = new DataContext(options);
        await db.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development": that would load the developer's user-secrets and point the API at their database
        builder.UseEnvironment("Testing");

        // UseSetting values are visible while Program.cs runs, so the startup secret check passes
        builder.UseSetting("ConnectionStrings:DataContext", _postgres.GetConnectionString());
        builder.UseSetting("jwt:Secret", JwtSecret);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(Emails);

            services.RemoveAll<ICaptchaValidator>();
            services.AddSingleton<ICaptchaValidator, AlwaysValidCaptcha>();
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
