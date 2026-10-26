namespace Api.ValueObjects;

/// <summary>
///     Outcome of a service call: either data, or an error message with the HTTP
///     status the controller should answer with.
/// </summary>
public sealed class ServiceResult<T>
{
    private ServiceResult(T? data, string? errorMessage, int statusCode)
    {
        Data = data;
        ErrorMessage = errorMessage;
        StatusCode = statusCode;
    }

    public T? Data { get; }
    public string? ErrorMessage { get; }
    public int StatusCode { get; }
    public bool IsSuccess => ErrorMessage is null;

    public static ServiceResult<T> Success(T data)
    {
        return new ServiceResult<T>(data, null, StatusCodes.Status200OK);
    }

    public static ServiceResult<T> Error(int statusCode, string message)
    {
        return new ServiceResult<T>(default, message, statusCode);
    }
}
