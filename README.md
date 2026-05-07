# ModelLink (C# WPF 版)

> **本软件完全免费，仅供个人学习和非商业用途。严禁任何形式的商业化行为。**
>
> 原作者：**抖音 Winhao学AI**（抖音号：54927876676）
>
> C# WPF 版本基于原 Rust 版本功能复刻，使用 WPF + HandyControl 原生界面替代 WebView。

让 Claude Desktop 桌面端接入任意第三方 API 模型的本地代理工具。

## 功能

- 将第三方模型（DeepSeek、Kimi、智谱 GLM 等）接入 Claude Desktop
- 支持同时配置多个 API 服务商，每个服务商独立配色标识
- 支持 1M 上下文模型变体
- 原生 WPF 界面（HandyControl），无需 WebView 运行时
- 顶部 Tab 导航（服务商管理 / 系统设置 / 请求日志）
- 连接测试（延迟检测 + 错误信息提取）
- SSE 流式转发，零缓冲实时响应
- 系统托盘常驻，关闭窗口后代理继续运行
- 请求日志（成功/失败状态指示 + 统计）
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

## 界面设计

采用顶部 Tab 导航布局，自定义标题栏可拖动：

- **顶部栏**：品牌 Logo + Tab 导航（服务商管理 / 系统设置 / 请求日志）+ 运行状态指示
- **服务商卡片**：顶部彩色条标识，6 色自动循环（橙/蓝/绿/紫/金/青）
- **模型行**：带图标的输入行，支持 1M 变体开关
- **空状态**：图标 + 引导文案 + 操作按钮
- **底部操作栏**：保存配置（幽灵按钮）+ 应用到 Claude Desktop（品牌色主按钮）

设计体系定义在 `Resources/Styles.xaml`：统一色彩、圆角（12/8/6/4 四级）、按钮样式（Primary/Success/Ghost/Small/Icon 五种）。

## 项目结构

```
claude model setting/
├── App.xaml / App.xaml.cs        # 入口：DI 容器、Serilog、全局异常处理、代理启动
├── Models/                       # 数据模型
│   ├── AppConfig.cs              # 配置根模型（providers 列表）
│   ├── Provider.cs               # 服务商（target_url, api_key, models[]）
│   ├── ModelEntry.cs             # 模型条目（name, to_1m）
│   ├── LogEntry.cs               # 请求日志
│   ├── ModelSlot.cs              # 8 个 Claude 模型槽位
│   ├── ResolvedModel.cs          # 解析后的目标模型
│   └── ApiResponse.cs            # API 响应封装
├── ViewModels/                   # MVVM ViewModel
│   ├── MainViewModel.cs          # 主 VM（Tab 导航、配置 CRUD、日志）
│   ├── ProviderViewModel.cs      # 服务商 VM（连接测试、模型管理、配色）
│   └── ModelEntryViewModel.cs    # 模型条目 VM（名称、1M 开关）
├── Views/                        # MVVM View
│   └── MainWindow.xaml / .cs     # 主窗口（顶部 Tab + 三页内容）
├── Converters/                   # WPF 值转换器
│   ├── IndexToVisibilityConverter.cs         # 导航索引 → 页面可见性
│   ├── InvertedBoolToVisibilityConverter.cs  # 布尔反转 → 可见性
│   └── CountToVisibilityConverter.cs         # 集合计数 → 可见性（支持 Invert）
├── Services/                     # 服务层（接口 + 实现）
│   ├── IConfigService / ConfigService            # JSON 配置读写（原子写入 + 锁）
│   ├── IModelResolverService / ModelResolverService  # 模型槽位映射
│   ├── IProxyServerService / ProxyServerService      # Kestrel 代理服务器
│   └── IClaudeDesktopService / ClaudeDesktopService  # Claude Desktop 配置 + 重启
├── Resources/                    # XAML 资源
│   └── Styles.xaml               # 设计体系（色彩、圆角、按钮、卡片、排版）
├── Helpers/                      # 工具类
│   ├── FileSystemHelper.cs       # 原子写入（临时文件 + 重命名）
│   ├── AutoStartHelper.cs        # 注册表开机自启动
│   └── TimeHelper.cs             # 时间格式化
└── Assets/                       # 资源文件
    └── tray_icon.ico             # 系统托盘图标
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
2. 点击「添加服务商」
3. 填写第三方 API 信息：
   - **API 地址**：如 `https://api.deepseek.com/anthropic`
   - **API 密钥**：第三方平台申请的 API Key
4. 点击「+ 添加模型」，填写模型名称
5. 点击「测试连接」验证（显示延迟和错误详情）
6. 点击「保存配置」
7. 点击「应用到 Claude Desktop」

### 第三步：开始使用

在 Claude Desktop 的模型选择器中选择配置的模型即可。

## 工作原理

```
Claude Desktop → http://127.0.0.1:5678 (本地代理) → 第三方 API
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
| 导航 | 左侧边栏 | 顶部 Tab 导航 |
| HTTP 服务器 | Axum | ASP.NET Core Kestrel |
| HTTP 客户端 | reqwest | HttpClient |
| JSON | serde | System.Text.Json |
| 系统托盘 | tray_icon crate | HandyControl NotifyIcon |
| 运行时 | 无（单文件编译） | 需要 .NET 8.0 Runtime |

## 免责声明

- 本软件基于 **抖音 Winhao学AI** 的原版 ModelLink 功能复刻
- **完全免费，不可商业化**
- 仅供学习和技术研究使用
