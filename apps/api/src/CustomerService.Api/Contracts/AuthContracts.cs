using System.ComponentModel.DataAnnotations;
using CustomerService.Api.Domain;

namespace CustomerService.Api.Contracts;

public record LoginRequest(
    [Display(Name = "Username"), Required(ErrorMessage = "{0} is required.")] string Username,
    [Display(Name = "Password"), Required(ErrorMessage = "{0} is required.")] string Password);

public record LoginResponse(string Token, string Username, Role Role, bool MustChangePassword);

public record CurrentUserResponse(string Username, Role Role, bool MustChangePassword);

public record ChangePasswordRequest(
    [Display(Name = "Current password"), Required(ErrorMessage = "{0} is required.")] string CurrentPassword,
    [Display(Name = "New password"), Required(ErrorMessage = "{0} is required.")] string NewPassword);
