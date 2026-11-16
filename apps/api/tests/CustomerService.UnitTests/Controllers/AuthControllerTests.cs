using System.Security.Claims;
using CustomerService.Api.Contracts;
using CustomerService.Api.Controllers;
using CustomerService.Api.Domain;
using CustomerService.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CustomerService.UnitTests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _service = new(MockBehavior.Strict);
    private readonly AuthController _sut;

    public AuthControllerTests()
    {
        _sut = new AuthController(_service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "5")], "Session"))
                }
            }
        };
        _sut.HttpContext.Request.Headers.Authorization = "Bearer the-token";
    }

    [Fact]
    public async Task Login_ValidRequest_ReturnsOkWithResponse()
    {
        var request = new LoginRequest("a", "b");
        var response = new LoginResponse("t", "a", Role.Viewer, false);
        _service.Setup(s => s.LoginAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await _sut.Login(request, CancellationToken.None);

        Assert.Same(response, Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task Logout_SignedIn_PassesHeaderTokenAndReturnsNoContent()
    {
        _service.Setup(s => s.LogoutAsync("the-token", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _sut.Logout(CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        _service.VerifyAll();
    }

    [Fact]
    public async Task Logout_LowercaseBearerScheme_PassesHeaderToken()
    {
        _sut.HttpContext.Request.Headers.Authorization = "bearer the-token";
        _service.Setup(s => s.LogoutAsync("the-token", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _sut.Logout(CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        _service.VerifyAll();
    }

    [Fact]
    public async Task Me_SignedIn_UsesNameIdentifierClaim()
    {
        var response = new CurrentUserResponse("a", Role.Admin, false);
        _service.Setup(s => s.GetCurrentAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await _sut.Me(CancellationToken.None);

        Assert.Same(response, Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task ChangePassword_ValidRequest_ReturnsNoContent()
    {
        var request = new ChangePasswordRequest("old", "new");
        _service.Setup(s => s.ChangePasswordAsync(5, request, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _sut.ChangePassword(request, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        _service.VerifyAll();
    }
}
