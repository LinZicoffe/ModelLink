using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using claude_model_setting.Models;
using Serilog;

namespace claude_model_setting.Services;

/// <summary>
/// Claude Desktop 汉化服务实现
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private readonly IBackupService _backupService;

    // 资源名称前缀
    private const string ResourcePrefix = "claude_model_setting.Resources.";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public LocalizationService(IBackupService backupService)
    {
        _backupService = backupService;
    }

    #region 检测安装

    /// <summary>
    /// 查找 Claude Desktop 安装信息
    /// </summary>
    public ClaudeInstallation? FindClaude()
    {
        // 优先检测 MSIX 安装
        var msixResult = FindMsixInstallation();
        if (msixResult != null) return msixResult;

        // 检测 EXE 安装
        return FindExeInstallation();
    }

    /// <summary>
    /// 检测 MSIX 安装（C:\Program Files\WindowsApps\ 下）
    /// </summary>
    private static ClaudeInstallation? FindMsixInstallation()
    {
        var windowsApps = @"C:\Program Files\WindowsApps";
        if (!Directory.Exists(windowsApps)) return null;

        try
        {
            foreach (var dir in Directory.GetDirectories(windowsApps, "Claude_*_x64__*"))
            {
                var dirName = Path.GetFileName(dir);
                var appDir = Path.Combine(dir, "app");
                var resourcesDir = Path.Combine(appDir, "resources");
                var enUsJson = Path.Combine(resourcesDir, "en-US.json");

                if (!File.Exists(enUsJson)) continue;

                // 从目录名提取版本号
                var version = ExtractVersionFromMsixDirName(dirName);
                var packageJson = Path.Combine(appDir, "package.json");
                if (File.Exists(packageJson))
                {
                    version = ReadVersionFromPackageJson(packageJson) ?? version;
                }

                return new ClaudeInstallation
                {
                    AppDir = appDir,
                    ResourcesDir = resourcesDir,
                    IonDistDir = Path.Combine(appDir, "ion-dist"),
                    Version = version,
                    PackageName = dirName,
                    IsMsix = true,
                };
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "扫描 MSIX 安装目录失败");
        }

        return null;
    }

    /// <summary>
    /// 检测 EXE 安装
    /// </summary>
    private static ClaudeInstallation? FindExeInstallation()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        // 可能的安装根路径
        var searchPaths = new List<string>
        {
            Path.Combine(localAppData, "AnthropicClaude"),
            Path.Combine(localAppData, "Programs", "claude-desktop"),
            Path.Combine(localAppData, "Programs", "Claude"),
            Path.Combine(localAppData, "Programs", "Claude Desktop"),
            Path.Combine(localAppData, "Claude"),
            Path.Combine(localAppData, "claude-desktop"),
            Path.Combine(localAppData, "Anthropic", "Claude"),
            Path.Combine(programFiles, "Claude"),
            Path.Combine(programFiles, "Claude Desktop"),
            Path.Combine(programFiles, "Anthropic", "Claude"),
        };

        // 从注册表补充搜索路径
        searchPaths.AddRange(FindPathsFromRegistry());

        foreach (var rootPath in searchPaths)
        {
            if (!Directory.Exists(rootPath)) continue;

            var result = ProbeExeInstallation(rootPath);
            if (result != null) return result;
        }

        return null;
    }

    /// <summary>
    /// 探测 EXE 安装目录
    /// </summary>
    private static ClaudeInstallation? ProbeExeInstallation(string rootPath)
    {
        // 检查根目录下的 resources/en-US.json
        var resourcesDir = Path.Combine(rootPath, "resources");
        if (File.Exists(Path.Combine(resourcesDir, "en-US.json")))
        {
            var packageJson = Path.Combine(rootPath, "package.json");
            var version = ReadVersionFromPackageJson(packageJson) ?? "未知";

            return new ClaudeInstallation
            {
                AppDir = rootPath,
                ResourcesDir = resourcesDir,
                IonDistDir = Path.Combine(rootPath, "ion-dist"),
                Version = version,
                IsMsix = false,
            };
        }

        // 检查版本化子目录 app-X.X.X
        try
        {
            foreach (var dir in Directory.GetDirectories(rootPath, "app-*"))
            {
                var subResources = Path.Combine(dir, "resources");
                if (File.Exists(Path.Combine(subResources, "en-US.json")))
                {
                    var dirName = Path.GetFileName(dir);
                    var version = dirName.StartsWith("app-") ? dirName[4..] : dirName;
                    var packageJson = Path.Combine(dir, "package.json");
                    var pkgVersion = ReadVersionFromPackageJson(packageJson);
                    if (pkgVersion != null) version = pkgVersion;

                    return new ClaudeInstallation
                    {
                        AppDir = dir,
                        ResourcesDir = subResources,
                        IonDistDir = Path.Combine(dir, "ion-dist"),
                        Version = version,
                        IsMsix = false,
                    };
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "扫描版本化目录失败: {Path}", rootPath);
        }

        // 检查 current 子目录
        var currentDir = Path.Combine(rootPath, "current");
        if (Directory.Exists(currentDir))
        {
            return ProbeExeInstallation(currentDir);
        }

        return null;
    }

    /// <summary>
    /// 从注册表查找安装路径
    /// </summary>
    private static List<string> FindPathsFromRegistry()
    {
        var paths = new List<string>();
        var keys = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        };

        foreach (var keyPath in keys)
        {
            using var hkcu = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath);
            using var hklm = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);

            foreach (var root in new[] { hkcu, hklm })
            {
                if (root == null) continue;
                foreach (var subKeyName in root.GetSubKeyNames())
                {
                    using var subKey = root.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    var installLocation = subKey.GetValue("InstallLocation") as string;
                    var displayIcon = subKey.GetValue("DisplayIcon") as string;
                    var displayName = subKey.GetValue("DisplayName") as string;

                    // 检查是否与 Claude 相关
                    if (!IsClaudeRelated(displayName) && !IsClaudeRelated(installLocation) && !IsClaudeRelated(displayIcon))
                        continue;

                    if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                        paths.Add(installLocation);

                    if (!string.IsNullOrEmpty(displayIcon))
                    {
                        var iconDir = Path.GetDirectoryName(displayIcon);
                        if (!string.IsNullOrEmpty(iconDir) && Directory.Exists(iconDir))
                            paths.Add(iconDir);
                    }
                }
            }
        }

        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsClaudeRelated(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return value.Contains("claude", StringComparison.OrdinalIgnoreCase)
            || value.Contains("anthropic", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 从 MSIX 目录名提取版本号
    /// </summary>
    private static string ExtractVersionFromMsixDirName(string dirName)
    {
        // Claude_1.0.5_x64__anthropic
        var parts = dirName.Split('_');
        return parts.Length >= 2 ? parts[1] : dirName;
    }

    /// <summary>
    /// 从 package.json 读取版本号
    /// </summary>
    private static string? ReadVersionFromPackageJson(string packageJsonPath)
    {
        if (!File.Exists(packageJsonPath)) return null;
        try
        {
            var json = File.ReadAllText(packageJsonPath);
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("version", out var v) ? v.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region 检查汉化状态

    /// <summary>
    /// 检查汉化状态：三个条件同时满足才算 Patched
    /// </summary>
    public PatchStatus CheckPatchStatus(ClaudeInstallation installation)
    {
        // 条件 1: ion-dist/i18n/zh-CN.json 存在
        var zhCNJson = Path.Combine(installation.IonDistDir, "i18n", "zh-CN.json");
        var hasZhCNFile = File.Exists(zhCNJson);

        // 条件 2: ion-dist/assets/v1/index-*.js 包含 "zh-CN"
        var hasZhCNInJs = CheckZhCNInIndexJs(installation);

        // 条件 3: config.json 中 locale 为 zh-CN
        var hasLocaleConfig = CheckLocaleInConfig();

        if (hasZhCNFile && hasZhCNInJs && hasLocaleConfig)
            return PatchStatus.Patched;

        return PatchStatus.Unpatched;
    }

    /// <summary>
    /// 检查 index-*.js 文件是否包含 zh-CN
    /// </summary>
    private static bool CheckZhCNInIndexJs(ClaudeInstallation installation)
    {
        var assetsDir = Path.Combine(installation.IonDistDir, "assets", "v1");
        if (!Directory.Exists(assetsDir)) return false;

        try
        {
            foreach (var jsFile in Directory.GetFiles(assetsDir, "index-*.js"))
            {
                var content = File.ReadAllText(jsFile);
                if (content.Contains("\"zh-CN\"", StringComparison.Ordinal))
                    return true;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "读取 index-*.js 文件失败");
        }

        return false;
    }

    /// <summary>
    /// 检查 Claude 配置文件中 locale 是否为 zh-CN
    /// </summary>
    private static bool CheckLocaleInConfig()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var configPaths = new[]
        {
            Path.Combine(appData, "Claude", "config.json"),
            Path.Combine(appData, "Claude-3p", "config.json"),
        };

        foreach (var configPath in configPaths)
        {
            if (!File.Exists(configPath)) continue;
            try
            {
                var json = File.ReadAllText(configPath);
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("locale", out var locale))
                {
                    if (locale.GetString() == "zh-CN")
                        return true;
                }
            }
            catch { /* 忽略 */ }
        }

        return false;
    }

    #endregion

    #region 执行汉化

    /// <summary>
    /// 执行汉化：8 步流程
    /// </summary>
    public async Task ApplyPatchAsync(ClaudeInstallation installation, IProgress<string> progress)
    {
        // 步骤 1: 关闭 Claude Desktop
        progress.Report("正在关闭 Claude Desktop...");
        await KillClaudeDesktopAsync();

        // 步骤 2: 获取文件权限
        progress.Report("正在获取文件权限...");
        await TakeOwnershipAsync(installation.IonDistDir);
        await TakeOwnershipAsync(installation.ResourcesDir);

        // 验证权限
        var testFile = Path.Combine(installation.ResourcesDir, ".permission_test");
        try
        {
            await File.WriteAllTextAsync(testFile, "test");
            File.Delete(testFile);
        }
        catch
        {
            throw new InvalidOperationException("无法写入目标目录，请确认以管理员身份运行");
        }

        // 步骤 3: 备份原始文件
        progress.Report("正在备份原始文件...");
        await Task.Run(() => _backupService.CreateBackup(installation));

        // 步骤 4: 写入翻译文件
        progress.Report("正在写入翻译文件...");
        await WriteTranslationFilesAsync(installation);

        // 步骤 5: 注入语言白名单
        progress.Report("正在注入语言白名单...");
        await InjectLanguageWhitelistAsync(installation);

        // 步骤 6: 设置语言配置
        progress.Report("正在设置语言配置...");
        await SetLocaleConfigAsync("zh-CN");

        // 步骤 7: 验证汉化结果
        progress.Report("正在验证汉化结果...");
        var status = CheckPatchStatus(installation);
        if (status != PatchStatus.Patched)
        {
            throw new InvalidOperationException("汉化验证失败，请检查 Claude Desktop 版本是否兼容");
        }

        // 步骤 8: 重启 Claude Desktop
        progress.Report("正在重启 Claude Desktop...");
        await RestartClaudeDesktopAsync(installation);

        progress.Report("汉化完成！");
    }

    /// <summary>
    /// 写入翻译文件
    /// </summary>
    private async Task WriteTranslationFilesAsync(ClaudeInstallation installation)
    {
        // 读取嵌入式翻译资源
        var zhCNContent = ReadEmbeddedResource("zh-CN.json");
        var desktopZhCNContent = ReadEmbeddedResource("desktop-zh-CN.json");
        var statsigZhCNContent = ReadEmbeddedResource("statsig-zh-CN.json");

        if (string.IsNullOrEmpty(zhCNContent))
            throw new InvalidOperationException("翻译资源 zh-CN.json 未找到");
        if (string.IsNullOrEmpty(desktopZhCNContent))
            throw new InvalidOperationException("翻译资源 desktop-zh-CN.json 未找到");
        if (string.IsNullOrEmpty(statsigZhCNContent))
            throw new InvalidOperationException("翻译资源 statsig-zh-CN.json 未找到");

        // 写入 resources/zh-CN.json（直接写入 desktop-zh-CN.json 的内容）
        var resourcesZhCN = Path.Combine(installation.ResourcesDir, "zh-CN.json");
        await AtomicWriteWithPermissionsAsync(resourcesZhCN, desktopZhCNContent);

        // 合并 ion-dist/i18n/en-US.json 和 zh-CN.json（深度合并）
        var i18nDir = Path.Combine(installation.IonDistDir, "i18n");
        Directory.CreateDirectory(i18nDir);

        var enUSPath = Path.Combine(i18nDir, "en-US.json");
        string? enUSContent = null;
        if (File.Exists(enUSPath))
        {
            enUSContent = await File.ReadAllTextAsync(enUSPath);
        }

        string mergedJson;
        if (!string.IsNullOrEmpty(enUSContent))
        {
            // 深度合并：中文覆盖英文，英文 key 作为回退
            var baseNode = JsonNode.Parse(enUSContent) as JsonObject;
            var overlayNode = JsonNode.Parse(zhCNContent) as JsonObject;
            var merged = DeepMerge(baseNode, overlayNode);
            mergedJson = merged.ToJsonString(JsonOpts);
        }
        else
        {
            mergedJson = zhCNContent;
        }

        var zhCNPath = Path.Combine(i18nDir, "zh-CN.json");
        await AtomicWriteWithPermissionsAsync(zhCNPath, mergedJson);

        // 写入 ion-dist/i18n/statsig/zh-CN.json
        var statsigDir = Path.Combine(i18nDir, "statsig");
        Directory.CreateDirectory(statsigDir);
        var statsigZhCNPath = Path.Combine(statsigDir, "zh-CN.json");
        await AtomicWriteWithPermissionsAsync(statsigZhCNPath, statsigZhCNContent);

        Log.Information("翻译文件写入完成");
    }

    /// <summary>
    /// 注入语言白名单到 index-*.js
    /// </summary>
    private async Task InjectLanguageWhitelistAsync(ClaudeInstallation installation)
    {
        var assetsDir = Path.Combine(installation.IonDistDir, "assets", "v1");
        if (!Directory.Exists(assetsDir))
        {
            throw new DirectoryNotFoundException($"资源目录不存在: {assetsDir}");
        }

        var jsFiles = Directory.GetFiles(assetsDir, "index-*.js");
        if (jsFiles.Length == 0)
        {
            throw new FileNotFoundException("未找到 index-*.js 文件");
        }

        // 正则模式（按优先级尝试）
        var patterns = new[]
        {
            @"\[""en-US""(?:,""[a-zA-Z]{2,3}(?:-[a-zA-Z0-9]{2,4})*"")+",
            @"\[""en-US""(?:\s*,\s*""[a-zA-Z]{2,3}(?:-[a-zA-Z0-9]{2,4})*"")+\s*",
            @"\[(?:""[a-z]{2}(?:-[A-Za-z0-9]{2,4})*""\s*,\s*)*""en-US""(?:\s*,\s*""[a-z]{2}(?:-[A-Za-z0-9]{2,4})*"")*\s*",
        };

        var patched = false;

        foreach (var jsFile in jsFiles)
        {
            // 获取文件权限
            await TakeOwnershipAsync(jsFile);

            var content = await File.ReadAllTextAsync(jsFile);

            // 已包含 zh-CN 则跳过
            if (content.Contains("\"zh-CN\"", StringComparison.Ordinal))
            {
                Log.Information("文件已包含 zh-CN，跳过: {File}", Path.GetFileName(jsFile));
                patched = true;
                continue;
            }

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(content, pattern);
                if (match.Success)
                {
                    // 在匹配的数组末尾 ] 前插入 ,"zh-CN"
                    var insertPos = match.Index + match.Length;
                    // 找到 ] 位置
                    var bracketPos = content.IndexOf(']', insertPos > 0 ? insertPos - 1 : 0);
                    if (bracketPos > match.Index)
                    {
                        content = content[..bracketPos] + ",\"zh-CN\"" + content[bracketPos..];
                        await AtomicWriteWithPermissionsAsync(jsFile, content);
                        Log.Information("已注入 zh-CN 到: {File}", Path.GetFileName(jsFile));
                        patched = true;
                        break;
                    }
                }
            }
        }

        if (!patched)
        {
            throw new InvalidOperationException("未找到语言白名单数组，无法注入 zh-CN。Claude Desktop 版本可能不兼容。");
        }
    }

    /// <summary>
    /// 设置 locale 配置
    /// </summary>
    private static async Task SetLocaleConfigAsync(string locale)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var configPaths = new[]
        {
            Path.Combine(appData, "Claude", "config.json"),
            Path.Combine(appData, "Claude-3p", "config.json"),
        };

        foreach (var configPath in configPaths)
        {
            var dir = Path.GetDirectoryName(configPath)!;
            Directory.CreateDirectory(dir);

            JsonObject? config = null;
            if (File.Exists(configPath))
            {
                try
                {
                    var existing = await File.ReadAllTextAsync(configPath);
                    config = JsonNode.Parse(existing) as JsonObject;
                }
                catch { /* 解析失败则重新创建 */ }
            }

            config ??= new JsonObject();
            config["locale"] = locale;

            await File.WriteAllTextAsync(configPath, config.ToJsonString(JsonOpts));
            Log.Information("已设置 locale 配置: {Path}", configPath);
        }
    }

    #endregion

    #region 恢复原版

    /// <summary>
    /// 恢复原版：6 步流程
    /// </summary>
    public async Task RemovePatchAsync(ClaudeInstallation installation, IProgress<string> progress)
    {
        // 步骤 1: 关闭 Claude Desktop
        progress.Report("正在关闭 Claude Desktop...");
        await KillClaudeDesktopAsync();

        // 步骤 2: 获取文件权限
        progress.Report("正在获取文件权限...");
        await TakeOwnershipAsync(installation.IonDistDir);
        await TakeOwnershipAsync(installation.ResourcesDir);

        // 步骤 3: 从备份恢复
        progress.Report("正在从备份恢复...");
        await Task.Run(() => _backupService.RestoreBackup(installation));

        // 步骤 4: 删除翻译文件
        progress.Report("正在删除翻译文件...");
        DeleteTranslationFiles(installation);

        // 步骤 5: 移除 locale 配置
        progress.Report("正在移除语言配置...");
        await RemoveLocaleConfigAsync();

        // 步骤 6: 重启 Claude Desktop
        progress.Report("正在重启 Claude Desktop...");
        await RestartClaudeDesktopAsync(installation);

        progress.Report("已恢复原版！");
    }

    /// <summary>
    /// 删除翻译文件
    /// </summary>
    private static void DeleteTranslationFiles(ClaudeInstallation installation)
    {
        var filesToDelete = new[]
        {
            Path.Combine(installation.ResourcesDir, "zh-CN.json"),
            Path.Combine(installation.IonDistDir, "i18n", "zh-CN.json"),
            Path.Combine(installation.IonDistDir, "i18n", "statsig", "zh-CN.json"),
        };

        foreach (var file in filesToDelete)
        {
            if (!File.Exists(file)) continue;
            try
            {
                // 清除只读属性
                var attrs = File.GetAttributes(file);
                if (attrs.HasFlag(FileAttributes.ReadOnly))
                    File.SetAttributes(file, attrs & ~FileAttributes.ReadOnly);

                File.Delete(file);
                Log.Information("已删除: {File}", Path.GetFileName(file));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "删除文件失败: {File}", file);
            }
        }
    }

    /// <summary>
    /// 移除 locale 配置
    /// </summary>
    private static async Task RemoveLocaleConfigAsync()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var configPaths = new[]
        {
            Path.Combine(appData, "Claude", "config.json"),
            Path.Combine(appData, "Claude-3p", "config.json"),
        };

        foreach (var configPath in configPaths)
        {
            if (!File.Exists(configPath)) continue;
            try
            {
                var json = await File.ReadAllTextAsync(configPath);
                var node = JsonNode.Parse(json) as JsonObject;
                if (node == null) continue;

                if (node.ContainsKey("locale"))
                {
                    node.Remove("locale");
                    await File.WriteAllTextAsync(configPath, node.ToJsonString(JsonOpts));
                    Log.Information("已移除 locale 配置: {Path}", configPath);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "移除 locale 配置失败: {Path}", configPath);
            }
        }
    }

    #endregion

    #region JSON 深度合并

    /// <summary>
    /// 深度合并两个 JsonObject：overlay 的 key 覆盖 base 的 key
    /// 如果两边都是 Object 则递归合并，否则 overlay 直接替换
    /// </summary>
    private static JsonObject DeepMerge(JsonObject? baseObj, JsonObject? overlayObj)
    {
        var result = new JsonObject();

        // 先复制 base 的所有 key
        if (baseObj != null)
        {
            foreach (var (key, value) in baseObj)
            {
                result[key] = value?.DeepClone();
            }
        }

        // 再用 overlay 覆盖
        if (overlayObj != null)
        {
            foreach (var (key, value) in overlayObj)
            {
                if (value is JsonObject overlayChild
                    && result.TryGetPropertyValue(key, out var existingNode)
                    && existingNode is JsonObject baseChild)
                {
                    // 两侧都是 Object，递归合并
                    result[key] = DeepMerge(baseChild, overlayChild);
                }
                else
                {
                    result[key] = value?.DeepClone();
                }
            }
        }

        return result;
    }

    #endregion

    #region 进程辅助

    /// <summary>
    /// 关闭 Claude Desktop 进程（仅针对 Anthropic 官方桌面端，避免误杀 Claude Code 等同名进程）
    /// </summary>
    private static async Task KillClaudeDesktopAsync()
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
                Log.Debug(ex, "无法读取进程 {Pid} 的主模块路径，跳过", proc.Id);
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

        if (desktopProcesses.Count > 0)
        {
            await Task.Delay(2000);
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
    /// 重启 Claude Desktop
    /// </summary>
    private static async Task RestartClaudeDesktopAsync(ClaudeInstallation installation)
    {
        await Task.Delay(1000);

        string? exePath = null;

        // 尝试从安装目录查找
        if (!installation.IsMsix)
        {
            var candidate = Path.Combine(installation.AppDir, "Claude.exe");
            if (File.Exists(candidate)) exePath = candidate;

            // 向上查找
            if (exePath == null)
            {
                var parentDir = Path.GetDirectoryName(installation.AppDir);
                if (parentDir != null)
                {
                    candidate = Path.Combine(parentDir, "Claude.exe");
                    if (File.Exists(candidate)) exePath = candidate;
                }
            }
        }

        // MSIX 或未找到 exe，尝试通过 PowerShell 启动
        if (exePath == null)
        {
            // 尝试常见安装路径
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var candidates = new[]
            {
                Path.Combine(localAppData, "AnthropicClaude", "Claude.exe"),
                Path.Combine(localAppData, "AnthropicClaude", "current", "Claude.exe"),
                Path.Combine(localAppData, "Programs", "Claude", "Claude.exe"),
            };

            foreach (var c in candidates)
            {
                if (File.Exists(c)) { exePath = c; break; }
            }
        }

        if (exePath != null)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
            });
            Log.Information("已启动 Claude Desktop: {Path}", exePath);
        }
        else if (installation.IsMsix && !string.IsNullOrEmpty(installation.PackageName))
        {
            // MSIX 包启动
            try
            {
                var psOutput = RunCommandCapture("powershell",
                    $"-Command \"(Get-AppxPackage | Where-Object {{$_.Name -like '*Claude*'}}).PackageFamilyName\"");
                if (!string.IsNullOrEmpty(psOutput))
                {
                    var familyName = psOutput.Trim().Split('\n').FirstOrDefault()?.Trim();
                    if (!string.IsNullOrEmpty(familyName))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"shell:AppsFolder\\{familyName}!App",
                            UseShellExecute = true,
                        });
                        Log.Information("已通过 MSIX 启动 Claude Desktop: {Family}", familyName);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "通过 MSIX 启动 Claude Desktop 失败");
            }
        }
        else
        {
            Log.Warning("未找到 Claude Desktop 可执行文件，请手动启动");
        }
    }

    /// <summary>
    /// 获取文件/目录所有权（异步）
    /// </summary>
    private static Task TakeOwnershipAsync(string path)
    {
        return Task.Run(() =>
        {
            RunCommandSilent("takeown", $"/F \"{path}\" /R /A /D Y");
            RunCommandSilent("icacls", $"\"{path}\" /grant Administrators:F /T /Q");
        });
    }

    /// <summary>
    /// 带权限的原子写入
    /// </summary>
    private static async Task AtomicWriteWithPermissionsAsync(string path, string content)
    {
        // 确保目录存在
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        // 清除只读属性
        if (File.Exists(path))
        {
            var attrs = File.GetAttributes(path);
            if (attrs.HasFlag(FileAttributes.ReadOnly))
                File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);
        }

        // 原子写入
        var tmpPath = path + ".tmp";
        await File.WriteAllTextAsync(tmpPath, content, Encoding.UTF8);
        File.Move(tmpPath, path, overwrite: true);
    }

    /// <summary>
    /// 执行外部命令（无窗口，不捕获输出）
    /// </summary>
    private static void RunCommandSilent(string fileName, string arguments)
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
            process?.WaitForExit(60000);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "执行命令失败: {Cmd} {Args}", fileName, arguments);
        }
    }

    /// <summary>
    /// 执行外部命令并捕获输出
    /// </summary>
    private static string? RunCommandCapture(string fileName, string arguments)
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
                StandardOutputEncoding = Encoding.UTF8,
            });
            if (process == null) return null;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(30000);
            return output;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "执行命令失败: {Cmd} {Args}", fileName, arguments);
            return null;
        }
    }

    /// <summary>
    /// 读取嵌入式资源
    /// </summary>
    private static string? ReadEmbeddedResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var fullName = ResourcePrefix + resourceName;

        using var stream = assembly.GetManifestResourceStream(fullName);
        if (stream == null)
        {
            Log.Error("嵌入式资源未找到: {Name}", fullName);
            return null;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    #endregion
}
