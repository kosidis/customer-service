using CustomerService.Api.Domain;

namespace CustomerService.Api.Data;

public interface IUserRepository
{
    Task<User?> FindByUsernameAsync(string normalisedUsername, CancellationToken ct);

    Task<User?> FindByIdAsync(int id, CancellationToken ct);

    Task<User?> FindBySessionAsync(string tokenHash, CancellationToken ct);

    Task AddSessionAsync(Session session, CancellationToken ct);

    Task<bool> DeleteSessionAsync(string tokenHash, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
