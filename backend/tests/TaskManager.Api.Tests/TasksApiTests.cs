using System.Net;
using System.Net.Http.Headers;
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
        // Authenticate the shared client as a unique user for this test class.
        var token = RegisterFresh().GetAwaiter().GetResult();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> RegisterFresh()
    {
        var email = $"u{Guid.NewGuid():N}@example.com";
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "correct-horse-battery-staple"), Json);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<AuthResponse>(Json);
        return body!.Token;
    }

    private HttpClient ClientFor(string token)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    [Fact]
    public async Task UnauthenticatedRequest_Returns401()
    {
        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
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
    public async Task Update_WithStaleVersion_Returns409WithCurrentState()
    {
        var created = await CreateAsync(_client, "Original title");

        var firstUpdate = await _client.PutAsJsonAsync($"/api/tasks/{created.Id}",
            new UpdateTaskRequest("First edit", null, null, TaskItemStatus.Todo, created.Version), Json);
        firstUpdate.EnsureSuccessStatusCode();

        var stale = await _client.PutAsJsonAsync($"/api/tasks/{created.Id}",
            new UpdateTaskRequest("Stale edit", null, null, TaskItemStatus.Todo, created.Version), Json);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var body = await stale.Content.ReadAsStringAsync();
        Assert.Contains("\"current\"", body);
        Assert.Contains("First edit", body);
    }

    [Fact]
    public async Task List_FiltersByStatus()
    {
        await CreateAsync(_client, "Todo item", TaskItemStatus.Todo);
        var done = await CreateAsync(_client, "Done item", TaskItemStatus.Todo);

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
    public async Task Delete_SoftDeletes_RowSurvivesButHiddenFromQueries()
    {
        var created = await CreateAsync(_client, "Delete me");

        var del = await _client.DeleteAsync($"/api/tasks/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var get = await _client.GetAsync($"/api/tasks/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        // The row is still in the database — bypass the soft-delete query
        // filter to verify the policy.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();
        var row = await db.Tasks.IgnoreQueryFilters().SingleAsync(t => t.Id == created.Id);
        Assert.NotNull(row.DeletedAt);
    }

    [Fact]
    public async Task Delete_MissingId_Returns404()
    {
        var resp = await _client.DeleteAsync($"/api/tasks/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // -------- Cross-user ownership tests --------

    [Fact]
    public async Task UserA_CannotReadUserBsTask_Returns404()
    {
        var alice = _client;
        var bob = ClientFor(await RegisterFresh());

        var bobsTask = await CreateAsync(bob, "Bob's private task");

        var resp = await alice.GetAsync($"/api/tasks/{bobsTask.Id}");
        // 404 (not 403) to avoid revealing that the id exists.
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task UserA_CannotUpdateUserBsTask_Returns404()
    {
        var alice = _client;
        var bob = ClientFor(await RegisterFresh());

        var bobsTask = await CreateAsync(bob, "Bob's task");

        var resp = await alice.PutAsJsonAsync($"/api/tasks/{bobsTask.Id}",
            new UpdateTaskRequest("Alice steals", null, null, TaskItemStatus.Done, bobsTask.Version), Json);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);

        // And Bob's task is unchanged.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();
        var row = await db.Tasks.IgnoreQueryFilters().SingleAsync(t => t.Id == bobsTask.Id);
        Assert.Equal("Bob's task", row.Title);
        Assert.Equal(TaskItemStatus.Todo, row.Status);
    }

    [Fact]
    public async Task UserA_CannotDeleteUserBsTask_Returns404()
    {
        var alice = _client;
        var bob = ClientFor(await RegisterFresh());

        var bobsTask = await CreateAsync(bob, "Don't delete me");

        var resp = await alice.DeleteAsync($"/api/tasks/{bobsTask.Id}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);

        // And Bob can still see it through their own client.
        var bobsView = await bob.GetAsync($"/api/tasks/{bobsTask.Id}");
        Assert.Equal(HttpStatusCode.OK, bobsView.StatusCode);
    }

    [Fact]
    public async Task List_OnlyReturnsCurrentUsersTasks()
    {
        var alice = _client;
        var bob = ClientFor(await RegisterFresh());

        var aliceTask = await CreateAsync(alice, "Alice's task");
        var bobTask = await CreateAsync(bob, "Bob's task");

        var aliceList = await (await alice.GetAsync("/api/tasks")).Content
            .ReadFromJsonAsync<List<TaskResponse>>(Json);
        var bobList = await (await bob.GetAsync("/api/tasks")).Content
            .ReadFromJsonAsync<List<TaskResponse>>(Json);

        Assert.Contains(aliceList!, t => t.Id == aliceTask.Id);
        Assert.DoesNotContain(aliceList!, t => t.Id == bobTask.Id);
        Assert.Contains(bobList!, t => t.Id == bobTask.Id);
        Assert.DoesNotContain(bobList!, t => t.Id == aliceTask.Id);
    }

    private static async Task<TaskResponse> CreateAsync(
        HttpClient client, string title, TaskItemStatus status = TaskItemStatus.Todo)
    {
        var resp = await client.PostAsJsonAsync("/api/tasks",
            new CreateTaskRequest(title, null, null, status), Json);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<TaskResponse>(Json))!;
    }
}
