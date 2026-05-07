using System.IO;
using System.Windows;
using System.Windows.Threading;
using claude_model_setting.Services;
using claude_model_setting.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace claude_model_setting;

/// <summary>
/// 应用入口：DI 容器、Serilog、全局异常处理
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 全局 DI 服务提供者
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // 配置 Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude-model-proxy", "logs", "app-.log"),
                rollingInterval: Serilog.RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        // 三层全局异常处理
        DispatcherUnhandledException += (s, args) =>
        {
            Log.Error(args.Exception, "UI 线程未处理异常");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            Log.Error(args.ExceptionObject as Exception, "非 UI 线程未处理异常");
        };
        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            Log.Error(args.Exception, "Task 未观察异常");
            args.SetObserved();
        };

        // 配置 DI 容器
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // 启动代理服务器
        var proxyServer = Services.GetRequiredService<IProxyServerService>();
        await proxyServer.StartAsync();

        // 显示主窗口
        var mainWindow = Services.GetRequiredService<Views.MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        // 停止代理服务器
        var proxyServer = Services.GetRequiredService<IProxyServerService>();
        await proxyServer.StopAsync();

        Log.CloseAndFlush();
        base.OnExit(e);
    }

    /// <summary>
    /// 注册所有服务和 ViewModel
    /// </summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        // 服务（单例）
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<IModelResolverService, ModelResolverService>();
        services.AddSingleton<IProxyServerService, ProxyServerService>();
        services.AddSingleton<IClaudeDesktopService, ClaudeDesktopService>();

        // ViewModel
        services.AddTransient<MainViewModel>();

        // View
        services.AddTransient<Views.MainWindow>();
    }
}
