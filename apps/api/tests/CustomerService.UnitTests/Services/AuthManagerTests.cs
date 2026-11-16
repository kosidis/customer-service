using Microsoft.Extensions.Logging.Abstractions;
using CustomerService.Api.Contracts;
using CustomerService.Api.Data;
using CustomerService.Api.Domain;
using CustomerService.Api.Errors;
using CustomerService.Api.Services;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace CustomerService.UnitTests.Services;

public class AuthManagerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

    private readonly Mock<IUserRepository> _users = new(MockBehavior.Strict);
    private readonly Mock<IPasswordHasher<User>> _hasher = new(MockBehavior.Strict);
    private readonly FixedTimeProvider _timeProvider = new(Now);
    private readonly AuthManager _sut;

    public AuthManagerTests()
    {
        _sut = new AuthManager(_users.Object, _hasher.Object, _timeProvider, NullLogger<AuthManager>.Instance);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokenAndStoresOnlyHash()
    {
        var user = BuildUser();
        Session? stored = null;
        _users.Setup(u => u.FindByUsernameAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.VerifyHashedPassword(user, user.PasswordHash, "secret"))
            .Returns(PasswordVerificationResult.Success);
        _users.Setup(u => u.AddSessionAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .Callback<Session, CancellationToken>((s, _) => stored = s)
            .Returns(Task.CompletedTask);
        _users.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _sut.LoginAsync(new LoginRequest("alice", "secret"), CancellationToken.None);

        Assert.Equal("alice", result.Username);
        Assert.Equal(Role.Viewer, result.Role);
        Assert.False(result.MustChangePassword);
        Assert.Equal(43, result.Token.Length);
        Assert.DoesNotContain('+', result.Token);
        Assert.DoesNotContain('/', result.Token);
        Assert.DoesNotContain('=', result.Token);
        Assert.NotNull(stored);
        Assert.Equal(AuthManager.HashToken(result.Token), stored!.TokenHash);
        Assert.NotEqual(result.Token, stored.TokenHash);
        Assert.Equal(64, stored.TokenHash.Length);
        Assert.Equal(user.Id, stored.UserId);
        Assert.Equal(Now.UtcDateTime, stored.CreatedAtUtc);
        _users.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_UsernameWithPaddingAndCapitals_Normalises()
    {
        var user = BuildUser();
        _users.Setup(u => u.FindByUsernameAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.VerifyHashedPassword(user, user.PasswordHash, "secret"))
            .Returns(PasswordVerificationResult.Success);
        _users.Setup(u => u.AddSessionAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _users.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _sut.LoginAsync(new LoginRequest("  AlIcE  ", "secret"), CancellationToken.None);

        Assert.Equal("alice", result.Username);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorized()
    {
        var user = BuildUser();
        _users.Setup(u => u.FindByUsernameAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.VerifyHashedPassword(user, user.PasswordHash, "nope"))
            .Returns(PasswordVerificationResult.Failed);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _sut.LoginAsync(new LoginRequest("alice", "nope"), CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_UnknownUser_RunsHasherAndThrowsUnauthorized()
    {
        string? hashUsed = null;
        _users.Setup(u => u.FindByUsernameAsync("ghost", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        _hasher.Setup(h => h.VerifyHashedPassword(It.IsAny<User>(), It.IsAny<string>(), "whatever"))
            .Callback<User, string, string>((_, hash, _) => hashUsed = hash)
            .Returns(PasswordVerificationResult.Failed);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _sut.LoginAsync(new LoginRequest("ghost", "whatever"), CancellationToken.None));

        _hasher.Verify(h => h.VerifyHashedPassword(It.IsAny<User>(), It.IsAny<string>(), "whatever"), Times.Once);
        Assert.False(string.IsNullOrEmpty(hashUsed));
    }

    [Fact]
    public async Task LoginAsync_PendingUser_ReturnsMustChangePasswordTrue()
    {
        var user = BuildUser(mustChange: true, role: Role.Admin);
        _users.Setup(u => u.FindByUsernameAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.VerifyHashedPassword(user, user.PasswordHash, "secret"))
            .Returns(PasswordVerificationResult.Success);
        _users.Setup(u => u.AddSessionAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _users.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _sut.LoginAsync(new LoginRequest("alice", "secret"), CancellationToken.None);

        Assert.True(result.MustChangePassword);
        Assert.Equal(Role.Admin, result.Role);
    }

    [Fact]
    public async Task GetCurrentAsync_ExistingUser_ReturnsMappedResponse()
    {
        var user = BuildUser(mustChange: true);
        _users.Setup(u => u.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _sut.GetCurrentAsync(user.Id, CancellationToken.None);

        Assert.Equal(new CurrentUserResponse("alice", Role.Viewer, true), result);
    }

    [Fact]
    public async Task GetCurrentAsync_MissingUser_ThrowsUnauthorized()
    {
        _users.Setup(u => u.FindByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<UnauthorizedException>(() => _sut.GetCurrentAsync(9, CancellationToken.None));
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrent_ThrowsFieldValidationOnCurrentPassword()
    {
        var user = BuildUser();
        _users.Setup(u => u.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.VerifyHashedPassword(user, user.PasswordHash, "wrong"))
            .Returns(PasswordVerificationResult.Failed);

        var ex = await Assert.ThrowsAsync<FieldValidationException>(
            () => _sut.ChangePasswordAsync(user.Id, new ChangePasswordRequest("wrong", "new-pass"), CancellationToken.None));

        Assert.Equal("currentPassword", ex.Field);
    }

    [Fact]
    public async Task ChangePasswordAsync_WhitespaceNew_ThrowsFieldValidationOnNewPassword()
    {
        var user = BuildUser();
        _users.Setup(u => u.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.VerifyHashedPassword(user, user.PasswordHash, "secret"))
            .Returns(PasswordVerificationResult.Success);

        var ex = await Assert.ThrowsAsync<FieldValidationException>(
            () => _sut.ChangePasswordAsync(user.Id, new ChangePasswordRequest("secret", "   "), CancellationToken.None));

        Assert.Equal("newPassword", ex.Field);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChangePasswordAsync_SameAsCurrent_ThrowsFieldValidationOnNewPassword(bool mustChange)
    {
        var user = BuildUser(mustChange: mustChange);
        _users.Setup(u => u.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.VerifyHashedPassword(user, user.PasswordHash, "secret"))
            .Returns(PasswordVerificationResult.Success);

        var ex = await Assert.ThrowsAsync<FieldValidationException>(
            () => _sut.ChangePasswordAsync(user.Id, new ChangePasswordRequest("secret", "secret"), CancellationToken.None));

        Assert.Equal("newPassword", ex.Field);
    }

    [Fact]
    public async Task ChangePasswordAsync_MissingUser_ThrowsUnauthorized()
    {
        _users.Setup(u => u.FindByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _sut.ChangePasswordAsync(9, new ChangePasswordRequest("a", "b"), CancellationToken.None));
    }

    [Fact]
    public async Task ChangePasswordAsync_Valid_StoresNewHashAndClearsFlag()
    {
        var user = BuildUser(mustChange: true);
        _users.Setup(u => u.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.VerifyHashedPassword(user, "old-hash", "secret"))
            .Returns(PasswordVerificationResult.Success);
        _hasher.Setup(h => h.HashPassword(user, "brand-new")).Returns("new-hash");
        _users.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _sut.ChangePasswordAsync(user.Id, new ChangePasswordRequest("secret", "brand-new"), CancellationToken.None);

        Assert.Equal("new-hash", user.PasswordHash);
        Assert.False(user.MustChangePassword);
        _users.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_Valid_LeavesSessionsAlone()
    {
        var user = BuildUser();
        _users.Setup(u => u.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.VerifyHashedPassword(user, user.PasswordHash, "secret"))
            .Returns(PasswordVerificationResult.Success);
        _hasher.Setup(h => h.HashPassword(user, "brand-new")).Returns("new-hash");
        _users.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _sut.ChangePasswordAsync(user.Id, new ChangePasswordRequest("secret", "brand-new"), CancellationToken.None);

        _users.Verify(u => u.DeleteSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LogoutAsync_KnownToken_DeletesSession()
    {
        _users.Setup(u => u.DeleteSessionAsync(AuthManager.HashToken("tok"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.LogoutAsync("tok", CancellationToken.None);

        _users.Verify(u => u.DeleteSessionAsync(AuthManager.HashToken("tok"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_UnknownToken_DoesNothing()
    {
        _users.Setup(u => u.DeleteSessionAsync(AuthManager.HashToken("tok"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.LogoutAsync("tok", CancellationToken.None);

        _users.Verify(u => u.DeleteSessionAsync(AuthManager.HashToken("tok"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void HashToken_KnownInput_ReturnsLowerHexSha256()
    {
        Assert.Equal(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            AuthManager.HashToken("abc"));
    }

    private static User BuildUser(bool mustChange = false, Role role = Role.Viewer) => new()
    {
        Id = 7,
        Username = "alice",
        PasswordHash = "old-hash",
        Role = role,
        MustChangePassword = mustChange,
        CreatedAtUtc = Now.UtcDateTime
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
