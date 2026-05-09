using System.Security.Principal;
using claude_model_setting.Models;
using claude_model_setting.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace claude_model_setting.ViewModels;

/// <summary>
/// 汉化功能 ViewModel
/// </summary>
public sealed partial class LocalizationViewModel : ObservableObject
{
    private readonly ILocalizationService _localizationService;
    private ClaudeInstallation? _installation;

    [ObservableProperty]
    private PatchStatus _patchStatus = PatchStatus.NotInstalled;

    [ObservableProperty]
    private string? _claudeVersion;

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private bool _isAdmin;

    /// <summary>
    /// 是否可以执行汉化
    /// </summary>
    public bool CanPatch => IsAdmin && IsInstalled && !IsProcessing;

    /// <summary>
    /// 是否可以恢复
    /// </summary>
    public bool CanRestore => IsAdmin && IsInstalled && PatchStatus == PatchStatus.Patched && !IsProcessing;

    /// <summary>
    /// 汉化按钮文本
    /// </summary>
    public string PatchButtonText => PatchStatus == PatchStatus.Patched ? "重新汉化" : "一键汉化";

    public LocalizationViewModel(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
        IsAdmin = IsRunningAsAdmin();
        RefreshStatus();
    }

    /// <summary>
    /// 重新检测安装和汉化状态
    /// </summary>
    [RelayCommand]
    private void RefreshStatus()
    {
        try
        {
            _installation = _localizationService.FindClaude();

            if (_installation != null)
            {
                IsInstalled = true;
                ClaudeVersion = _installation.Version;
                PatchStatus = _localizationService.CheckPatchStatus(_installation);
                StatusMessage = PatchStatus switch
                {
                    PatchStatus.Patched => $"已汉化 (Claude Desktop v{ClaudeVersion})",
                    PatchStatus.Unpatched => $"检测到 Claude Desktop v{ClaudeVersion}，未汉化",
                    _ => "未知状态",
                };
            }
            else
            {
                IsInstalled = false;
                ClaudeVersion = null;
                PatchStatus = PatchStatus.NotInstalled;
                StatusMessage = "未检测到 Claude Desktop 安装";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "检测 Claude Desktop 状态失败");
            StatusMessage = $"检测失败: {ex.Message}";
        }

        UpdateDerivedProperties();
    }

    /// <summary>
    /// 一键汉化
    /// </summary>
    [RelayCommand]
    private async Task ApplyPatchAsync()
    {
        if (_installation == null || !CanPatch) return;

        IsProcessing = true;
        StatusMessage = "正在汉化...";
        UpdateDerivedProperties();

        var progress = new Progress<string>(msg =>
        {
            StatusMessage = msg;
        });

        try
        {
            await _localizationService.ApplyPatchAsync(_installation, progress);
            RefreshStatus();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "汉化失败");
            StatusMessage = $"汉化失败: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
            UpdateDerivedProperties();
        }
    }

    /// <summary>
    /// 一键恢复原版
    /// </summary>
    [RelayCommand]
    private async Task RemovePatchAsync()
    {
        if (_installation == null || !CanRestore) return;

        IsProcessing = true;
        StatusMessage = "正在恢复原版...";
        UpdateDerivedProperties();

        var progress = new Progress<string>(msg =>
        {
            StatusMessage = msg;
        });

        try
        {
            await _localizationService.RemovePatchAsync(_installation, progress);
            RefreshStatus();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "恢复原版失败");
            StatusMessage = $"恢复失败: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
            UpdateDerivedProperties();
        }
    }

    /// <summary>
    /// 更新派生属性通知
    /// </summary>
    private void UpdateDerivedProperties()
    {
        OnPropertyChanged(nameof(CanPatch));
        OnPropertyChanged(nameof(CanRestore));
        OnPropertyChanged(nameof(PatchButtonText));
    }

    partial void OnPatchStatusChanged(PatchStatus value)
    {
        UpdateDerivedProperties();
    }

    partial void OnIsInstalledChanged(bool value)
    {
        UpdateDerivedProperties();
    }

    partial void OnIsProcessingChanged(bool value)
    {
        UpdateDerivedProperties();
    }

    /// <summary>
    /// 检测是否以管理员身份运行
    /// </summary>
    private static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
