using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Domain;

namespace TaskManager.Api.Data;

public class TaskDbContext : DbContext
{
    public TaskDbContext(DbContextOptions<TaskDbContext> options) : base(options) { }

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var task = modelBuilder.Entity<TaskItem>();

        task.HasKey(t => t.Id);
        task.Property(t => t.Title).IsRequired().HasMaxLength(200);
        task.Property(t => t.Description).HasMaxLength(2000);
        task.Property(t => t.Status).HasConversion<int>();

        // SQLite has no native DateTimeOffset type and EF refuses to translate
        // ORDER BY on it. Storing as ISO 8601 text gives chronologically
        // correct lexical sort.
        task.Property(t => t.CreatedAt).HasConversion<string>();
        task.Property(t => t.UpdatedAt).HasConversion<string>();
        task.Property(t => t.DueDate).HasConversion<string>();
        task.Property(t => t.DeletedAt).HasConversion<string>();

        // Optimistic concurrency. SQLite has no native rowversion, so the
        // integer is incremented on every save (see SaveChangesAsync) and
        // checked in the UPDATE WHERE clause.
        task.Property(t => t.Version).IsConcurrencyToken();

        // Soft delete: hide rows where DeletedAt IS NOT NULL by default.
        // Use IgnoreQueryFilters() to opt out (e.g. admin / audit views).
        task.HasQueryFilter(t => t.DeletedAt == null);

        // Indexed to keep filtered scans cheap as the table grows.
        task.HasIndex(t => t.DeletedAt);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<TaskItem>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
                entry.Entity.Version = 1;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.Version += 1;
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
