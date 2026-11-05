using System.Net;
using System.Net.Http.Json;
using CustomerService.Api.Contracts;
using CustomerService.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace CustomerService.IntegrationTests;

[Collection(ApiCollection.Name)]
public class CustomersApiTests(CustomerApiFactory factory) : IAsyncLifetime
{
    private const string BaseUrl = "/api/customers";

    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    // POST /api/customers

    [Fact]
    public async Task Create_ValidRequest_Returns201WithLocationAndBody()
    {
        var response = await _client.PostAsJsonAsync(BaseUrl,
            new CreateCustomerRequest(" Jane ", "Doe", "Jane.Doe@Example.com", "+44 20 7946 0000"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var customer = await response.Content.ReadFromJsonAsync<CustomerResponse>();
        Assert.NotNull(customer);
        Assert.NotEqual(Guid.Empty, customer.Id);
        Assert.Equal("Jane", customer.FirstName);
        Assert.Equal("jane.doe@example.com", customer.Email);
        Assert.Equal("+44 20 7946 0000", customer.PhoneNumber);
        Assert.EndsWith($"{BaseUrl}/{customer.Id}", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(InvalidCreateRequests))]
    public async Task Create_InvalidRequest_Returns400ValidationProblem(object request, string invalidField)
    {
        var response = await _client.PostAsJsonAsync(BaseUrl, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(problem!.Errors.Keys, key => key.Equals(invalidField, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Create_DuplicateEmail_Returns409()
    {
        await CreateCustomerAsync("Jane", "Doe", "jane@example.com");

        var response = await _client.PostAsJsonAsync(BaseUrl,
            new CreateCustomerRequest("Janet", "Doe", "JANE@example.com", null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(409, problem!.Status);
    }

    // GET /api/customers/{id}

    [Fact]
    public async Task GetById_ExistingCustomer_Returns200()
    {
        var created = await CreateCustomerAsync("Jane", "Doe", "jane@example.com");

        var customer = await _client.GetFromJsonAsync<CustomerResponse>($"{BaseUrl}/{created.Id}");

        Assert.Equal(created, customer);
    }

    [Fact]
    public async Task GetById_ReturnsTimestampsAsUtc()
    {
        var created = await CreateCustomerAsync("Jane", "Doe", "jane@example.com");

        var json = await _client.GetStringAsync($"{BaseUrl}/{created.Id}");
        using var document = System.Text.Json.JsonDocument.Parse(json);

        Assert.EndsWith("Z", document.RootElement.GetProperty("createdAtUtc").GetString());
        Assert.EndsWith("Z", document.RootElement.GetProperty("updatedAtUtc").GetString());
    }

    [Fact]
    public async Task GetById_MissingCustomer_Returns404Problem()
    {
        var response = await _client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetById_MalformedId_Returns404()
    {
        var response = await _client.GetAsync($"{BaseUrl}/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // GET /api/customers

    [Fact]
    public async Task List_ReturnsPagedResultsInNameOrder()
    {
        await CreateCustomerAsync("Zoe", "Brown", "zoe@example.com");
        await CreateCustomerAsync("Adam", "Brown", "adam@example.com");
        await CreateCustomerAsync("Carl", "Adams", "carl@example.com");

        var firstPage = await _client.GetFromJsonAsync<PagedResponse<CustomerResponse>>($"{BaseUrl}?page=1&pageSize=2");
        var secondPage = await _client.GetFromJsonAsync<PagedResponse<CustomerResponse>>($"{BaseUrl}?page=2&pageSize=2");

        Assert.Equal(3, firstPage!.TotalCount);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(["Carl", "Adam"], firstPage.Items.Select(c => c.FirstName));
        Assert.Equal(["Zoe"], secondPage!.Items.Select(c => c.FirstName));
    }

    [Fact]
    public async Task List_WithSearch_FiltersResults()
    {
        await CreateCustomerAsync("Jane", "Doe", "jane@example.com");
        await CreateCustomerAsync("John", "Smith", "john@example.com");

        var page = await _client.GetFromJsonAsync<PagedResponse<CustomerResponse>>($"{BaseUrl}?search=Smith");

        var customer = Assert.Single(page!.Items);
        Assert.Equal("John", customer.FirstName);
    }

    [Fact]
    public async Task List_Empty_ReturnsEmptyPage()
    {
        var page = await _client.GetFromJsonAsync<PagedResponse<CustomerResponse>>(BaseUrl);

        Assert.Empty(page!.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task List_InvalidPaging_Returns400(string queryString)
    {
        var response = await _client.GetAsync($"{BaseUrl}?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // PUT /api/customers/{id}

    [Fact]
    public async Task Update_ExistingCustomer_Returns200AndPersists()
    {
        var created = await CreateCustomerAsync("Jane", "Doe", "jane@example.com");

        var response = await _client.PutAsJsonAsync($"{BaseUrl}/{created.Id}",
            new UpdateCustomerRequest("Janet", "Smith", "janet@example.com", "+1 555 0100"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CustomerResponse>();
        Assert.Equal("Janet", updated!.FirstName);
        Assert.Equal("janet@example.com", updated.Email);
        Assert.True(updated.UpdatedAtUtc >= created.UpdatedAtUtc);

        var fetched = await _client.GetFromJsonAsync<CustomerResponse>($"{BaseUrl}/{created.Id}");
        Assert.Equal("Smith", fetched!.LastName);
        Assert.Equal("+1 555 0100", fetched.PhoneNumber);
    }

    [Fact]
    public async Task Update_KeepingOwnEmail_Returns200()
    {
        var created = await CreateCustomerAsync("Jane", "Doe", "jane@example.com");

        var response = await _client.PutAsJsonAsync($"{BaseUrl}/{created.Id}",
            new UpdateCustomerRequest("Jane", "Smith", "jane@example.com", null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_InvalidRequest_Returns400()
    {
        var created = await CreateCustomerAsync("Jane", "Doe", "jane@example.com");

        var response = await _client.PutAsJsonAsync($"{BaseUrl}/{created.Id}",
            new UpdateCustomerRequest("", "Doe", "not-an-email", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_MissingCustomer_Returns404()
    {
        var response = await _client.PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}",
            new UpdateCustomerRequest("Jane", "Doe", "jane@example.com", null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_EmailTakenByAnotherCustomer_Returns409()
    {
        await CreateCustomerAsync("Jane", "Doe", "jane@example.com");
        var other = await CreateCustomerAsync("John", "Smith", "john@example.com");

        var response = await _client.PutAsJsonAsync($"{BaseUrl}/{other.Id}",
            new UpdateCustomerRequest("John", "Smith", "jane@example.com", null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // DELETE /api/customers/{id}

    [Fact]
    public async Task Delete_ExistingCustomer_Returns204ThenGetReturns404()
    {
        var created = await CreateCustomerAsync("Jane", "Doe", "jane@example.com");

        var deleteResponse = await _client.DeleteAsync($"{BaseUrl}/{created.Id}");
        var getResponse = await _client.GetAsync($"{BaseUrl}/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_MissingCustomer_Returns404()
    {
        var response = await _client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // GET /health, /health/live, /health/ready

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task ProbeEndpoints_DatabaseReachable_Return200(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_DatabaseReachable_Returns200()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    public static TheoryData<object, string> InvalidCreateRequests() => new()
    {
        { new { firstName = "", lastName = "Doe", email = "jane@example.com" }, "FirstName" },
        { new { firstName = "Jane", lastName = "Doe" }, "Email" },
        { new { firstName = "Jane", lastName = "Doe", email = "not-an-email" }, "Email" },
        { new { firstName = new string('a', 101), lastName = "Doe", email = "jane@example.com" }, "FirstName" },
        { new { firstName = "Jane", lastName = "Doe", email = "jane@example.com", phoneNumber = "call me" }, "PhoneNumber" }
    };

    private async Task<CustomerResponse> CreateCustomerAsync(string firstName, string lastName, string email)
    {
        var response = await _client.PostAsJsonAsync(BaseUrl, new CreateCustomerRequest(firstName, lastName, email, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CustomerResponse>())!;
    }
}
