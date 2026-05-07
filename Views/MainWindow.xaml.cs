using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using claude_model_setting.ViewModels;

namespace claude_model_setting.Views;

/// <summary>
/// 主窗口：顶部 Tab 导航 + 三页内容
/// </summary>
public partial class MainWindow : HandyControl.Controls.Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Closing += (s, e) =>
        {
            e.Cancel = true;
            Hide();
        };
    }

    /// <summary>
    /// 自定义标题栏拖动
    /// </summary>
    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    /// <summary>
    /// Tab 切换：服务商管理
    /// </summary>
    private void TabProviders_Checked(object sender, RoutedEventArgs e)
    {
        if (PageProviders == null) return;
        PageProviders.Visibility = Visibility.Visible;
        PageSettings.Visibility = Visibility.Collapsed;
        PageLogs.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Tab 切换：系统设置
    /// </summary>
    private void TabSettings_Checked(object sender, RoutedEventArgs e)
    {
        if (PageSettings == null) return;
        PageProviders.Visibility = Visibility.Collapsed;
        PageSettings.Visibility = Visibility.Visible;
        PageLogs.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Tab 切换：请求日志
    /// </summary>
    private void TabLogs_Checked(object sender, RoutedEventArgs e)
    {
        if (PageLogs == null) return;
        PageProviders.Visibility = Visibility.Collapsed;
        PageSettings.Visibility = Visibility.Collapsed;
        PageLogs.Visibility = Visibility.Visible;

        if (DataContext is MainViewModel vm)
            vm.RefreshLogsCommand.Execute(null);
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
    /// 删除服务商
    /// </summary>
    private void DeleteProvider_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ProviderViewModel provider)
        {
            if (DataContext is MainViewModel vm)
                vm.Providers.Remove(provider);
        }
    }

    /// <summary>
    /// 删除模型
    /// </summary>
    private void DeleteModel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ModelEntryViewModel model)
        {
            var parent = TryFindParent<ItemsControl>(btn);
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
