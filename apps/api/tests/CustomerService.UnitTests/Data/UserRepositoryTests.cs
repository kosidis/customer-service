using CustomerService.Api.Data;
using CustomerService.Api.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CustomerService.UnitTests.Data;

// Runs against SQLite in-memory so joins, the unique index and conversions behave like a real database.
public sealed class UserRepositoryTests : IDisposable
{
    private static readonly string Hash = new('a', 64);

    private readonly SqliteConnection _connection;
    private readonly CustomerDbContext _dbContext;
    private readonly UserRepository _sut;

    public UserRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _dbContext = CreateContext();
        _dbContext.Database.EnsureCreated();
        _sut = new UserRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task FindByUsernameAsync_ExistingUser_ReturnsUser()
    {
        _dbContext.Users.Add(BuildUser("ada", Role.Admin));
        await _dbContext.SaveChangesAsync();

        var found = await _sut.FindByUsernameAsync("ada", CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal("ada", found.Username);
        Assert.Equal(Role.Admin, found.Role);
        Assert.Equal(DateTimeKind.Utc, found.CreatedAtUtc.Kind);
    }

    [Fact]
    public async Task FindByUsernameAsync_UnknownUser_ReturnsNull()
    {
        var found = await _sut.FindByUsernameAsync("nobody", CancellationToken.None);

        Assert.Null(found);
    }

    [Fact]
    public async Task FindByIdAsync_ExistingUser_ReturnsUser()
    {
        var user = BuildUser("ada", Role.Viewer);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var found = await _sut.FindByIdAsync(user.Id, CancellationToken.None);

        Assert.Equal("ada", found?.Username);
        Assert.Null(await _sut.FindByIdAsync(user.Id + 1, CancellationToken.None));
    }

    [Fact]
    public async Task FindBySessionAsync_KnownHash_ReturnsOwner()
    {
        var user = BuildUser("ada", Role.Viewer);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        await _sut.AddSessionAsync(new Session { TokenHash = Hash, UserId = user.Id, CreatedAtUtc = DateTime.UtcNow }, CancellationToken.None);
        await _sut.SaveChangesAsync(CancellationToken.None);

        var owner = await _sut.FindBySessionAsync(Hash, CancellationToken.None);

        Assert.Equal(user.Id, owner?.Id);
    }

    [Fact]
    public async Task FindBySessionAsync_UnknownHash_ReturnsNull()
    {
        var owner = await _sut.FindBySessionAsync(Hash, CancellationToken.None);

        Assert.Null(owner);
    }

    [Fact]
    public async Task DeleteSessionAsync_KnownHash_RemovesRow()
    {
        var user = BuildUser("ada", Role.Viewer);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        await _sut.AddSessionAsync(new Session { TokenHash = Hash, UserId = user.Id, CreatedAtUtc = DateTime.UtcNow }, CancellationToken.None);
        await _sut.SaveChangesAsync(CancellationToken.None);

        var deleted = await _sut.DeleteSessionAsync(Hash, CancellationToken.None);

        Assert.True(deleted);
        await using var verifyContext = CreateContext();
        Assert.Empty(await verifyContext.Sessions.ToListAsync());
    }

    [Fact]
    public async Task DeleteSessionAsync_UnknownHash_ReturnsFalse()
    {
        var deleted = await _sut.DeleteSessionAsync(Hash, CancellationToken.None);

        Assert.False(deleted);
    }

    [Fact]
    public async Task SaveChangesAsync_DuplicateUsername_Throws()
    {
        _dbContext.Users.Add(BuildUser("ada", Role.Viewer));
        await _sut.SaveChangesAsync(CancellationToken.None);

        _dbContext.Users.Add(BuildUser("ada", Role.Admin));

        await Assert.ThrowsAsync<DbUpdateException>(() => _sut.SaveChangesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task AnyAsync_NoUsers_ReturnsFalse()
    {
        Assert.False(await _sut.AnyAsync(CancellationToken.None));
    }

    [Fact]
    public async Task AnyAsync_OneUser_ReturnsTrue()
    {
        _dbContext.Users.Add(BuildUser("ada", Role.Viewer));
        await _dbContext.SaveChangesAsync();

        Assert.True(await _sut.AnyAsync(CancellationToken.None));
    }

    [Fact]
    public async Task AddUserAsync_NewUser_Persists()
    {
        await _sut.AddUserAsync(BuildUser("ada", Role.Admin), CancellationToken.None);

        await using var verifyContext = CreateContext();
        var stored = await verifyContext.Users.SingleAsync();
        Assert.Equal("ada", stored.Username);
        Assert.Equal(Role.Admin, stored.Role);
    }

    [Fact]
    public async Task Role_ColumnIsNonNullBoundedString_AndRoundTrips()
    {
        var property = _dbContext.Model.FindEntityType(typeof(User))!.FindProperty(nameof(User.Role))!;
        Assert.False(property.IsNullable);
        Assert.Equal(20, property.GetMaxLength());
        _dbContext.Users.Add(BuildUser("vera", Role.Viewer));
        _dbContext.Users.Add(BuildUser("ada", Role.Admin));
        await _dbContext.SaveChangesAsync();

        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT Role FROM Users ORDER BY Username";
        var roles = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                roles.Add(reader.GetString(0));
            }
        }

        Assert.Equal(["Admin", "Viewer"], roles);
    }

    private static User BuildUser(string username, Role role) => new()
    {
        Username = username,
        PasswordHash = "hash",
        Role = role,
        MustChangePassword = false,
        CreatedAtUtc = DateTime.UtcNow,
    };

    private CustomerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CustomerDbContext>().UseSqlite(_connection).Options);
}
