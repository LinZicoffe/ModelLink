using System.IO;
using System.Text.Json;
using claude_model_setting.Helpers;
using claude_model_setting.Models;
using Serilog;

namespace claude_model_setting.Services;

/// <summary>
/// Claude Desktop 集成服务：配置写入 + 进程重启
/// </summary>
public sealed class ClaudeDesktopService : IClaudeDesktopService
{
    private const string ProviderUuid = "a0a0a0a0-b1b1-4c2c-9d3d-e4e4e4e4e4e4";
    private const string ProxyUrl = "http://127.0.0.1:5678";

    private readonly IConfigService _configService;
    private readonly IModelResolverService _resolverService;

    public ClaudeDesktopService(IConfigService configService, IModelResolverService resolverService)
    {
        _configService = configService;
        _resolverService = resolverService;
    }

    /// <summary>
    /// 将配置应用到 Claude Desktop 并重启
    /// </summary>
    public async Task<string> ApplyToClaudeDesktopAsync()
    {
        var config = _configService.CurrentConfig;

        // 验证配置
        if (config.Providers.Count == 0)
            throw new InvalidOperationException("请先添加至少一个服务商");

        foreach (var p in config.Providers)
        {
            if (string.IsNullOrEmpty(p.TargetUrl))
                throw new InvalidOperationException("所有服务商必须填写 API 地址");
            if (!p.TargetUrl.StartsWith("http://") && !p.TargetUrl.StartsWith("https://"))
                throw new InvalidOperationException($"API 地址必须以 http:// 或 https:// 开头: {p.TargetUrl}");
            if (string.IsNullOrEmpty(p.ApiKey))
                throw new InvalidOperationException("所有服务商必须填写 API 密钥");
            foreach (var m in p.Models)
            {
                if (string.IsNullOrEmpty(m.Name))
                    throw new InvalidOperationException("所有模型必须填写名称");
            }
        }

        var slots = _resolverService.FlattenConfig(config);
        if (slots.Count == 0)
            throw new InvalidOperationException("请至少配置一个模型");

        // 写入 Claude-3p 配置
        await WriteClaude3pConfigAsync(slots);

        // 写入 Claude Desktop 通用配置
        await WriteClaudeCommonConfigAsync();

        // 重启 Claude Desktop
        await RestartClaudeDesktopAsync();

        return $"已成功应用 {slots.Count} 个模型配置";
    }

    /// <summary>
    /// 重启 Claude Desktop 进程
    /// </summary>
    public async Task RestartClaudeDesktopAsync()
    {
        var script = """
            $ErrorActionPreference = 'SilentlyContinue'
            $proc = Get-Process -Name 'Claude' -ErrorAction SilentlyContinue
            if ($proc) {
                $path = $proc.Path
                Stop-Process -Name 'Claude' -Force
                Start-Sleep -Seconds 3
                if ($path) {
                    Start-Process $path
                } else {
                    # 尝试 UWP 路径
                    $pkg = Get-AppxPackage -Name '*Claude*' | Select-Object -First 1
                    if ($pkg) {
                        $fam = $pkg.PackageFamilyName
                        Start-Process "explorer.exe" "shell:AppsFolder\$fam!Claude"
                    }
                }
            }
            """;

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-WindowStyle Hidden -Command \"{script.Replace("\"", "\\\"")}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            System.Diagnostics.Process.Start(psi);
            Log.Information("已发送 Claude Desktop 重启命令");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "重启 Claude Desktop 失败");
            throw new InvalidOperationException($"重启 Claude Desktop 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 写入 Claude-3p 配置目录
    /// </summary>
    private async Task WriteClaude3pConfigAsync(List<(string Slot, string Name, string To1m, string Url, string Key)> slots)
    {
        var configLibrary = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Claude-3p", "configLibrary");
        Directory.CreateDirectory(configLibrary);

        // 读取或创建 _meta.json
        var metaPath = Path.Combine(configLibrary, "_meta.json");
        var appliedId = ProviderUuid;

        if (File.Exists(metaPath))
        {
            try
            {
                var existingMetaJson = await File.ReadAllTextAsync(metaPath);
                var doc = JsonDocument.Parse(existingMetaJson);
                if (doc.RootElement.TryGetProperty("appliedId", out var idEl))
                    appliedId = idEl.GetString() ?? ProviderUuid;
            }
            catch { /* 使用默认值 */ }
        }

        // 写入 provider 配置
        var targetId = appliedId;
        var inferenceModels = slots.Select(s => new
        {
            name = s.Slot,
            supports1m = !string.IsNullOrEmpty(s.To1m),
        }).ToList();

        var providerConfig = new
        {
            coworkEgressAllowedHosts = new[] { "*" },
            inferenceProvider = "gateway",
            inferenceGatewayBaseUrl = ProxyUrl,
            inferenceGatewayApiKey = "proxy",
            inferenceGatewayAuthScheme = "bearer",
            inferenceModels,
        };

        var providerJson = JsonSerializer.Serialize(providerConfig, new JsonSerializerOptions { WriteIndented = true });
        await FileSystemHelper.AtomicWriteAsync(Path.Combine(configLibrary, $"{targetId}.json"), providerJson);

        // 更新 _meta.json
        var meta = new
        {
            appliedId,
            entries = new[]
            {
                new { id = targetId, name = "ModelLink" },
            },
        };
        var metaJson = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
        await FileSystemHelper.AtomicWriteAsync(metaPath, metaJson);

        // 删除旧的 model-proxy.json
        var proxyPath = Path.Combine(configLibrary, "model-proxy.json");
        if (File.Exists(proxyPath))
        {
            try { File.Delete(proxyPath); } catch { /* 忽略 */ }
        }

        Log.Information("Claude-3p 配置已写入");
    }

    /// <summary>
    /// 写入 Claude Desktop 通用配置
    /// </summary>
    private async Task WriteClaudeCommonConfigAsync()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // Claude-3p 目录
        var claude3pDir = Path.Combine(appData, "Claude-3p");
        Directory.CreateDirectory(claude3pDir);

        var deploymentConfig = new { deploymentMode = "3p" };
        var deploymentJson = JsonSerializer.Serialize(deploymentConfig, new JsonSerializerOptions { WriteIndented = true });

        await FileSystemHelper.AtomicWriteAsync(
            Path.Combine(claude3pDir, "claude_desktop_config.json"), deploymentJson);

        // developer_settings.json
        var devSettings = new { };
        await FileSystemHelper.AtomicWriteAsync(
            Path.Combine(claude3pDir, "developer_settings.json"),
            JsonSerializer.Serialize(devSettings, new JsonSerializerOptions { WriteIndented = true }));

        // config.json
        await FileSystemHelper.AtomicWriteAsync(
            Path.Combine(claude3pDir, "config.json"),
            JsonSerializer.Serialize(devSettings, new JsonSerializerOptions { WriteIndented = true }));

        // Claude 目录（标准 Claude Desktop）
        var claudeDir = Path.Combine(appData, "Claude");
        Directory.CreateDirectory(claudeDir);

        await FileSystemHelper.AtomicWriteAsync(
            Path.Combine(claudeDir, "claude_desktop_config.json"), deploymentJson);

        Log.Information("Claude Desktop 通用配置已写入");
    }
}
