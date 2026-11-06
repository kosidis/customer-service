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

    public static TheoryData<Exception, int> KnownExceptions() => new()
    {
        { new NotFoundException("Customer 'x' was not found."), StatusCodes.Status404NotFound },
        { new ConflictException("Email already exists."), StatusCodes.Status409Conflict }
    };
}
