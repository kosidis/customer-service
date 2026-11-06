using CustomerService.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CustomerService.Api.Data;

public class CustomerRepository(CustomerDbContext dbContext) : ICustomerRepository
{
    private const int MaxSearchWords = 5;

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<Customer> Items, int TotalCount)> ListAsync(
        int page, int pageSize, string? search, CancellationToken cancellationToken)
    {
        var query = dbContext.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Every word must match some column, so "Ada Lovelace" finds a first and last name pair.
            var words = search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Take(MaxSearchWords);
            foreach (var word in words)
            {
                query = query.Where(c =>
                    c.FirstName.Contains(word) ||
                    c.LastName.Contains(word) ||
                    c.Email.Contains(word));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<bool> EmailExistsAsync(string email, Guid? excludeId, CancellationToken cancellationToken) =>
        dbContext.Customers.AnyAsync(
            c => c.Email == email && (excludeId == null || c.Id != excludeId),
            cancellationToken);

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task UpdateAsync(Customer customer, CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task DeleteAsync(Customer customer, CancellationToken cancellationToken)
    {
        dbContext.Customers.Remove(customer);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
