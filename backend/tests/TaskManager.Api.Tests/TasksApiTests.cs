using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskManager.Api.Contracts;
using TaskManager.Api.Domain;

namespace TaskManager.Api.Tests;

public class TasksApiTests : IClassFixture<ApiFactory>
{
    // Match the server's JSON options so enum names round-trip correctly.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public TasksApiTests(ApiFactory factory) => _client = factory.CreateClient();

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
            new UpdateTaskRequest("x", null, null, TaskItemStatus.Todo), Json);

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

        // Move one to Done via PUT.
        var put = await _client.PutAsJsonAsync($"/api/tasks/{done.Id}",
            new UpdateTaskRequest(done.Title, null, null, TaskItemStatus.Done), Json);
        put.EnsureSuccessStatusCode();

        var resp = await _client.GetAsync("/api/tasks?status=Done");
        resp.EnsureSuccessStatusCode();
        var items = await resp.Content.ReadFromJsonAsync<List<TaskResponse>>(Json);

        Assert.NotNull(items);
        Assert.All(items!, t => Assert.Equal(TaskItemStatus.Done, t.Status));
        Assert.Contains(items!, t => t.Id == done.Id);
    }

    private async Task<TaskResponse> CreateAsync(string title, TaskItemStatus status = TaskItemStatus.Todo)
    {
        var resp = await _client.PostAsJsonAsync("/api/tasks",
            new CreateTaskRequest(title, null, null, status), Json);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<TaskResponse>(Json))!;
    }
}
