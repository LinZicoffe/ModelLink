using System.Text.Json.Serialization;

namespace claude_model_setting.Models;

/// <summary>
/// 代理请求日志条目
/// </summary>
public sealed class LogEntry
{
    [JsonPropertyName("time")]
    public string Time { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    /// <summary>
    /// 是否成功（HTTP 200）
    /// </summary>
    [JsonIgnore]
    public bool IsSuccess => Status == 200;

    /// <summary>
    /// 状态显示文本：200 显示"成功"，否则显示状态码
    /// </summary>
    [JsonIgnore]
    public string StatusText => Status == 200 ? "成功" : Status.ToString();
}
