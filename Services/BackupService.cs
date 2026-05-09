using System.Diagnostics;
using System.IO;
using System.Text;
using claude_model_setting.Models;
using Serilog;

namespace claude_model_setting.Services;

/// <summary>
/// 备份服务实现：备份/还原 ion-dist/assets/v1/index-*.js 文件
/// </summary>
public sealed class BackupService : IBackupService
{
    private static readonly string BaseBackupDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClaudeCN", "backups");

    private static readonly string VersionFilePath = Path.Combine(
        BaseBackupDir, "backup-version.txt");

    public string BackupDirectory => BaseBackupDir;

    /// <summary>
    /// 创建备份：将 ion-dist/assets/v1/index-*.js 备份到 ClaudeCN/backups/
    /// 版本号相同且备份存在时跳过
    /// </summary>
    public void CreateBackup(ClaudeInstallation installation)
    {
        Directory.CreateDirectory(BaseBackupDir);

        // 检查版本号是否与已有备份一致
        var currentVersion = installation.Version;
        if (File.Exists(VersionFilePath))
        {
            var existingVersion = File.ReadAllText(VersionFilePath).Trim();
            if (existingVersion == currentVersion && Directory.GetFiles(BaseBackupDir, "index-*.js").Length > 0)
            {
                Log.Information("备份版本 {Version} 已存在，跳过备份", currentVersion);
                return;
            }
        }

        // 清除旧备份
        foreach (var f in Directory.GetFiles(BaseBackupDir, "index-*.js"))
        {
            try { File.Delete(f); } catch { /* 忽略 */ }
        }

        // 备份 index-*.js 文件
        var assetsDir = Path.Combine(installation.IonDistDir, "assets", "v1");
        if (!Directory.Exists(assetsDir))
        {
            Log.Warning("资源目录不存在，跳过备份: {Dir}", assetsDir);
            return;
        }

        foreach (var jsFile in Directory.GetFiles(assetsDir, "index-*.js"))
        {
            var fileName = Path.GetFileName(jsFile);
            var destPath = Path.Combine(BaseBackupDir, fileName);

            // 获取文件权限（MSIX 目录可能需要）
            TakeOwnership(jsFile);

            // 清除只读属性
            var attrs = File.GetAttributes(jsFile);
            if (attrs.HasFlag(FileAttributes.ReadOnly))
            {
                File.SetAttributes(jsFile, attrs & ~FileAttributes.ReadOnly);
            }

            File.Copy(jsFile, destPath, overwrite: true);
            Log.Information("已备份: {File}", fileName);
        }

        // 写入版本号
        File.WriteAllText(VersionFilePath, currentVersion, Encoding.UTF8);
        Log.Information("备份完成，版本: {Version}", currentVersion);
    }

    /// <summary>
    /// 从备份恢复：将备份文件覆盖回 ion-dist/assets/v1/
    /// </summary>
    public void RestoreBackup(ClaudeInstallation installation)
    {
        if (!Directory.Exists(BaseBackupDir))
        {
            Log.Warning("备份目录不存在，无法恢复");
            return;
        }

        var assetsDir = Path.Combine(installation.IonDistDir, "assets", "v1");
        if (!Directory.Exists(assetsDir))
        {
            Log.Warning("目标资源目录不存在: {Dir}", assetsDir);
            return;
        }

        // 获取目标目录权限
        TakeOwnership(assetsDir);

        foreach (var backupFile in Directory.GetFiles(BaseBackupDir, "index-*.js"))
        {
            var fileName = Path.GetFileName(backupFile);
            var targetPath = Path.Combine(assetsDir, fileName);

            // 获取目标文件权限
            if (File.Exists(targetPath))
            {
                TakeOwnership(targetPath);
                // 清除只读属性
                var attrs = File.GetAttributes(targetPath);
                if (attrs.HasFlag(FileAttributes.ReadOnly))
                {
                    File.SetAttributes(targetPath, attrs & ~FileAttributes.ReadOnly);
                }
            }

            File.Copy(backupFile, targetPath, overwrite: true);
            Log.Information("已恢复: {File}", fileName);
        }

        Log.Information("备份恢复完成");
    }

    /// <summary>
    /// 获取文件/目录所有权（管理员权限下）
    /// </summary>
    private static void TakeOwnership(string path)
    {
        RunCommand("takeown", $"/F \"{path}\" /R /A /D Y");
        RunCommand("icacls", $"\"{path}\" /grant Administrators:F /T /Q");
    }

    /// <summary>
    /// 执行外部命令（无窗口）
    /// </summary>
    private static void RunCommand(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            process?.WaitForExit(30000);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "执行命令失败: {Cmd} {Args}", fileName, arguments);
        }
    }
}
