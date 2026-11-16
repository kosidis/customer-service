using CustomerService.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CustomerService.Api.Data;

public class UserRepository(CustomerDbContext dbContext) : IUserRepository
{
    public Task<User?> FindByUsernameAsync(string normalisedUsername, CancellationToken ct) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.Username == normalisedUsername, ct);

    public Task<User?> FindByIdAsync(int id, CancellationToken ct) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> FindBySessionAsync(string tokenHash, CancellationToken ct) =>
        dbContext.Sessions
            .Where(s => s.TokenHash == tokenHash)
            .Select(s => s.User)
            .FirstOrDefaultAsync(ct);

    public async Task AddSessionAsync(Session session, CancellationToken ct) =>
        await dbContext.Sessions.AddAsync(session, ct);

    public async Task<bool> DeleteSessionAsync(string tokenHash, CancellationToken ct)
    {
        var session = await dbContext.Sessions.FirstOrDefaultAsync(s => s.TokenHash == tokenHash, ct);
        if (session is null)
        {
            return false;
        }

        dbContext.Sessions.Remove(session);
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
