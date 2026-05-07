namespace claude_model_setting.Models;

/// <summary>
/// 模型解析结果
/// </summary>
public sealed class ResolvedModel
{
    public string Model { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
