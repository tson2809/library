namespace Server.Contracts.Common;

public sealed class ApiResult<T>
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public T? Data { get; set; }

    public static ApiResult<T> Ok(T? data, string message = "Thành công.")
    {
        return new ApiResult<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    public static ApiResult<T> Fail(string message)
    {
        return new ApiResult<T>
        {
            Success = false,
            Message = message
        };
    }
}

public sealed class ApiResult
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public static ApiResult Ok(string message = "Thành công.")
    {
        return new ApiResult
        {
            Success = true,
            Message = message
        };
    }

    public static ApiResult Fail(string message)
    {
        return new ApiResult
        {
            Success = false,
            Message = message
        };
    }
}
