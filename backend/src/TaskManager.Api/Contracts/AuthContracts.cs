using System.ComponentModel.DataAnnotations;

namespace TaskManager.Api.Contracts;

public record RegisterRequest(
    [Required(AllowEmptyStrings = false)]
    [EmailAddress(ErrorMessage = "Email must be a valid address.")]
    [StringLength(254)]
    string Email,

    [Required(AllowEmptyStrings = false)]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    string Password
);

public record LoginRequest(
    [Required(AllowEmptyStrings = false)]
    [EmailAddress]
    string Email,

    [Required(AllowEmptyStrings = false)]
    string Password
);

public record AuthResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    string Email
);
