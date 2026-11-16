using CustomerService.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CustomerService.Api.Data;

// Creates the first Admin when the database has no users. The account must change its password at first sign-in.
public class DefaultAdminSeeder(
    IUserRepository users,
    IPasswordHasher<User> hasher,
    TimeProvider timeProvider,
    IConfiguration configuration,
    ILogger<DefaultAdminSeeder> logger)
{
    private const string AdminUsername = "admin";

    public async Task SeedAsync(CancellationToken ct)
    {
        if (await users.AnyAsync(ct))
        {
            return;
        }

        var password = configuration["Seed:DefaultAdminPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Seed:DefaultAdminPassword must be set to create the default Admin.");
        }

        var admin = new User
        {
            Username = AdminUsername,
            Role = Role.Admin,
            MustChangePassword = true,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };
        admin.PasswordHash = hasher.HashPassword(admin, password);

        try
        {
            await users.AddUserAsync(admin, ct);
        }
        catch (DbUpdateException)
        {
            // Another replica seeded first (unique index on Username): nothing left to do.
            // If there is still no user the failure is real, so rethrow it.
            if (await users.AnyAsync(ct))
            {
                return;
            }

            throw;
        }

        logger.LogInformation("{Event}", "DefaultAdminCreated");
    }
}
