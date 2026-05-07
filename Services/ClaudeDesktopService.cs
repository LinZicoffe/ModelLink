using System.Diagnostics;
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
    /// 重启 Claude Desktop 进程（仅针对 Anthropic 官方桌面端路径，避免误杀 Claude Code 等同名进程）
    /// </summary>
    public async Task RestartClaudeDesktopAsync()
    {
        try
        {
            var desktopProcesses = new List<(Process Proc, string ExePath)>();

            foreach (var proc in Process.GetProcessesByName("Claude"))
            {
                string exePath;
                try
                {
                    exePath = proc.MainModule?.FileName ?? string.Empty;
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "无法读取进程 {Pid} 的主模块路径（可能被保护或非托管），跳过", proc.Id);
                    proc.Dispose();
                    continue;
                }

                if (!IsAnthropicClaudeDesktopExePath(exePath))
                {
                    proc.Dispose();
                    continue;
                }

                desktopProcesses.Add((proc, exePath));
            }

            var exeToLaunch = PickPreferredExePath(desktopProcesses.Select(x => x.ExePath))
                              ?? FindInstalledClaudeDesktopExe();

            foreach (var (proc, exePath) in desktopProcesses)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill();
                        Log.Information("已结束 Claude Desktop 进程 {Pid}: {Path}", proc.Id, exePath);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "结束 Claude Desktop 进程 {Pid} 失败", proc.Id);
                }
                finally
                {
                    proc.Dispose();
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(exeToLaunch) && File.Exists(exeToLaunch))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exeToLaunch,
                    UseShellExecute = true,
                });
                Log.Information("已启动 Claude Desktop: {Path}", exeToLaunch);
            }
            else if (desktopProcesses.Count > 0)
            {
                Log.Warning("已结束运行中的 Claude Desktop，但未解析到可重新启动的 exe，请手动打开 Claude Desktop");
            }
            else
            {
                Log.Warning("未检测到运行中的 Claude Desktop；也未找到常见安装路径下的 Claude.exe，请手动启动以使配置生效");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "重启 Claude Desktop 失败");
            throw new InvalidOperationException($"重启 Claude Desktop 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 判断 exe 完整路径是否属于 Anthropic Claude Desktop（而非 Claude Code / IDE 插件等）
    /// </summary>
    private static bool IsAnthropicClaudeDesktopExePath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return false;

        var normalized = fullPath.Replace('/', '\\');

        foreach (var fragment in ClaudeDesktopPathBlacklist)
        {
            if (normalized.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        foreach (var fragment in ClaudeDesktopPathWhitelist)
        {
            if (normalized.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 从多个运行实例中选取最可信的 exe（优先官方目录 AnthropicClaude）
    /// </summary>
    private static string? PickPreferredExePath(IEnumerable<string> paths)
    {
        var list = paths
            .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (list.Count == 0)
            return null;

        return list
            .OrderByDescending(p => p.Contains("AnthropicClaude", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(p => p.Contains(@"\Anthropic\Claude\", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(p => p.Contains(@"\Programs\Claude\", StringComparison.OrdinalIgnoreCase))
            .First();
    }

    /// <summary>
    /// 在未运行时探测常见安装路径（与路径白名单一致）
    /// </summary>
    private static string? FindInstalledClaudeDesktopExe()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        foreach (var candidate in BuildClaudeDesktopExeCandidates(localAppData, programFiles, programFilesX86))
        {
            if (File.Exists(candidate) && IsAnthropicClaudeDesktopExePath(candidate))
                return candidate;
        }

        return null;
    }

    private static IEnumerable<string> BuildClaudeDesktopExeCandidates(string localAppData, string programFiles, string programFilesX86)
    {
        yield return Path.Combine(localAppData, "AnthropicClaude", "Claude.exe");
        yield return Path.Combine(localAppData, "AnthropicClaude", "current", "Claude.exe");
        yield return Path.Combine(localAppData, "Programs", "Claude", "Claude.exe");
        yield return Path.Combine(localAppData, "Programs", "Anthropic Claude", "Claude.exe");
        yield return Path.Combine(programFiles, "Anthropic", "Claude", "Claude.exe");
        yield return Path.Combine(programFiles, "Claude", "Claude.exe");
        yield return Path.Combine(programFilesX86, "Anthropic", "Claude", "Claude.exe");
        yield return Path.Combine(programFilesX86, "Claude", "Claude.exe");
    }

    /// <summary>
    /// 命中任一则视为「非桌面端」路径，避免误判
    /// </summary>
    private static readonly string[] ClaudeDesktopPathBlacklist =
    [
        @"claude-code",
        @"claude code",
        @"claude-code.exe",
        @"\.vscode\",
        @"\vscode\",
        "node_modules",
        @"\npm\",
        @"\nvm\",
        @"\cursor\",
        @"\.cursor\",
    ];

    /// <summary>
    /// Anthropic Claude Desktop 常见安装目录片段（Windows）
    /// </summary>
    private static readonly string[] ClaudeDesktopPathWhitelist =
    [
        @"\AnthropicClaude\",
        @"\Programs\Claude\",
        @"\Anthropic\Claude\",
    ];

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
            inferenceGatewayBaseUrl = Constants.ProxyUrl,
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
