using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCors = "frontend";

builder.Services.AddCors(o => o.AddPolicy(FrontendCors, p => p
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddDbContext<TaskDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default")
        ?? "Data Source=tasks.db"));

builder.Services
    .AddControllers()
    .AddJsonOptions(opt =>
    {
        // Serialize/accept enums as their string names (e.g. "Todo") so the
        // wire format is self-describing rather than relying on integer order.
        opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply migrations on startup. Cheap for SQLite + makes fresh-clone setup
// match the README ("dotnet run" — no manual migrate step).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(FrontendCors);
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposed so WebApplicationFactory<Program> can find the entry point.
public partial class Program { }
