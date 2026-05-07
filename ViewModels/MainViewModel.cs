using System.Collections.ObjectModel;
using claude_model_setting.Models;
using claude_model_setting.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace claude_model_setting.ViewModels;

/// <summary>
/// 主视图模型
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IConfigService _configService;
    private readonly IClaudeDesktopService _claudeDesktopService;

    [ObservableProperty]
    private bool _isServerRunning;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isAutoStartEnabled;

    [ObservableProperty]
    private string _logSummary = "暂无日志";

    [ObservableProperty]
    private int _logSuccessCount;

    [ObservableProperty]
    private int _logFailCount;

    [ObservableProperty]
    private bool _hasProviders;

    [ObservableProperty]
    private bool _hasLogs;

    public ObservableCollection<ProviderViewModel> Providers { get; } = [];
    public ObservableCollection<LogEntry> Logs { get; } = [];

    public MainViewModel(
        IConfigService configService,
        IClaudeDesktopService claudeDesktopService,
        IProxyServerService proxyServerService)
    {
        _configService = configService;
        _claudeDesktopService = claudeDesktopService;
        IsServerRunning = true;
        IsAutoStartEnabled = Helpers.AutoStartHelper.IsEnabled();
        LoadConfig();
    }

    private void LoadConfig()
    {
        var config = _configService.CurrentConfig;
        Providers.Clear();
        var index = 0;
        foreach (var provider in config.Providers)
        {
            var pvm = new ProviderViewModel(provider) { Index = index++ };
            Providers.Add(pvm);
        }
        UpdateHasProviders();
        Providers.CollectionChanged += (_, _) => UpdateHasProviders();
    }

    private void UpdateHasProviders()
    {
        HasProviders = Providers.Count > 0;
        var idx = 0;
        foreach (var p in Providers)
            p.Index = idx++;
    }

    private void UpdateHasLogs()
    {
        HasLogs = Logs.Count > 0;
    }

    private AppConfig BuildConfig()
    {
        return new AppConfig
        {
            Providers = Providers.Select(p => p.ToProvider()).ToList(),
        };
    }

    [RelayCommand]
    private void AddProvider()
    {
        Providers.Add(new ProviderViewModel());
    }

    /// <summary>
    /// 托盘点击显示窗口
    /// </summary>
    [RelayCommand]
    private void ShowWindow()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var window = System.Windows.Application.Current.MainWindow;
            if (window != null)
            {
                window.Show();
                window.Activate();
                window.WindowState = System.Windows.WindowState.Normal;
            }
        });
    }

    [RelayCommand]
    private async Task SaveConfigAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var config = BuildConfig();
            await _configService.SaveConfigAsync(config);
            HandyControl.Controls.Growl.Success("配置已保存");
        }
        catch (Exception ex)
        {
            HandyControl.Controls.Growl.Error($"保存失败: {ex.Message}");
            Log.Error(ex, "保存配置失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApplyToClaudeAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var config = BuildConfig();
            await _configService.SaveConfigAsync(config);
            var msg = await _claudeDesktopService.ApplyToClaudeDesktopAsync();
            HandyControl.Controls.Growl.Success(msg);
        }
        catch (Exception ex)
        {
            HandyControl.Controls.Growl.Error($"应用失败: {ex.Message}");
            Log.Error(ex, "应用到 Claude Desktop 失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void RefreshLogs()
    {
        var proxyService = App.Services.GetService(typeof(IProxyServerService)) as IProxyServerService;
        if (proxyService == null) return;

        Logs.Clear();
        foreach (var log in proxyService.GetLogs())
        {
            Logs.Add(log);
        }
        LogSuccessCount = Logs.Count(l => l.IsSuccess);
        LogFailCount = Logs.Count - LogSuccessCount;
        LogSummary = Logs.Count > 0 ? $"共 {Logs.Count} 条记录" : "暂无日志";
        UpdateHasLogs();
    }

    [RelayCommand]
    private void ClearLogs()
    {
        Logs.Clear();
        LogSuccessCount = 0;
        LogFailCount = 0;
        LogSummary = "暂无日志";
        UpdateHasLogs();
    }

    partial void OnIsAutoStartEnabledChanged(bool value)
    {
        try
        {
            Helpers.AutoStartHelper.SetEnabled(value);
        }
        catch
        {
            // 忽略注册表操作失败
        }
    }
}
