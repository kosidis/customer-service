namespace CustomerService.Api.Domain;

public class Session
{
    // 64-character lower-case hex SHA-256 of the opaque token; the token itself is never stored.
    public string TokenHash { get; set; } = string.Empty;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}
