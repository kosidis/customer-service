using CustomerService.Api.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CustomerService.UnitTests.Errors;

public class GlobalExceptionHandlerTests
{
    private readonly Mock<IProblemDetailsService> _problemDetailsService = new();
    private ProblemDetailsContext? _written;
    private readonly GlobalExceptionHandler _sut;

    public GlobalExceptionHandlerTests()
    {
        _problemDetailsService
            .Setup(s => s.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(context => _written = context)
            .ReturnsAsync(true);
        _sut = new GlobalExceptionHandler(_problemDetailsService.Object, NullLogger<GlobalExceptionHandler>.Instance);
    }

    [Theory]
    [MemberData(nameof(KnownExceptions))]
    public async Task TryHandleAsync_KnownException_MapsStatusAndExposesMessage(Exception exception, int expectedStatus)
    {
        var httpContext = new DefaultHttpContext();

        var handled = await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(expectedStatus, httpContext.Response.StatusCode);
        Assert.Equal(expectedStatus, _written!.ProblemDetails.Status);
        Assert.Equal(exception.Message, _written.ProblemDetails.Detail);
    }

    [Fact]
    public async Task TryHandleAsync_UnexpectedException_Returns500WithoutLeakingMessage()
    {
        var httpContext = new DefaultHttpContext();

        var handled = await _sut.TryHandleAsync(
            httpContext, new InvalidOperationException("connection string secret"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.Equal(StatusCodes.Status500InternalServerError, _written!.ProblemDetails.Status);
        Assert.Null(_written.ProblemDetails.Detail);
    }

    [Fact]
    public async Task TryHandleAsync_UnauthorizedException_Writes401WithoutDetailLeak()
    {
        var httpContext = new DefaultHttpContext();

        var handled = await _sut.TryHandleAsync(
            httpContext, new UnauthorizedException("user 'bob' not found"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, _written!.ProblemDetails.Status);
        Assert.Equal("Invalid username or password", _written.ProblemDetails.Title);
        Assert.Null(_written.ProblemDetails.Detail);
    }

    [Fact]
    public async Task TryHandleAsync_FieldValidationException_Writes400WithKeyedErrors()
    {
        var httpContext = new DefaultHttpContext();

        var handled = await _sut.TryHandleAsync(
            httpContext, new FieldValidationException("newPassword", "Must differ."), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        var problem = Assert.IsType<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(_written!.ProblemDetails);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal(["Must differ."], problem.Errors["newPassword"]);
    }

    public static TheoryData<Exception, int> KnownExceptions() => new()
    {
        { new NotFoundException("Customer 'x' was not found."), StatusCodes.Status404NotFound },
        { new ConflictException("Email already exists."), StatusCodes.Status409Conflict }
    };
}
