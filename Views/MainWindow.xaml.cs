using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        if (e.ChangedButton != MouseButton.Left)
            return;
        try
        {
            DragMove();
        }
        catch
        {
            // 无标题栏拖动时偶发 Win32 异常，忽略即可
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
        PageLocalization.Visibility = Visibility.Collapsed;
        PlayPageEnter(PageProviders);
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
        PageLocalization.Visibility = Visibility.Collapsed;
        PlayPageEnter(PageSettings);
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
        PageLocalization.Visibility = Visibility.Collapsed;
        PlayPageEnter(PageLogs);

        if (DataContext is MainViewModel vm)
            vm.RefreshLogsCommand.Execute(null);
    }

    /// <summary>
    /// Tab 切换：界面汉化
    /// </summary>
    private void TabLocalization_Checked(object sender, RoutedEventArgs e)
    {
        if (PageLocalization == null) return;
        PageProviders.Visibility = Visibility.Collapsed;
        PageSettings.Visibility = Visibility.Collapsed;
        PageLogs.Visibility = Visibility.Collapsed;
        PageLocalization.Visibility = Visibility.Visible;
        PlayPageEnter(PageLocalization);
    }

    /// <summary>
    /// 播放页面进入动画（淡入 + 上移）
    /// </summary>
    private void PlayPageEnter(FrameworkElement page)
    {
        if (TryFindResource("PageEnterAnim") is not Storyboard sbTemplate)
            return;

        page.BeginAnimation(UIElement.OpacityProperty, null);
        if (page.RenderTransform is TranslateTransform tt)
            tt.BeginAnimation(TranslateTransform.YProperty, null);

        var clone = sbTemplate.Clone();
        clone.Begin(page, HandoffBehavior.SnapshotAndReplace, isControllable: true);
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
}
