# ModelLink (C# WPF 版)

> **本软件完全免费，仅供个人学习和非商业用途。严禁任何形式的商业化行为。**
>
> 原作者：**抖音 Winhao学AI**（抖音号：54927876676）
>
> C# WPF 版本基于原 Rust 版本功能复刻，使用 WPF + HandyControl 原生界面替代 WebView。

让 Claude Desktop 桌面端接入任意第三方 API 模型的本地代理工具。

## 功能

- 将第三方模型（DeepSeek、Kimi、智谱 GLM 等）接入 Claude Desktop
- 支持同时配置多个 API 服务商
- 支持 1M 上下文模型变体
- 原生 WPF 界面（HandyControl），无需 WebView 运行时
- SSE 流式转发，零缓冲实时响应
- 系统托盘常驻，关闭窗口后代理继续运行
- 连接测试、请求日志（带成功/失败状态指示）
- 开机自启动

## 技术栈

| 组件 | 技术 |
|------|------|
| UI 框架 | WPF + HandyControl 3.5.1 |
| MVVM | CommunityToolkit.Mvvm 8.x |
| HTTP 代理 | ASP.NET Core Kestrel（自宿主） |
| JSON 序列化 | System.Text.Json |
| DI 容器 | Microsoft.Extensions.DependencyInjection |
| 日志 | Serilog + Serilog.Sinks.File |
| 目标框架 | .NET 8.0 (Windows) |

## 项目结构

```
claude model setting/
├── Models/                     # 数据模型
│   ├── AppConfig.cs            # 配置根模型
│   ├── Provider.cs             # 服务商模型
│   ├── ModelEntry.cs           # 模型条目
│   ├── LogEntry.cs             # 请求日志
│   ├── ModelSlot.cs            # 8 个 Claude 模型槽位
│   ├── ResolvedModel.cs        # 模型解析结果
│   └── ApiResponse.cs          # API 响应
├── ViewModels/                 # 视图模型（MVVM）
│   ├── MainViewModel.cs        # 主 VM
│   ├── ProviderViewModel.cs    # 服务商卡片 VM
│   └── ModelEntryViewModel.cs  # 模型行 VM
├── Views/                      # 视图
│   ├── MainWindow.xaml/.cs     # 主窗口
│   └── Converters/             # 值转换器
├── Services/                   # 服务层
│   ├── ConfigService.cs        # JSON 配置读写
│   ├── ProxyServerService.cs   # Kestrel 代理服务器
│   ├── ClaudeDesktopService.cs # Claude Desktop 集成
│   └── ModelResolverService.cs # 模型槽位映射
├── Helpers/                    # 工具类
│   ├── FileSystemHelper.cs     # 原子写入
│   ├── AutoStartHelper.cs      # 注册表自启动
│   └── TimeHelper.cs           # 时间格式化
└── Assets/                     # 资源文件
```

## 构建与运行

需要 .NET 8.0 SDK：

```bash
dotnet build
dotnet run
```

## 使用方法

### 第一步：首次配置 Claude Desktop

1. 打开 Claude Desktop，完成初始启动
2. 点击左上角 **☰ 汉堡菜单**
3. 进入 **Help > Troubleshooting > Enable Developer Mode**
4. 完全关闭并重新打开 Claude Desktop
5. 再次点击 **☰ 汉堡菜单**，进入 **Developer > Configure third-party inference**
6. 在配置面板中：
   - **Backend** 选择 `Gateway (Anthropic-compatible)`
   - **Gateway URL** 填写 `http://127.0.0.1:5678`
   - **API Key** 填写 `proxy`
7. 点击 **Apply locally** 保存

### 第二步：配置 ModelLink

1. 运行 ModelLink
2. 点击「+ 添加服务商」
3. 填写第三方 API 信息：
   - **API 地址**：如 `https://api.deepseek.com/anthropic`
   - **API 密钥**：第三方平台申请的 API Key
4. 点击「+ 添加模型」，填写模型名称
5. 点击「测试连接」验证
6. 点击「保存配置」
7. 点击「应用到 Claude Desktop」

### 第三步：开始使用

在 Claude Desktop 的模型选择器中选择配置的模型即可。

## 工作原理

```
Claude Desktop → http://127.0.0.1:5678 (本代理) → 第三方 API
```

1. Claude Desktop 的所有 API 请求发送到本地代理（端口 5678）
2. 代理根据请求中的模型名称，查找对应的第三方服务商配置
3. 将模型名称替换为 Claude 模型槽位名（如 `claude-3-opus-latest`）
4. 使用服务商的真实 API Key 转发请求到第三方 API
5. SSE 流式回传响应给 Claude Desktop

最多支持 8 个模型槽位，映射到 Claude Desktop 中的模型选择器。

## 配置文件

- 应用配置：`%USERPROFILE%\.claude-model-proxy\config.json`
- 日志文件：`%USERPROFILE%\.claude-model-proxy\logs\`
- Claude Desktop 配置：`%APPDATA%\Claude-3p\configLibrary\`

## 与原 Rust 版本的差异

| 项目 | Rust 原版 | C# WPF 版 |
|------|-----------|-----------|
| UI | WRY/WebView + HTML | WPF + HandyControl |
| HTTP 服务器 | Axum | ASP.NET Core Kestrel |
| HTTP 客户端 | reqwest | HttpClient |
| JSON | serde | System.Text.Json |
| 系统托盘 | tray_icon crate | HandyControl NotifyIcon |
| 运行时 | 无（单文件编译） | 需要 .NET 8.0 Runtime |

## 免责声明

- 本软件基于 **抖音 Winhao学AI** 的原版 ModelLink 功能复刻
- **完全免费，不可商业化**
- 仅供学习和技术研究使用
