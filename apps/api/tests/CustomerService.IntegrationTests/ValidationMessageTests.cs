using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CustomerService.Api.Domain;
using CustomerService.IntegrationTests.Infrastructure;

namespace CustomerService.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ValidationMessageTests(CustomerApiFactory factory) : IAsyncLifetime
{
    private readonly List<HttpClient> _clients = [];

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync()
    {
        _clients.ForEach(c => c.Dispose());
        return Task.CompletedTask;
    }

    private static async Task<Dictionary<string, string[]>> ErrorsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!;
    }

    private static void AssertMessage(Dictionary<string, string[]> errors, string key, string expected) =>
        Assert.Equal(expected, Assert.Single(errors[key]));

    [Fact]
    public async Task Login_EmptyBody_ReturnsReadableMessages()
    {
        var client = factory.CreateClient();
        _clients.Add(client);

        var errors = await ErrorsAsync(await client.PostAsJsonAsync("/api/auth/login", new { }));

        AssertMessage(errors, "Username", "Username is required.");
        AssertMessage(errors, "Password", "Password is required.");
    }

    [Fact]
    public async Task ChangePassword_EmptyBody_ReturnsReadableMessages()
    {
        var client = await factory.CreateSignedInClientAsync("ada", Role.Viewer);
        _clients.Add(client);

        var errors = await ErrorsAsync(await client.PostAsJsonAsync("/api/auth/change-password", new { }));

        AssertMessage(errors, "CurrentPassword", "Current password is required.");
        AssertMessage(errors, "NewPassword", "New password is required.");
    }

    [Fact]
    public async Task CreateCustomer_EmptyBody_ReturnsReadableMessages()
    {
        var client = await factory.CreateSignedInClientAsync("boss", Role.Admin);
        _clients.Add(client);

        var errors = await ErrorsAsync(await client.PostAsJsonAsync("/api/customers", new { }));

        AssertMessage(errors, "FirstName", "First name is required.");
        AssertMessage(errors, "LastName", "Last name is required.");
        AssertMessage(errors, "Email", "Email is required.");
    }
}
