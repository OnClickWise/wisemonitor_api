public class ServiceResult<T>
{
    public bool Success { get; private set; }    // Só pode ser setado internamente
    public string? ErrorMessage { get; private set; }
    public T? Data { get; private set; }
    public int StatusCode { get; private set; }

    private ServiceResult(bool success, T? data = default, string? errorMessage = null)
    {
        Success = success;
        Data = data;
        ErrorMessage = errorMessage;
        StatusCode = StatusCode;
    }

    public static ServiceResult<T> SuccessResult(T data) => new ServiceResult<T>(true, data);

    public static ServiceResult<T> Fail(string error) => new ServiceResult<T>(false, default, error);
}
