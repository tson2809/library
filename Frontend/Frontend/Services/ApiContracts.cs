using System.Text.Json;

namespace Client_web.Services;

public sealed class ApiResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public JsonElement Data { get; set; }
}
