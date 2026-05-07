using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using claude_model_setting.Helpers;
using claude_model_setting.Models;
using Serilog;

namespace claude_model_setting.Services;

/// <summary>
/// 配置文件读写服务
/// </summary>
public sealed class ConfigService : IConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private AppConfig _config = new();

    public AppConfig CurrentConfig => _config;
    public string ConfigFilePath => GetConfigPath();

    /// <summary>
    /// 从磁盘加载配置
    /// </summary>
    public async Task<AppConfig> LoadConfigAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var path = GetConfigPath();
            if (File.Exists(path))
            {
                var json = await File.ReadAllTextAsync(path);
                _config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            }
            else
            {
                _config = new AppConfig();
            }
            return _config;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "加载配置失败，使用默认配置");
            _config = new AppConfig();
            return _config;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 保存配置到磁盘（原子写入）
    /// </summary>
    public async Task SaveConfigAsync(AppConfig config)
    {
        await _lock.WaitAsync();
        try
        {
            var dir = GetConfigDir();
            Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(config, JsonOptions);
            await FileSystemHelper.AtomicWriteAsync(GetConfigPath(), json);
            _config = config;
            Log.Information("配置已保存");
        }
        catch (Exception ex)
        {
            var msg = FileSystemHelper.FriendlyWriteError(ex, GetConfigPath());
            Log.Error(ex, "保存配置失败: {Message}", msg);
            throw new InvalidOperationException(msg, ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 更新内存中的配置
    /// </summary>
    public void UpdateConfig(AppConfig config)
    {
        _lock.Wait();
        try
        {
            _config = config;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string GetConfigDir()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".claude-model-proxy");
    }

    private static string GetConfigPath()
    {
        return Path.Combine(GetConfigDir(), "config.json");
    }
}
