using System.Security.Claims;
using TaskManager.Api.Data;

namespace TaskManager.Api.Auth;

// Reads the authenticated user's id from the request's claims. Returns null
// for anonymous requests (e.g. /auth/register and /auth/login), which is what
// the DbContext query filter checks for before applying ownership scoping.
public class CurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _http;
    public CurrentUserAccessor(IHttpContextAccessor http) => _http = http;

    public Guid? UserId
    {
        get
        {
            var sub = _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? _http.HttpContext?.User.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }
}
