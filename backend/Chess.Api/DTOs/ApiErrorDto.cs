namespace Api.DTOs;

public record ApiErrorDto(
    string Message,
    string? Error = null);
