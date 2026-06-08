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
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
