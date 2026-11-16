using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CustomerService.Api.Contracts;
using CustomerService.Api.Domain;
using CustomerService.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace CustomerService.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthApiTests(CustomerApiFactory factory) : IAsyncLifetime
{
    private const string Password = CustomerApiFactory.TestPassword;

    private readonly List<HttpClient> _clients = [];

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync()
    {
        _clients.ForEach(c => c.Dispose());
        return Task.CompletedTask;
    }

    private HttpClient Anonymous()
    {
        var client = factory.CreateClient();
        _clients.Add(client);
        return client;
    }

    private async Task<HttpClient> SignedInAsync(string username = "viewer", Role role = Role.Admin, bool pending = false)
    {
        var client = await factory.CreateSignedInClientAsync(username, role, pending);
        _clients.Add(client);
        return client;
    }

    // POST /api/auth/login

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithToken()
    {
        await SignedInAsync("ada", Role.Viewer);

        var response = await Anonymous().PostAsJsonAsync("/api/auth/login", new LoginRequest("ada", Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(CustomerApiFactory.Json);
        Assert.False(string.IsNullOrWhiteSpace(login!.Token));
        Assert.Equal("ada", login.Username);
        Assert.Equal(Role.Viewer, login.Role);
        Assert.False(login.MustChangePassword);
    }

    [Fact]
    public async Task Login_MixedCaseUsername_Returns200()
    {
        await SignedInAsync("ada");

        var response = await Anonymous().PostAsJsonAsync("/api/auth/login", new LoginRequest("  AdA ", Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(CustomerApiFactory.Json);
        Assert.Equal("ada", login!.Username);
    }

    [Fact]
    public async Task Login_WrongPassword_And_UnknownUser_ReturnIdenticalBody()
    {
        await SignedInAsync("ada");
        var client = Anonymous();

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("ada", "nope"));
        var unknownUser = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("ghost", "nope"));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        Assert.Equal("application/problem+json", wrongPassword.Content.Headers.ContentType?.MediaType);
        var wrongBody = await wrongPassword.Content.ReadFromJsonAsync<ProblemDetails>();
        var unknownBody = await unknownUser.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(wrongBody!.Title, unknownBody!.Title);
        Assert.Equal(wrongBody.Status, unknownBody.Status);
        Assert.Equal(wrongBody.Detail, unknownBody.Detail);
        Assert.Equal(
            await ReadWithoutTraceAsync(wrongPassword),
            await ReadWithoutTraceAsync(unknownUser));
    }

    // Trace ids differ per request, so compare the body with that member removed.
    private static async Task<string> ReadWithoutTraceAsync(HttpResponseMessage response)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        node.Remove("traceId");
        return node.ToJsonString();
    }

    // GET /api/auth/me

    [Fact]
    public async Task Me_WithToken_ReturnsRoleAndFlag()
    {
        var client = await SignedInAsync("ada", Role.Viewer, pending: true);

        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me", CustomerApiFactory.Json);

        Assert.Equal(new CurrentUserResponse("ada", Role.Viewer, true), me);
    }

    // POST /api/auth/logout

    [Fact]
    public async Task Logout_ThenMe_Returns401()
    {
        var client = await SignedInAsync();

        var logout = await client.PostAsync("/api/auth/logout", null);
        var me = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    // POST /api/auth/change-password

    [Fact]
    public async Task ChangePassword_Valid_Returns204AndClearsFlag()
    {
        var client = await SignedInAsync("ada", Role.Viewer, pending: true);

        var response = await client.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest(Password, "Another-Password-2!"));
        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me", CustomerApiFactory.Json);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(me!.MustChangePassword);
        var relogin = await Anonymous().PostAsJsonAsync("/api/auth/login", new LoginRequest("ada", "Another-Password-2!"));
        Assert.Equal(HttpStatusCode.OK, relogin.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_Valid_ExistingSessionStillWorks()
    {
        var client = await SignedInAsync();

        await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest(Password, "Another-Password-2!"));
        var me = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_SameAsCurrent_Returns400OnNewPassword()
    {
        var client = await SignedInAsync();

        var response = await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest(Password, Password));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(problem!.Errors.Keys, k => k.Equals("newPassword", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ChangePassword_WrongCurrent_Returns400OnCurrentPassword()
    {
        var client = await SignedInAsync();

        var response = await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest("wrong", "Another-Password-2!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(problem!.Errors.Keys, k => k.Equals("currentPassword", StringComparison.OrdinalIgnoreCase));
    }

    // Protected customer endpoints

    [Theory]
    [InlineData("GET", "/api/customers")]
    [InlineData("GET", "/api/customers/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/customers")]
    [InlineData("PUT", "/api/customers/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", "/api/customers/00000000-0000-0000-0000-000000000001")]
    public async Task Customers_NoToken_Returns401(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(new CreateCustomerRequest("Jane", "Doe", "jane@example.com", null, null));
        }

        var response = await Anonymous().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Bearer")]
    [InlineData("Bearer ")]
    [InlineData("Basic abc")]
    [InlineData("Bearer not-a-real-token")]
    public async Task Customers_MalformedBearer_Returns401(string header)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/customers");
        request.Headers.TryAddWithoutValidation("Authorization", header);

        var response = await Anonymous().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Customers_LowercaseBearerScheme_Returns200()
    {
        var signedIn = await SignedInAsync();
        var token = signedIn.DefaultRequestHeaders.Authorization!.Parameter;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/customers");
        request.Headers.TryAddWithoutValidation("Authorization", $"bearer {token}");

        var response = await Anonymous().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Customers_PendingUser_Returns403()
    {
        var client = await SignedInAsync("ada", Role.Viewer, pending: true);

        var response = await client.GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminOnlyEndpoint_PendingAdmin_Returns403()
    {
        var client = await SignedInAsync("boss", Role.Admin, pending: true);

        var response = await client.GetAsync("/test/admin-only");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminOnlyEndpoint_SettledAdmin_Returns200AndViewer403()
    {
        var admin = await SignedInAsync("boss", Role.Admin);
        var viewer = await SignedInAsync("ada", Role.Viewer);

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/test/admin-only")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/test/admin-only")).StatusCode);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoints_NoToken_Return200(string path)
    {
        var response = await Anonymous().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
