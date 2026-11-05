using CustomerService.Api.Contracts;

namespace CustomerService.Api.Services;

public interface ICustomerService
{
    Task<CustomerResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResponse<CustomerResponse>> ListAsync(CustomerQuery query, CancellationToken cancellationToken);

    Task<CustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken);

    Task<CustomerResponse> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
