using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Contracts;
using TaskManager.Api.Data;
using TaskManager.Api.Domain;

namespace TaskManager.Api.Controllers;

[ApiController]
[Route("api/tasks")]
public class TasksController : ControllerBase
{
    private readonly TaskDbContext _db;

    public TasksController(TaskDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskResponse>>> List(
        [FromQuery] TaskItemStatus? status,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var q = _db.Tasks.AsNoTracking();

        if (status.HasValue)
            q = q.Where(t => t.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(t => EF.Functions.Like(t.Title, $"%{s}%"));
        }

        var items = await q.OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
        return Ok(items.Select(TaskResponse.From));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaskResponse>> Get(Guid id, CancellationToken ct)
    {
        var t = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return t is null ? NotFound() : Ok(TaskResponse.From(t));
    }

    [HttpPost]
    public async Task<ActionResult<TaskResponse>> Create(
        [FromBody] CreateTaskRequest body,
        CancellationToken ct)
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = body.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description.Trim(),
            DueDate = body.DueDate,
            Status = body.Status ?? TaskItemStatus.Todo,
        };

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = task.Id }, TaskResponse.From(task));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TaskResponse>> Update(
        Guid id,
        [FromBody] UpdateTaskRequest body,
        CancellationToken ct)
    {
        var task = await _db.Tasks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (task is null) return NotFound();

        task.Title = body.Title.Trim();
        task.Description = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description.Trim();
        task.DueDate = body.DueDate;
        task.Status = body.Status;

        await _db.SaveChangesAsync(ct);
        return Ok(TaskResponse.From(task));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var task = await _db.Tasks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (task is null) return NotFound();

        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
