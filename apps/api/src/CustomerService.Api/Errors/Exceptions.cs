namespace CustomerService.Api.Errors;

public class NotFoundException(string message) : Exception(message);

public class ConflictException(string message) : Exception(message);

public class UnauthorizedException(string message) : Exception(message);

public class FieldValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
