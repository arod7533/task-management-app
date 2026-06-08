using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskManager.Api.Contracts;

namespace TaskManager.Api.Tests;

public class AuthApiTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public AuthApiTests(ApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Register_WithValidCredentials_ReturnsToken()
    {
        var email = $"u{Guid.NewGuid():N}@example.com";
        var resp = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "correct-horse-battery-staple"), Json);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal(email, body.Email);
    }

    [Fact]
    public async Task Register_WithShortPassword_Returns400()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest($"u{Guid.NewGuid():N}@example.com", "short"), Json);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409()
    {
        var email = $"u{Guid.NewGuid():N}@example.com";
        var first = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "correct-horse-battery-staple"), Json);
        first.EnsureSuccessStatusCode();

        var second = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "correct-horse-battery-staple"), Json);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsToken()
    {
        var email = $"u{Guid.NewGuid():N}@example.com";
        var pw = "correct-horse-battery-staple";
        var reg = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, pw), Json);
        reg.EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, pw), Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var body = await login.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var email = $"u{Guid.NewGuid():N}@example.com";
        var reg = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "correct-horse-battery-staple"), Json);
        reg.EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "wrong-password-x123"), Json);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401SameAsWrongPassword()
    {
        // The endpoint should not distinguish "no such user" from "wrong
        // password" — otherwise it's a free account-enumeration oracle.
        var resp = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("nobody@example.com", "anything"), Json);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
