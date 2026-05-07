namespace claude_model_setting.Models;

/// <summary>
/// Claude Desktop 模型槽位定义（8 个固定槽位）
/// </summary>
public static class ModelSlot
{
    public const int MaxSlots = 8;

    public static readonly string[] Slots =
    [
        "claude-3-opus-latest",
        "claude-3-5-sonnet-latest",
        "claude-3-sonnet-20240229",
        "claude-3-haiku-20240307",
        "claude-3-5-haiku-latest",
        "claude-3-opus-20240229",
        "claude-3-5-sonnet-20241022",
        "claude-3-5-sonnet-20240620",
    ];
}
