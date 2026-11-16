using System.ComponentModel.DataAnnotations;
using CustomerService.Api.Domain;

namespace CustomerService.Api.Contracts;

public record LoginRequest([Required] string Username, [Required] string Password);

public record LoginResponse(string Token, string Username, Role Role, bool MustChangePassword);

public record CurrentUserResponse(string Username, Role Role, bool MustChangePassword);

public record ChangePasswordRequest([Required] string CurrentPassword, [Required] string NewPassword);
