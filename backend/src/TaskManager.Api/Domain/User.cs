namespace TaskManager.Api.Domain;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    // Encoded as "$argon2id$v=19$m=...,t=...,p=...$<salt-b64>$<hash-b64>"
    // so the hash carries its own parameters and can be re-verified after a
    // parameter upgrade without a schema change.
    public string PasswordHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
