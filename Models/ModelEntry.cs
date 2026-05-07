using System.Text.Json.Serialization;

namespace claude_model_setting.Models;

/// <summary>
/// 模型条目：第三方模型名 + 是否支持 1M 上下文
/// </summary>
public sealed class ModelEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 1M 上下文变体模型名（非空表示支持）
    /// </summary>
    [JsonPropertyName("to_1m")]
    public string To1m { get; set; } = string.Empty;
}
