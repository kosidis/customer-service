using CustomerService.Api.Data;
using CustomerService.Api.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CustomerService.UnitTests.Data;

// Runs against SQLite in-memory so queries, ordering and the unique index behave like a real database.
public sealed class CustomerRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CustomerDbContext _dbContext;
    private readonly CustomerRepository _sut;

    public CustomerRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _dbContext = CreateContext();
        _dbContext.Database.EnsureCreated();
        _sut = new CustomerRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task AddAsync_PersistsCustomer()
    {
        var customer = BuildCustomer("Jane", "Doe", "jane@example.com");

        await _sut.AddAsync(customer, CancellationToken.None);

        await using var verifyContext = CreateContext();
        var stored = await verifyContext.Customers.SingleAsync();
        Assert.Equal(customer.Id, stored.Id);
        Assert.Equal("jane@example.com", stored.Email);
        Assert.Equal(DateTimeKind.Utc, stored.CreatedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, stored.UpdatedAtUtc.Kind);
    }

    [Fact]
    public async Task AddAsync_DuplicateEmail_ViolatesUniqueIndex()
    {
        await _sut.AddAsync(BuildCustomer("Jane", "Doe", "jane@example.com"), CancellationToken.None);

        await using var otherContext = CreateContext();
        var otherRepository = new CustomerRepository(otherContext);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            otherRepository.AddAsync(BuildCustomer("Janet", "Doe", "jane@example.com"), CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCustomerOrNull()
    {
        var customer = BuildCustomer("Jane", "Doe", "jane@example.com");
        await _sut.AddAsync(customer, CancellationToken.None);

        Assert.Equal(customer.Id, (await _sut.GetByIdAsync(customer.Id, CancellationToken.None))?.Id);
        Assert.Null(await _sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ListAsync_OrdersByLastNameThenFirstNameAndPages()
    {
        await SeedAsync(
            BuildCustomer("Zoe", "Brown", "zoe@example.com"),
            BuildCustomer("Adam", "Brown", "adam@example.com"),
            BuildCustomer("Carl", "Adams", "carl@example.com"),
            BuildCustomer("Dana", "Clark", "dana@example.com"));

        var (firstPage, total) = await _sut.ListAsync(1, 2, null, CancellationToken.None);
        var (secondPage, _) = await _sut.ListAsync(2, 2, null, CancellationToken.None);

        Assert.Equal(4, total);
        Assert.Equal(["Carl", "Adam"], firstPage.Select(c => c.FirstName));
        Assert.Equal(["Zoe", "Dana"], secondPage.Select(c => c.FirstName));
    }

    [Theory]
    [InlineData("Brown", 2)]
    [InlineData("carl", 1)]
    [InlineData("example.com", 4)]
    [InlineData("  Clark  ", 1)]
    [InlineData("nobody", 0)]
    [InlineData("   ", 4)]
    public async Task ListAsync_FiltersOnNameAndEmail(string search, int expected)
    {
        await SeedAsync(
            BuildCustomer("Zoe", "Brown", "zoe@example.com"),
            BuildCustomer("Adam", "Brown", "adam@example.com"),
            BuildCustomer("Carl", "Adams", "carl@example.com"),
            BuildCustomer("Dana", "Clark", "dana@example.com"));

        var (items, total) = await _sut.ListAsync(1, 10, search, CancellationToken.None);

        Assert.Equal(expected, total);
        Assert.Equal(expected, items.Count);
    }

    [Fact]
    public async Task EmailExistsAsync_RespectsExcludedId()
    {
        var customer = BuildCustomer("Jane", "Doe", "jane@example.com");
        await _sut.AddAsync(customer, CancellationToken.None);

        Assert.True(await _sut.EmailExistsAsync("jane@example.com", null, CancellationToken.None));
        Assert.True(await _sut.EmailExistsAsync("jane@example.com", Guid.NewGuid(), CancellationToken.None));
        Assert.False(await _sut.EmailExistsAsync("jane@example.com", customer.Id, CancellationToken.None));
        Assert.False(await _sut.EmailExistsAsync("other@example.com", null, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_SavesTrackedChanges()
    {
        var customer = BuildCustomer("Jane", "Doe", "jane@example.com");
        await _sut.AddAsync(customer, CancellationToken.None);

        customer.LastName = "Smith";
        await _sut.UpdateAsync(customer, CancellationToken.None);

        await using var verifyContext = CreateContext();
        Assert.Equal("Smith", (await verifyContext.Customers.SingleAsync()).LastName);
    }

    [Fact]
    public async Task DeleteAsync_RemovesCustomer()
    {
        var customer = BuildCustomer("Jane", "Doe", "jane@example.com");
        await _sut.AddAsync(customer, CancellationToken.None);

        await _sut.DeleteAsync(customer, CancellationToken.None);

        await using var verifyContext = CreateContext();
        Assert.False(await verifyContext.Customers.AnyAsync());
    }

    private CustomerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CustomerDbContext>().UseSqlite(_connection).Options);

    private async Task SeedAsync(params Customer[] customers)
    {
        _dbContext.Customers.AddRange(customers);
        await _dbContext.SaveChangesAsync();
    }

    private static Customer BuildCustomer(string firstName, string lastName, string email) => new()
    {
        Id = Guid.NewGuid(),
        FirstName = firstName,
        LastName = lastName,
        Email = email,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };
}
