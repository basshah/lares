using Lares.Api.Data;
using Lares.Api.Services;
using Lares.Api.Tests.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Lares.Api.Tests;

/// <summary>
/// Boots the API in-memory against a throwaway PostgreSQL container,
/// so tests exercise the real HTTP + EF + Postgres pipeline.
/// </summary>
public class LaresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:Key", "lares-test-signing-key-0123456789-abcdefghijk");
        builder.UseSetting("Gemini:ApiKey", "test-key-unused");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAiClient>();
            services.AddScoped<IAiClient, FakeAiClient>();
            // The real time-based scheduler runs on a 30s wall-clock loop, which would make tests
            // both slow and racy. Tests call AutomationSchedulerService.RunOnceAsync(...) directly
            // for deterministic control instead.
            services.RemoveAll<IHostedService>();
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<LaresDbContext>().Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
