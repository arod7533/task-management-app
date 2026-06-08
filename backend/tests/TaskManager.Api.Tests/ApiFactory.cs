using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Api.Data;

namespace TaskManager.Api.Tests;

// Spins up the real app pointed at an in-memory SQLite database so we
// exercise the actual EF + SQLite behavior in tests.
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public ApiFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Swap the SQLite-file registration for one bound to the open
            // in-memory connection we own here.
            var descriptor = services.Single(
                d => d.ServiceType == typeof(DbContextOptions<TaskDbContext>));
            services.Remove(descriptor);

            services.AddDbContext<TaskDbContext>(opt => opt.UseSqlite(_connection));
            // Schema is created by Program.cs's Database.Migrate() on startup,
            // running against this same open in-memory connection.
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
