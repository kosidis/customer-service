using CustomerService.Api.Contracts;
using CustomerService.Api.Data;
using CustomerService.Api.Domain;
using CustomerService.Api.Errors;

namespace CustomerService.Api.Services;

public class CustomerManager(ICustomerRepository repository, TimeProvider timeProvider) : ICustomerService
{
    public async Task<CustomerResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await FindOrThrowAsync(id, cancellationToken);
        return ToResponse(customer);
    }

    public async Task<PagedResponse<CustomerResponse>> ListAsync(CustomerQuery query, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await repository.ListAsync(query.Page, query.PageSize, query.Search, cancellationToken);
        return new PagedResponse<CustomerResponse>(
            items.Select(ToResponse).ToList(), query.Page, query.PageSize, totalCount);
    }

    public async Task<CustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var email = NormaliseEmail(request.Email);
        await EnsureEmailIsUniqueAsync(email, excludeId: null, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PhoneNumber = NormaliseOptional(request.PhoneNumber),
            Domain = NormaliseOptional(request.Domain),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await repository.AddAsync(customer, cancellationToken);
        return ToResponse(customer);
    }

    public async Task<CustomerResponse> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await FindOrThrowAsync(id, cancellationToken);
        var email = NormaliseEmail(request.Email);
        await EnsureEmailIsUniqueAsync(email, id, cancellationToken);

        customer.FirstName = request.FirstName.Trim();
        customer.LastName = request.LastName.Trim();
        customer.Email = email;
        customer.PhoneNumber = NormaliseOptional(request.PhoneNumber);
        customer.Domain = NormaliseOptional(request.Domain);
        customer.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        await repository.UpdateAsync(customer, cancellationToken);
        return ToResponse(customer);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await FindOrThrowAsync(id, cancellationToken);
        await repository.DeleteAsync(customer, cancellationToken);
    }

    private async Task<Customer> FindOrThrowAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Customer '{id}' was not found.");

    private async Task EnsureEmailIsUniqueAsync(string email, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (await repository.EmailExistsAsync(email, excludeId, cancellationToken))
        {
            throw new ConflictException($"A customer with email '{email}' already exists.");
        }
    }

    private static string NormaliseEmail(string email) => email.Trim().ToLowerInvariant();

    private static string? NormaliseOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CustomerResponse ToResponse(Customer customer) => new(
        customer.Id,
        customer.FirstName,
        customer.LastName,
        customer.Email,
        customer.PhoneNumber,
        customer.Domain,
        customer.CreatedAtUtc,
        customer.UpdatedAtUtc);
}
