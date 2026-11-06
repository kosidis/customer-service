using CustomerService.Api.Domain;

namespace CustomerService.Api.Data;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Customer> Items, int TotalCount)> ListAsync(
        int page, int pageSize, string? search, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string email, Guid? excludeId, CancellationToken cancellationToken);

    Task AddAsync(Customer customer, CancellationToken cancellationToken);

    Task UpdateAsync(Customer customer, CancellationToken cancellationToken);

    Task DeleteAsync(Customer customer, CancellationToken cancellationToken);
}
