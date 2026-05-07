using System.Text.Json.Serialization;

namespace claude_model_setting.Models;

/// <summary>
/// 第三方 API 服务商配置
/// </summary>
public sealed class Provider
{
    [JsonPropertyName("target_url")]
    public string TargetUrl { get; set; } = string.Empty;

    [JsonPropertyName("api_key")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("models")]
    public List<ModelEntry> Models { get; set; } = [];
}
