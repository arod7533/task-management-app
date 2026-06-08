using System.ComponentModel.DataAnnotations;
using TaskManager.Api.Domain;

namespace TaskManager.Api.Contracts;

public record CreateTaskRequest(
    [Required(AllowEmptyStrings = false, ErrorMessage = "Title is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be 1-200 characters.")]
    string Title,

    [StringLength(2000, ErrorMessage = "Description must be 2000 characters or fewer.")]
    string? Description,

    DateTimeOffset? DueDate,

    TaskItemStatus? Status
);

public record UpdateTaskRequest(
    [Required(AllowEmptyStrings = false, ErrorMessage = "Title is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be 1-200 characters.")]
    string Title,

    [StringLength(2000, ErrorMessage = "Description must be 2000 characters or fewer.")]
    string? Description,

    DateTimeOffset? DueDate,

    [Required(ErrorMessage = "Status is required.")]
    TaskItemStatus Status,

    // Optimistic concurrency token returned by GET/POST/PUT. Clients must
    // echo the version they edited; a mismatch yields 409 Conflict.
    [Required(ErrorMessage = "Version is required for updates.")]
    int Version
);

public record TaskResponse(
    Guid Id,
    string Title,
    string? Description,
    DateTimeOffset? DueDate,
    TaskItemStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version
)
{
    public static TaskResponse From(TaskItem t) => new(
        t.Id, t.Title, t.Description, t.DueDate, t.Status,
        t.CreatedAt, t.UpdatedAt, t.Version);
}
