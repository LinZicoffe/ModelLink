using System.IO;

namespace claude_model_setting.Helpers;

/// <summary>
/// 文件系统辅助：原子写入、重试逻辑
/// </summary>
public static class FileSystemHelper
{
    /// <summary>
    /// 带重试的文件写入（处理 Windows 文件锁定）
    /// </summary>
    public static async Task WriteWithRetryAsync(string path, string content, int maxRetries = 3)
    {
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                await File.WriteAllTextAsync(path, content);
                return;
            }
            catch (IOException ex) when (IsFileLocked(ex) && attempt < maxRetries - 1)
            {
                Serilog.Log.Warning("[写入] 文件被锁定，1 秒后重试... {Path}", path);
                await Task.Delay(1000);
            }
        }
    }

    /// <summary>
    /// 原子写入：先写临时文件，再重命名
    /// </summary>
    public static async Task AtomicWriteAsync(string targetPath, string content)
    {
        var tmpPath = targetPath + ".tmp";
        await WriteWithRetryAsync(tmpPath, content);
        File.Move(tmpPath, targetPath, overwrite: true);
    }

    /// <summary>
    /// 生成友好的写入错误消息
    /// </summary>
    public static string FriendlyWriteError(Exception ex, string path)
    {
        return ex switch
        {
            UnauthorizedAccessException =>
                $"权限不足: {path}。请检查文件夹权限或尝试以管理员身份运行。",
            DirectoryNotFoundException =>
                $"路径不存在: {path}。请确保父目录存在。",
            IOException ioEx when IsFileLocked(ioEx) =>
                $"文件被锁定: {path}。请先关闭 Claude Desktop 再试。",
            _ => $"写入失败 ({path}): {ex.Message}"
        };
    }

    /// <summary>
    /// 判断异常是否为文件锁定
    /// </summary>
    private static bool IsFileLocked(IOException ex)
    {
        return ex.HResult == unchecked((int)0x80070020)   // ERROR_SHARING_VIOLATION (32)
            || ex.HResult == unchecked((int)0x80070021);  // ERROR_LOCK_VIOLATION (33)
    }
}
