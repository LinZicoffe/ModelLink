using claude_model_setting.Models;

namespace claude_model_setting.Services;

/// <summary>
/// 代理服务器服务接口
/// </summary>
public interface IProxyServerService : IAsyncDisposable
{
    /// <summary>
    /// 启动代理服务器
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// 停止代理服务器
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// 获取请求日志
    /// </summary>
    List<LogEntry> GetLogs();
}
