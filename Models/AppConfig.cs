using System.Text.Json.Serialization;

namespace claude_model_setting.Models;

/// <summary>
/// 应用配置根模型
/// </summary>
public sealed class AppConfig
{
    [JsonPropertyName("providers")]
    public List<Provider> Providers { get; set; } = [];
}
