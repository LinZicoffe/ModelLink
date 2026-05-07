namespace claude_model_setting.Models;

/// <summary>
/// 通用 API 响应
/// </summary>
public sealed class ApiResponse
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;

    public static ApiResponse Success(string message = "") => new() { Ok = true, Message = message };
    public static ApiResponse Error(string message) => new() { Ok = false, Message = message };
}
