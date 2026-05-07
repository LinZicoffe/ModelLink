using System.IO;
using System.Windows;
using System.Windows.Controls;
using claude_model_setting.ViewModels;

namespace claude_model_setting.Views;

/// <summary>
/// 主窗口：使用 HandyControl NotifyIcon 系统托盘
/// </summary>
public partial class MainWindow : HandyControl.Controls.Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // 关闭时隐藏到托盘
        Closing += (s, e) =>
        {
            e.Cancel = true;
            Hide();
        };
    }

    /// <summary>
    /// 托盘菜单 - 显示窗口
    /// </summary>
    private void ShowWindow_Click(object sender, RoutedEventArgs e)
    {
        Show();
        Activate();
        WindowState = WindowState.Normal;
    }

    /// <summary>
    /// 托盘菜单 - 退出应用
    /// </summary>
    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    /// <summary>
    /// 删除服务商按钮
    /// </summary>
    private void DeleteProvider_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.DataContext is ProviderViewModel provider)
        {
            if (DataContext is MainViewModel vm)
                vm.Providers.Remove(provider);
        }
    }

    /// <summary>
    /// 删除模型按钮
    /// </summary>
    private void DeleteModel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.DataContext is ModelEntryViewModel model)
        {
            var parent = TryFindParent<System.Windows.Controls.ItemsControl>(btn);
            if (parent?.DataContext is ProviderViewModel provider)
                provider.Models.Remove(model);
        }
    }

    private static T? TryFindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T result) return result;
            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
        }
        return null;
    }
}
