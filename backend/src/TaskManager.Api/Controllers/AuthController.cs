using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Auth;
using TaskManager.Api.Contracts;
using TaskManager.Api.Data;
using TaskManager.Api.Domain;

namespace TaskManager.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly TaskDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtIssuer _jwt;

    public AuthController(TaskDbContext db, IPasswordHasher hasher, IJwtIssuer jwt)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest body,
        CancellationToken ct)
    {
        var email = body.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            return Conflict(new ProblemDetails
            {
                Title = "Email already registered",
                Status = StatusCodes.Status409Conflict,
                Detail = "An account with this email already exists.",
            });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = _hasher.Hash(body.Password),
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return Ok(new AuthResponse(_jwt.Issue(user.Id, user.Email), _jwt.Expiry, user.Id, user.Email));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest body,
        CancellationToken ct)
    {
        var email = body.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        // One failure response for both "user not found" and "wrong password"
        // so the endpoint cannot be used to enumerate registered accounts.
        if (user is null || !_hasher.Verify(body.Password, user.PasswordHash))
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid credentials",
                Status = StatusCodes.Status401Unauthorized,
            });

        return Ok(new AuthResponse(_jwt.Issue(user.Id, user.Email), _jwt.Expiry, user.Id, user.Email));
    }
}
