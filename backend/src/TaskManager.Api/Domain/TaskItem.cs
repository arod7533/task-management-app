namespace TaskManager.Api.Domain;

public enum TaskItemStatus
{
    Todo = 0,
    InProgress = 1,
    Done = 2
}

public class TaskItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public int Version { get; set; }
}
