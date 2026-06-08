using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Domain;

namespace TaskManager.Api.Data;

// Holds the current request's user id. Resolved from the JWT subject claim by
// CurrentUserAccessor and consumed by the global query filter on TaskItem so
// every read is automatically scoped — defense in depth against forgetting an
// explicit WHERE clause in a controller.
public interface ICurrentUserAccessor
{
    Guid? UserId { get; }
}

public class TaskDbContext : DbContext
{
    private readonly ICurrentUserAccessor _user;

    public TaskDbContext(DbContextOptions<TaskDbContext> options, ICurrentUserAccessor user)
        : base(options)
    {
        _user = user;
    }

    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<User>();
        user.HasKey(u => u.Id);
        user.Property(u => u.Email).IsRequired().HasMaxLength(254);
        user.HasIndex(u => u.Email).IsUnique();
        user.Property(u => u.PasswordHash).IsRequired();
        user.Property(u => u.CreatedAt).HasConversion<string>();

        var task = modelBuilder.Entity<TaskItem>();

        task.HasKey(t => t.Id);
        task.Property(t => t.OwnerId).IsRequired();
        task.HasIndex(t => t.OwnerId);
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

        // Optimistic concurrency. SQLite has no native rowversion, so we use
        // an integer that we increment on every save (see SaveChangesAsync).
        task.Property(t => t.Version).IsConcurrencyToken();

        // Two filters in one expression:
        //  - exclude soft-deleted rows
        //  - exclude rows the caller does not own
        // The OwnerId comparison reads _user.UserId at query-translation time,
        // so anonymous contexts (none of which should ever read tasks) get a
        // filter that matches nothing.
        task.HasQueryFilter(t =>
            t.DeletedAt == null &&
            _user.UserId != null &&
            t.OwnerId == _user.UserId);

        // Index the soft-delete column to keep filtered scans cheap as the table grows.
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
        foreach (var entry in ChangeTracker.Entries<User>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = now;
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
