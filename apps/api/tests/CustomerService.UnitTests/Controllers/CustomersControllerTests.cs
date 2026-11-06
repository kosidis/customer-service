using CustomerService.Api.Contracts;
using CustomerService.Api.Controllers;
using CustomerService.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CustomerService.UnitTests.Controllers;

public class CustomersControllerTests
{
    private readonly Mock<ICustomerService> _service = new(MockBehavior.Strict);
    private readonly CustomersController _sut;

    public CustomersControllerTests()
    {
        _sut = new CustomersController(_service.Object);
    }

    [Fact]
    public async Task List_ReturnsOkWithPage()
    {
        var query = new CustomerQuery { Page = 1, PageSize = 10 };
        var page = new PagedResponse<CustomerResponse>([BuildResponse()], 1, 10, 1);
        _service.Setup(s => s.ListAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(page);

        var result = await _sut.List(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(page, ok.Value);
    }

    [Fact]
    public async Task GetById_ReturnsOkWithCustomer()
    {
        var customer = BuildResponse();
        _service.Setup(s => s.GetByIdAsync(customer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(customer);

        var result = await _sut.GetById(customer.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(customer, ok.Value);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtGetById()
    {
        var request = new CreateCustomerRequest("Jane", "Doe", "jane@example.com", null);
        var customer = BuildResponse();
        _service.Setup(s => s.CreateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(customer);

        var result = await _sut.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtRouteResult>(result.Result);
        Assert.Equal(nameof(CustomersController.GetById), created.RouteName);
        Assert.Equal(customer.Id, created.RouteValues!["id"]);
        Assert.Same(customer, created.Value);
    }

    [Fact]
    public async Task Update_ReturnsOkWithUpdatedCustomer()
    {
        var customer = BuildResponse();
        var request = new UpdateCustomerRequest("Jane", "Doe", "jane@example.com", null);
        _service.Setup(s => s.UpdateAsync(customer.Id, request, It.IsAny<CancellationToken>())).ReturnsAsync(customer);

        var result = await _sut.Update(customer.Id, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(customer, ok.Value);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _service.Setup(s => s.DeleteAsync(id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _sut.Delete(id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        _service.Verify(s => s.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static CustomerResponse BuildResponse() =>
        new(Guid.NewGuid(), "Jane", "Doe", "jane@example.com", null, DateTime.UtcNow, DateTime.UtcNow);
}
