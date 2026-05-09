using claude_model_setting.Models;

namespace claude_model_setting.Services;

/// <summary>
/// 备份服务接口：备份/还原 Claude Desktop 原始文件
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// 备份目录路径
    /// </summary>
    string BackupDirectory { get; }

    /// <summary>
    /// 创建备份
    /// </summary>
    void CreateBackup(ClaudeInstallation installation);

    /// <summary>
    /// 从备份恢复
    /// </summary>
    void RestoreBackup(ClaudeInstallation installation);
}
