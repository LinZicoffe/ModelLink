using claude_model_setting.Models;

namespace claude_model_setting.Services;

/// <summary>
/// 配置文件读写服务接口
/// </summary>
public interface IConfigService
{
    /// <summary>
    /// 当前内存中的配置副本
    /// </summary>
    AppConfig CurrentConfig { get; }

    /// <summary>
    /// 从磁盘加载配置
    /// </summary>
    Task<AppConfig> LoadConfigAsync();

    /// <summary>
    /// 保存配置到磁盘（原子写入）
    /// </summary>
    Task SaveConfigAsync(AppConfig config);

    /// <summary>
    /// 获取配置文件路径
    /// </summary>
    string ConfigFilePath { get; }
}
