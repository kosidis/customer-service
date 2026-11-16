using System.ComponentModel.DataAnnotations;

namespace CustomerService.Api.Contracts;

public record CreateCustomerRequest(
    [Required, StringLength(100, MinimumLength = 1)] string FirstName,
    [Required, StringLength(100, MinimumLength = 1)] string LastName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Phone, StringLength(32)] string? PhoneNumber,
    [StringLength(253)] string? Domain);

public record UpdateCustomerRequest(
    [Required, StringLength(100, MinimumLength = 1)] string FirstName,
    [Required, StringLength(100, MinimumLength = 1)] string LastName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Phone, StringLength(32)] string? PhoneNumber,
    [StringLength(253)] string? Domain);

public record CustomerResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string? Domain,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public record CustomerQuery
{
    public const int MaxPageSize = 100;

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; init; } = 20;

    [StringLength(100)]
    public string? Search { get; init; }
}

public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
