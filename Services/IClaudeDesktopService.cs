namespace claude_model_setting.Services;

/// <summary>
/// Claude Desktop 集成服务接口
/// </summary>
public interface IClaudeDesktopService
{
    /// <summary>
    /// 将配置应用到 Claude Desktop 并重启
    /// </summary>
    Task<string> ApplyToClaudeDesktopAsync();

    /// <summary>
    /// 重启 Claude Desktop 进程
    /// </summary>
    Task RestartClaudeDesktopAsync();
}
