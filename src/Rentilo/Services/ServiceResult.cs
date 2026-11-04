namespace Rentilo.Services;

/// <summary>Outcome of a business operation: either success (with an optional value) or a user-facing error.</summary>
public record ServiceResult(bool Succeeded, string? Error)
{
    public static ServiceResult Ok() => new(true, null);
    public static ServiceResult Fail(string error) => new(false, error);
}

public record ServiceResult<T>(bool Succeeded, string? Error, T? Value) : ServiceResult(Succeeded, Error)
{
    public static ServiceResult<T> Ok(T value) => new(true, null, value);
    public new static ServiceResult<T> Fail(string error) => new(false, error, default);
}
