namespace claude_model_setting.Models;

/// <summary>
/// Claude Desktop 安装信息
/// </summary>
public sealed class ClaudeInstallation
{
    public string AppDir { get; init; } = "";
    public string ResourcesDir { get; init; } = "";
    public string IonDistDir { get; init; } = "";
    public string Version { get; init; } = "";
    public string PackageName { get; init; } = "";
    public bool IsMsix { get; init; }
}
