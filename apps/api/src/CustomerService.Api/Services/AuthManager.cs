using System.Security.Cryptography;
using System.Text;
using CustomerService.Api.Contracts;
using CustomerService.Api.Data;
using CustomerService.Api.Domain;
using CustomerService.Api.Errors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace CustomerService.Api.Services;

public class AuthManager(
    IUserRepository users,
    IPasswordHasher<User> hasher,
    TimeProvider timeProvider,
    ILogger<AuthManager> logger) : IAuthService
{
    // Verified when the username is unknown so response time does not reveal whether it exists.
    private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(new User(), "dummy-password");

    private const string InvalidCredentials = "Invalid username or password.";

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim().ToLowerInvariant();
        var user = await users.FindByUsernameAsync(username, cancellationToken);

        var verified = user is not null
            && hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) != PasswordVerificationResult.Failed;
        if (user is null)
        {
            hasher.VerifyHashedPassword(new User(), DummyHash, request.Password);
        }

        if (!verified)
        {
            logger.LogInformation("{Event}", "SignInFailed");
            throw new UnauthorizedException(InvalidCredentials);
        }

        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        await users.AddSessionAsync(new Session
        {
            TokenHash = HashToken(token),
            UserId = user!.Id,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        }, cancellationToken);
        await users.SaveChangesAsync(cancellationToken);

        return new LoginResponse(token, user.Username, user.Role, user.MustChangePassword);
    }

    public async Task LogoutAsync(string token, CancellationToken cancellationToken)
    {
        var removed = await users.DeleteSessionAsync(HashToken(token), cancellationToken);
        if (removed)
        {
            logger.LogInformation("{Event}", "SignedOut");
        }
    }

    public async Task<CurrentUserResponse> GetCurrentAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedException(InvalidCredentials);
        return new CurrentUserResponse(user.Username, user.Role, user.MustChangePassword);
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedException(InvalidCredentials);

        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            throw new FieldValidationException("currentPassword", "The current password is incorrect.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            throw new FieldValidationException("newPassword", "The new password must not be empty.");
        }

        if (request.NewPassword == request.CurrentPassword)
        {
            throw new FieldValidationException("newPassword", "The new password must differ from the current password.");
        }

        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        user.MustChangePassword = false;
        await users.SaveChangesAsync(cancellationToken);
    }

    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
