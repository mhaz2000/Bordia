namespace BuildingBlocks.Contracts.Responses;

/// <summary>
/// Standardized API response wrapper.
/// </summary>
/// <typeparam name="T">The type of the response data.</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// Indicates whether the request was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The response data. Null if the request failed.
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Error message if the request failed. Null if successful.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Creates a successful API response.
    /// </summary>
    public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };

    /// <summary>
    /// Creates a failure API response.
    /// </summary>
    public static ApiResponse<T> Fail(string error) => new() { Success = false, Error = error };
}
