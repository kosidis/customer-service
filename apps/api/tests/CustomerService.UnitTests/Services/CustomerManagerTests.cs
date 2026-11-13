using CustomerService.Api.Contracts;
using CustomerService.Api.Data;
using CustomerService.Api.Domain;
using CustomerService.Api.Errors;
using CustomerService.Api.Services;
using Moq;

namespace CustomerService.UnitTests.Services;

public class CustomerManagerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

    private readonly Mock<ICustomerRepository> _repository = new(MockBehavior.Strict);
    private readonly FixedTimeProvider _timeProvider = new(Now);
    private readonly CustomerManager _sut;

    public CustomerManagerTests()
    {
        _sut = new CustomerManager(_repository.Object, _timeProvider);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingCustomer_ReturnsMappedResponse()
    {
        var customer = BuildCustomer();
        _repository.Setup(r => r.GetByIdAsync(customer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(customer);

        var result = await _sut.GetByIdAsync(customer.Id, CancellationToken.None);

        Assert.Equal(customer.Id, result.Id);
        Assert.Equal(customer.FirstName, result.FirstName);
        Assert.Equal(customer.LastName, result.LastName);
        Assert.Equal(customer.Email, result.Email);
        Assert.Equal(customer.PhoneNumber, result.PhoneNumber);
        Assert.Equal(customer.CreatedAtUtc, result.CreatedAtUtc);
        Assert.Equal(customer.UpdatedAtUtc, result.UpdatedAtUtc);
    }

    [Fact]
    public async Task GetByIdAsync_MissingCustomer_ThrowsNotFound()
    {
        var id = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Customer?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetByIdAsync(id, CancellationToken.None));

        Assert.Contains(id.ToString(), exception.Message);
    }

    [Fact]
    public async Task ListAsync_PassesQueryToRepositoryAndMapsPage()
    {
        var customers = new List<Customer> { BuildCustomer(), BuildCustomer("other@example.com") };
        var query = new CustomerQuery { Page = 2, PageSize = 2, Search = "doe" };
        _repository
            .Setup(r => r.ListAsync(2, 2, "doe", It.IsAny<CancellationToken>()))
            .ReturnsAsync((customers, 5));

        var result = await _sut.ListAsync(query, CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(customers.Select(c => c.Id), result.Items.Select(i => i.Id));
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_NormalisesInputAndPersists()
    {
        Customer? saved = null;
        _repository
            .Setup(r => r.EmailExistsAsync("jane.doe@example.com", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()))
            .Callback<Customer, CancellationToken>((c, _) => saved = c)
            .Returns(Task.CompletedTask);

        var request = new CreateCustomerRequest("  Jane ", " Doe ", " Jane.Doe@Example.com ", " +44 20 7946 0000 ", null);
        var result = await _sut.CreateAsync(request, CancellationToken.None);

        Assert.NotNull(saved);
        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.Equal("Jane", saved.FirstName);
        Assert.Equal("Doe", saved.LastName);
        Assert.Equal("jane.doe@example.com", saved.Email);
        Assert.Equal("+44 20 7946 0000", saved.PhoneNumber);
        Assert.Equal(Now.UtcDateTime, saved.CreatedAtUtc);
        Assert.Equal(Now.UtcDateTime, saved.UpdatedAtUtc);
        Assert.Equal(saved.Id, result.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_BlankPhone_StoresNull(string? phone)
    {
        Customer? saved = null;
        _repository
            .Setup(r => r.EmailExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()))
            .Callback<Customer, CancellationToken>((c, _) => saved = c)
            .Returns(Task.CompletedTask);

        await _sut.CreateAsync(new CreateCustomerRequest("Jane", "Doe", "jane@example.com", phone, null), CancellationToken.None);

        Assert.Null(saved!.PhoneNumber);
    }

    [Theory]
    [InlineData("  example.com ", "example.com")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public async Task CreateAsync_Domain_IsTrimmedOrStoredAsNull(string? domain, string? expected)
    {
        Customer? saved = null;
        _repository
            .Setup(r => r.EmailExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()))
            .Callback<Customer, CancellationToken>((c, _) => saved = c)
            .Returns(Task.CompletedTask);

        var result = await _sut.CreateAsync(new CreateCustomerRequest("Jane", "Doe", "jane@example.com", null, domain), CancellationToken.None);

        Assert.Equal(expected, saved!.Domain);
        Assert.Equal(expected, result.Domain);
    }

    [Theory]
    [InlineData("  Example.com ", "Example.com")]
    [InlineData("   ", null)]
    public async Task UpdateAsync_Domain_IsTrimmedOrStoredAsNull(string domain, string? expected)
    {
        var customer = BuildCustomer();
        customer.Domain = "old.example.com";
        _repository.Setup(r => r.GetByIdAsync(customer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        _repository
            .Setup(r => r.EmailExistsAsync("new@example.com", customer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repository.Setup(r => r.UpdateAsync(customer, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _sut.UpdateAsync(customer.Id, new UpdateCustomerRequest("A", "B", "new@example.com", null, domain), CancellationToken.None);

        Assert.Equal(expected, customer.Domain);
        Assert.Equal(expected, result.Domain);
    }

    [Fact]
    public async Task GetByIdAsync_CustomerWithDomain_MapsDomain()
    {
        var customer = BuildCustomer();
        customer.Domain = "example.com";
        _repository.Setup(r => r.GetByIdAsync(customer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(customer);

        var result = await _sut.GetByIdAsync(customer.Id, CancellationToken.None);

        Assert.Equal("example.com", result.Domain);
    }

    [Fact]
    public async Task CreateAsync_DuplicateEmail_ThrowsConflictAndDoesNotPersist()
    {
        _repository
            .Setup(r => r.EmailExistsAsync("jane@example.com", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.CreateAsync(new CreateCustomerRequest("Jane", "Doe", "JANE@example.com", null, null), CancellationToken.None));

        _repository.Verify(r => r.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ExistingCustomer_UpdatesFieldsAndTimestamp()
    {
        var customer = BuildCustomer();
        var originalCreatedAt = customer.CreatedAtUtc;
        _repository.Setup(r => r.GetByIdAsync(customer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        _repository
            .Setup(r => r.EmailExistsAsync("new@example.com", customer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repository.Setup(r => r.UpdateAsync(customer, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _sut.UpdateAsync(
            customer.Id,
            new UpdateCustomerRequest(" John ", " Smith ", "New@Example.com", null, null),
            CancellationToken.None);

        Assert.Equal("John", result.FirstName);
        Assert.Equal("Smith", result.LastName);
        Assert.Equal("new@example.com", result.Email);
        Assert.Null(result.PhoneNumber);
        Assert.Equal(originalCreatedAt, result.CreatedAtUtc);
        Assert.Equal(Now.UtcDateTime, result.UpdatedAtUtc);
        _repository.Verify(r => r.UpdateAsync(customer, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_MissingCustomer_ThrowsNotFound()
    {
        var id = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Customer?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.UpdateAsync(id, new UpdateCustomerRequest("A", "B", "a@example.com", null, null), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_EmailTakenByAnotherCustomer_ThrowsConflict()
    {
        var customer = BuildCustomer();
        _repository.Setup(r => r.GetByIdAsync(customer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        _repository
            .Setup(r => r.EmailExistsAsync("taken@example.com", customer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.UpdateAsync(customer.Id, new UpdateCustomerRequest("A", "B", "taken@example.com", null, null), CancellationToken.None));

        _repository.Verify(r => r.UpdateAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_ExistingCustomer_Deletes()
    {
        var customer = BuildCustomer();
        _repository.Setup(r => r.GetByIdAsync(customer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        _repository.Setup(r => r.DeleteAsync(customer, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _sut.DeleteAsync(customer.Id, CancellationToken.None);

        _repository.Verify(r => r.DeleteAsync(customer, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_MissingCustomer_ThrowsNotFound()
    {
        var id = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Customer?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.DeleteAsync(id, CancellationToken.None));
    }

    private static Customer BuildCustomer(string email = "jane@example.com") => new()
    {
        Id = Guid.NewGuid(),
        FirstName = "Jane",
        LastName = "Doe",
        Email = email,
        PhoneNumber = "+44 20 7946 0000",
        CreatedAtUtc = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAtUtc = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
