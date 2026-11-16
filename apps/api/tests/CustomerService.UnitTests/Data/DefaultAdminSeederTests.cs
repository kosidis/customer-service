using CustomerService.Api.Data;
using CustomerService.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CustomerService.UnitTests.Data;

public class DefaultAdminSeederTests
{
    private const string Password = "ChangeMe-Now-1";
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private readonly Mock<IUserRepository> _users = new(MockBehavior.Strict);
    private readonly PasswordHasher<User> _hasher = new();

    private DefaultAdminSeeder CreateSut(string? password = Password) => new(
        _users.Object,
        _hasher,
        new FixedTimeProvider(Now),
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:DefaultAdminPassword"] = password })
            .Build(),
        NullLogger<DefaultAdminSeeder>.Instance);

    [Fact]
    public async Task SeedAsync_NoUsers_AddsAdminWithRoleAndPendingChange()
    {
        User? added = null;
        _users.Setup(u => u.AnyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _users.Setup(u => u.AddUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => added = user)
            .Returns(Task.CompletedTask);

        await CreateSut().SeedAsync(CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal("admin", added.Username);
        Assert.Equal(Role.Admin, added.Role);
        Assert.True(added.MustChangePassword);
        Assert.Equal(Now.UtcDateTime, added.CreatedAtUtc);
        Assert.NotEqual(PasswordVerificationResult.Failed, _hasher.VerifyHashedPassword(added, added.PasswordHash, Password));
    }

    [Fact]
    public async Task SeedAsync_UsersExist_DoesNothing()
    {
        _users.Setup(u => u.AnyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await CreateSut().SeedAsync(CancellationToken.None);

        _users.Verify(u => u.AddUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SeedAsync_BlankPassword_Throws(string? password)
    {
        _users.Setup(u => u.AnyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut(password).SeedAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SeedAsync_InsertLosesRace_IgnoresWhenUsersNowExist()
    {
        _users.SetupSequence(u => u.AnyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false).ReturnsAsync(true);
        _users.Setup(u => u.AddUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("duplicate"));

        await CreateSut().SeedAsync(CancellationToken.None);
    }

    [Fact]
    public async Task SeedAsync_InsertFailsAndStillEmpty_Rethrows()
    {
        _users.Setup(u => u.AnyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _users.Setup(u => u.AddUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("down"));

        await Assert.ThrowsAsync<DbUpdateException>(() => CreateSut().SeedAsync(CancellationToken.None));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
