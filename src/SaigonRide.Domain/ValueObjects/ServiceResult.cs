namespace SaigonRide.Domain.ValueObjects;

/// <summary>
/// Standard envelope returned by service methods so controllers don't need to
/// catch exceptions for predictable business failures (NFR-06). Architecture
/// §7.1.1 row 3.
/// </summary>
public sealed class ServiceResult
{
    public bool Success { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static ServiceResult Ok() => new() { Success = true };

    public static ServiceResult Fail(string code, string message) =>
        new() { Success = false, ErrorCode = code, ErrorMessage = message };
}

public sealed class ServiceResult<T>
{
    public bool Success { get; private set; }
    public T? Value { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static ServiceResult<T> Ok(T value) =>
        new() { Success = true, Value = value };

    public static ServiceResult<T> Fail(string code, string message) =>
        new() { Success = false, ErrorCode = code, ErrorMessage = message };
}
