using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Api.Contracts;
using TaskManager.Api.Data;
using TaskManager.Api.Domain;

namespace TaskManager.Api.Tests;

public class TasksApiTests : IClassFixture<ApiFactory>
{
    // Match the server's JSON options so enum names round-trip correctly.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public TasksApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_RejectsEmptyTitle_Returns400()
    {
        var resp = await _client.PostAsJsonAsync("/api/tasks",
            new CreateTaskRequest("", null, null, null), Json);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Create_RejectsOversizedTitle_Returns400()
    {
        var resp = await _client.PostAsJsonAsync("/api/tasks",
            new CreateTaskRequest(new string('x', 201), null, null, null), Json);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Create_PersistsAndReturns201WithLocation()
    {
        var resp = await _client.PostAsJsonAsync("/api/tasks",
            new CreateTaskRequest("Buy milk", "2% organic", null, TaskItemStatus.Todo), Json);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        Assert.NotNull(resp.Headers.Location);

        var created = await resp.Content.ReadFromJsonAsync<TaskResponse>(Json);
        Assert.NotNull(created);
        Assert.Equal("Buy milk", created!.Title);
        Assert.Equal(1, created.Version);
    }

    [Fact]
    public async Task Get_MissingId_Returns404()
    {
        var resp = await _client.GetAsync($"/api/tasks/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Update_MissingId_Returns404()
    {
        var resp = await _client.PutAsJsonAsync($"/api/tasks/{Guid.NewGuid()}",
            new UpdateTaskRequest("x", null, null, TaskItemStatus.Todo, 1), Json);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_MissingId_Returns404()
    {
        var resp = await _client.DeleteAsync($"/api/tasks/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task List_FiltersByStatus()
    {
        await CreateAsync("Todo item", TaskItemStatus.Todo);
        var done = await CreateAsync("Done item", TaskItemStatus.Todo);

        var put = await _client.PutAsJsonAsync($"/api/tasks/{done.Id}",
            new UpdateTaskRequest(done.Title, null, null, TaskItemStatus.Done, done.Version), Json);
        put.EnsureSuccessStatusCode();

        var resp = await _client.GetAsync("/api/tasks?status=Done");
        resp.EnsureSuccessStatusCode();
        var items = await resp.Content.ReadFromJsonAsync<List<TaskResponse>>(Json);

        Assert.NotNull(items);
        Assert.All(items!, t => Assert.Equal(TaskItemStatus.Done, t.Status));
        Assert.Contains(items!, t => t.Id == done.Id);
    }

    [Fact]
    public async Task Update_WithStaleVersion_Returns409WithCurrentState()
    {
        var created = await CreateAsync("Original title");

        // First update succeeds, bumping the row to version 2.
        var firstUpdate = await _client.PutAsJsonAsync($"/api/tasks/{created.Id}",
            new UpdateTaskRequest("First edit", null, null, TaskItemStatus.Todo, created.Version), Json);
        firstUpdate.EnsureSuccessStatusCode();

        // Second update reuses the stale version from `created` — must conflict.
        var stale = await _client.PutAsJsonAsync($"/api/tasks/{created.Id}",
            new UpdateTaskRequest("Stale edit", null, null, TaskItemStatus.Todo, created.Version), Json);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var body = await stale.Content.ReadAsStringAsync();
        Assert.Contains("\"current\"", body);
        Assert.Contains("First edit", body);
    }

    [Fact]
    public async Task Delete_SoftDeletes_RowSurvivesButHiddenFromQueries()
    {
        var created = await CreateAsync("Delete me");

        var del = await _client.DeleteAsync($"/api/tasks/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // From the API surface, it's gone.
        var get = await _client.GetAsync($"/api/tasks/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        // But the row is still in the database — bypass the soft-delete query
        // filter to verify the policy.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();
        var row = await db.Tasks.IgnoreQueryFilters().SingleAsync(t => t.Id == created.Id);
        Assert.NotNull(row.DeletedAt);
    }

    private async Task<TaskResponse> CreateAsync(string title, TaskItemStatus status = TaskItemStatus.Todo)
    {
        var resp = await _client.PostAsJsonAsync("/api/tasks",
            new CreateTaskRequest(title, null, null, status), Json);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<TaskResponse>(Json))!;
    }
}
