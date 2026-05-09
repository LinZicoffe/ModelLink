using claude_model_setting.Models;

namespace claude_model_setting.Services;

/// <summary>
/// Claude Desktop 汉化服务接口
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// 查找 Claude Desktop 安装信息
    /// </summary>
    ClaudeInstallation? FindClaude();

    /// <summary>
    /// 检查汉化状态
    /// </summary>
    PatchStatus CheckPatchStatus(ClaudeInstallation installation);

    /// <summary>
    /// 执行汉化
    /// </summary>
    Task ApplyPatchAsync(ClaudeInstallation installation, IProgress<string> progress);

    /// <summary>
    /// 恢复原版
    /// </summary>
    Task RemovePatchAsync(ClaudeInstallation installation, IProgress<string> progress);
}
