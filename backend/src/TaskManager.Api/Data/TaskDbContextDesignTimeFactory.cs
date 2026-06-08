using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskManager.Api.Data;

// Used by `dotnet ef migrations` — the design-time tools cannot resolve
// ICurrentUserAccessor from DI, so we provide a stub that returns null. The
// schema doesn't depend on the user id, only the runtime query filter does.
public class TaskDbContextDesignTimeFactory : IDesignTimeDbContextFactory<TaskDbContext>
{
    private sealed class NullUser : ICurrentUserAccessor
    {
        public Guid? UserId => null;
    }

    public TaskDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<TaskDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new TaskDbContext(opts, new NullUser());
    }
}
