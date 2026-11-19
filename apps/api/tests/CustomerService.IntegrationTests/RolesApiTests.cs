using System.Net;
using System.Net.Http.Json;
using CustomerService.Api.Contracts;
using CustomerService.Api.Data;
using CustomerService.Api.Domain;
using CustomerService.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerService.IntegrationTests;

[Collection(ApiCollection.Name)]
public class RolesApiTests(CustomerApiFactory factory) : IAsyncLifetime
{
    private const string BaseUrl = "/api/customers";

    private readonly List<HttpClient> _clients = [];

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync()
    {
        _clients.ForEach(c => c.Dispose());
        return Task.CompletedTask;
    }

    private async Task<HttpClient> SignedInAsync(string username, Role role, bool pending = false)
    {
        var client = await factory.CreateSignedInClientAsync(username, role, pending);
        _clients.Add(client);
        return client;
    }

    private async Task<CustomerResponse> AddCustomerAsync(HttpClient admin, string email = "jane@example.com")
    {
        var response = await admin.PostAsJsonAsync(BaseUrl, new CreateCustomerRequest("Jane", "Doe", email, null, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerResponse>())!;
    }

    private static CreateCustomerRequest NewCustomer(string email = "new@example.com") =>
        new("New", "Person", email, null, null);

    private static UpdateCustomerRequest Update(string email = "jane@example.com") =>
        new("Changed", "Name", email, null, null);

    [Fact]
    public async Task Viewer_GetListAndGetById_Return200()
    {
        var admin = await SignedInAsync("boss", Role.Admin);
        var customer = await AddCustomerAsync(admin);
        var viewer = await SignedInAsync("vera", Role.Viewer);

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(BaseUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"{BaseUrl}/{customer.Id}")).StatusCode);
    }

    [Fact]
    public async Task Viewer_PostPutDelete_Return403_AndCustomerUnchanged()
    {
        var admin = await SignedInAsync("boss", Role.Admin);
        var customer = await AddCustomerAsync(admin);
        var viewer = await SignedInAsync("vera", Role.Viewer);

        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(BaseUrl, NewCustomer())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PutAsJsonAsync($"{BaseUrl}/{customer.Id}", Update())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.DeleteAsync($"{BaseUrl}/{customer.Id}")).StatusCode);

        var after = await admin.GetFromJsonAsync<CustomerResponse>($"{BaseUrl}/{customer.Id}");
        Assert.Equal("Jane", after!.FirstName);
        var page = await admin.GetFromJsonAsync<PagedResponse<CustomerResponse>>(BaseUrl);
        Assert.Equal(1, page!.TotalCount);
    }

    [Fact]
    public async Task Admin_PostPutDelete_ReturnSuccessCodes()
    {
        var admin = await SignedInAsync("boss", Role.Admin);

        var created = await admin.PostAsJsonAsync(BaseUrl, NewCustomer());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<CustomerResponse>())!.Id;

        var updated = await admin.PutAsJsonAsync($"{BaseUrl}/{id}", Update("new@example.com"));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var deleted = await admin.DeleteAsync($"{BaseUrl}/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task PendingAdmin_PostPutDelete_Return403()
    {
        var admin = await SignedInAsync("boss", Role.Admin);
        var customer = await AddCustomerAsync(admin);
        var pending = await SignedInAsync("newadmin", Role.Admin, pending: true);

        Assert.Equal(HttpStatusCode.Forbidden, (await pending.PostAsJsonAsync(BaseUrl, NewCustomer())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pending.PutAsJsonAsync($"{BaseUrl}/{customer.Id}", Update())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pending.DeleteAsync($"{BaseUrl}/{customer.Id}")).StatusCode);
    }

    [Fact]
    public async Task NoToken_PostPutDelete_Return401()
    {
        var client = factory.CreateClient();
        _clients.Add(client);
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(BaseUrl, NewCustomer())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync($"{BaseUrl}/{id}", Update())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync($"{BaseUrl}/{id}")).StatusCode);
    }

    [Fact]
    public async Task Seeder_EmptyDatabase_CreatesAdminWhoCanSignInAndMustChange()
    {
        await SeedAsync();

        var client = factory.CreateClient();
        _clients.Add(client);
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "ChangeMe-Now-1"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(CustomerApiFactory.Json);
        Assert.Equal(Role.Admin, login!.Role);
        Assert.True(login.MustChangePassword);
    }

    [Fact]
    public async Task Seeder_RunTwice_KeepsOneRowAndExistingPassword()
    {
        await SeedAsync();
        string changedHash;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
            var user = await db.Users.SingleAsync();
            changedHash = "changed-hash-value";
            user.PasswordHash = changedHash;
            await db.SaveChangesAsync();
        }

        await SeedAsync();

        await using var verify = factory.Services.CreateAsyncScope();
        var users = await verify.ServiceProvider.GetRequiredService<CustomerDbContext>().Users.AsNoTracking().ToListAsync();
        var only = Assert.Single(users);
        Assert.Equal(changedHash, only.PasswordHash);
    }

    private async Task SeedAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DefaultAdminSeeder>().SeedAsync(CancellationToken.None);
    }
}
