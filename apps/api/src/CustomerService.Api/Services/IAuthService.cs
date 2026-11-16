using CustomerService.Api.Contracts;

namespace CustomerService.Api.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task LogoutAsync(string token, CancellationToken cancellationToken);

    Task<CurrentUserResponse> GetCurrentAsync(int userId, CancellationToken cancellationToken);

    Task ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken);
}
