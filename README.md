# ModelLink (C# WPF 版)

> **本软件完全免费，仅供个人学习和非商业用途。严禁任何形式的商业化行为。**
>
> 原作者：**抖音 Winhao学AI**（抖音号：54927876676）
>        原项目地址：**https://github.com/Win-Hao/ModelLink**
>
> C# WPF 版本基于原 Rust 版本功能复刻，使用 WPF + HandyControl 原生界面替代 WebView。

让 Claude Desktop 桌面端接入任意第三方 API 模型的本地代理工具。

## 下载安装

前往 [Releases 页面](https://github.com/LinZicoffe/ModelLink/releases) 下载最新版安装包。

安装包为自包含部署，无需额外安装 .NET 运行时。

## 功能

- 将第三方模型（DeepSeek、Kimi、智谱 GLM 等）接入 Claude Desktop
- 支持同时配置多个 API 服务商，每个服务商独立配色标识
- 支持 1M 上下文模型变体
- **Claude Desktop 一键汉化**：将界面切换为中文，支持汉化和恢复
- 原生 WPF 界面（HandyControl），无需 WebView 运行时
- 顶部 Tab 导航（服务商管理 / 系统设置 / 请求日志 / 界面汉化）
- 连接测试（延迟检测 + 错误信息提取）
- SSE 流式转发，零缓冲实时响应
- 系统托盘常驻，关闭窗口后代理继续运行
- 请求日志（成功/失败状态指示 + 统计）
- 开机自启动
- 智能进程管理：精确识别 Anthropic Claude Desktop 进程，避免误杀同名进程（如 Claude Code）

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

- **顶部栏**：品牌 Logo + Tab 导航（服务商管理 / 系统设置 / 请求日志 / 界面汉化）+ 运行状态指示
- **服务商卡片**：顶部彩色条标识，6 色自动循环（橙/蓝/绿/紫/金/青）
- **模型行**：带图标的输入行，支持 1M 变体开关
- **界面汉化**：检测 Claude Desktop 安装状态，一键汉化/恢复，自动备份原始文件
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
│   ├── ApiResponse.cs            # API 响应封装
│   ├── ClaudeInstallation.cs     # Claude Desktop 安装信息（路径、版本、安装类型）
│   └── PatchStatus.cs            # 汉化状态枚举（NotInstalled/Unpatched/Patched）
├── ViewModels/                   # MVVM ViewModel
│   ├── MainViewModel.cs          # 主 VM（Tab 导航、配置 CRUD、日志、汉化）
│   ├── ProviderViewModel.cs      # 服务商 VM（连接测试、模型管理）
│   ├── ModelEntryViewModel.cs    # 模型条目 VM（名称、1M 开关）
│   └── LocalizationViewModel.cs  # 汉化 VM（安装检测、汉化/恢复操作、状态管理）
├── Views/                        # MVVM View
│   └── MainWindow.xaml / .cs     # 主窗口（顶部 Tab + 四页内容）
├── Converters/                   # WPF 值转换器
│   ├── IndexToBrushConverter.cs             # 索引 → 服务商配色画刷
│   ├── InvertedBoolToVisibilityConverter.cs # 布尔反转 → 可见性
│   └── CountToVisibilityConverter.cs        # 集合计数 → 可见性（支持 Invert）
├── Services/                     # 服务层（接口 + 实现）
│   ├── IConfigService / ConfigService                # JSON 配置读写（原子写入 + 锁）
│   ├── IModelResolverService / ModelResolverService  # 模型槽位映射
│   ├── IProxyServerService / ProxyServerService      # Kestrel 代理服务器
│   ├── IClaudeDesktopService / ClaudeDesktopService  # Claude Desktop 配置 + 智能重启
│   ├── INotificationService / NotificationService    # UI 通知抽象（Growl 封装）
│   ├── IBackupService / BackupService                # Claude Desktop 文件备份/还原
│   └── ILocalizationService / LocalizationService    # Claude Desktop 汉化（检测、注入、恢复）
├── Resources/                    # XAML 资源 + 嵌入式翻译
│   ├── Styles.xaml               # 设计体系（色彩、圆角、按钮、卡片、排版）
│   ├── zh-CN.json                # ion-dist/i18n 中文翻译
│   ├── desktop-zh-CN.json        # resources/zh-CN.json 中文翻译
│   └── statsig-zh-CN.json        # statsig 中文翻译
├── Helpers/                      # 工具类
│   ├── Constants.cs              # 全局共享常量（端口、版本号、URL）
│   ├── FileSystemHelper.cs       # 原子写入（临时文件 + 重命名）
│   ├── AutoStartHelper.cs        # 注册表开机自启动
│   └── TimeHelper.cs             # 时间格式化
├── GenIcon.cs                    # 托盘图标生成工具（不参与编译）
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

### 第三步：界面汉化（可选）

1. 切换到「界面汉化」Tab 页
2. 程序自动检测 Claude Desktop 安装状态和汉化状态
3. 需要以**管理员身份**运行 ModelLink 才能执行汉化
4. 点击「一键汉化」执行汉化操作（自动备份原始文件）
5. 如需恢复，点击「一键恢复」

汉化流程：
1. 关闭 Claude Desktop
2. 获取文件权限（MSIX 安装目录需要提升权限）
3. 备份原始 `index-*.js` 文件
4. 写入翻译文件（zh-CN.json、desktop-zh-CN.json、statsig-zh-CN.json）
5. 注入语言白名单到 index-*.js
6. 设置语言配置（locale: zh-CN）
7. 验证汉化结果
8. 重启 Claude Desktop

### 第四步：开始使用

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
- 汉化备份：`%LOCALAPPDATA%\ClaudeCN\backups\`

## 与原 Rust 版本的差异

| 项目 | Rust 原版 | C# WPF 版 |
|------|-----------|-----------|
| UI | WRY/WebView + HTML | WPF + HandyControl |
| 导航 | 左侧边栏 | 顶部 Tab 导航 |
| HTTP 服务器 | Axum | ASP.NET Core Kestrel |
| HTTP 客户端 | reqwest | HttpClient |
| JSON | serde | System.Text.Json |
| 系统托盘 | tray_icon crate | HandyControl NotifyIcon |
| 汉化功能 | 无 | 一键汉化/恢复（支持 MSIX 和 EXE 安装） |
| 运行时 | 无（单文件编译） | 需要 .NET 8.0 Runtime |

## 构建与打包

需要 .NET 8.0 SDK 和 [Inno Setup 6](https://jrsoftware.org/isdl.php)。

```bash
# 1. 自包含发布（输出到 publish_output/）
dotnet publish "claude model setting/claude model setting.csproj" -c Release -r win-x64 --self-contained true -o publish_output

# 2. 编译安装包（输出到 Output/）
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" setup.iss
```

安装包版本号在 `setup.iss` 顶部 `#define MyAppVersion` 处修改。

## 许可证

本项目采用 **CC BY-NC-ND 4.0**（署名-非商业性使用-禁止演绎）许可证，详见 [LICENSE](LICENSE)。

- **署名**：使用时须保留原作者信息
- **非商业性使用**：不得用于任何商业目的
- **禁止演绎**：不得修改后再分发（防止去除水印后转卖）

本软件基于 **抖音 Winhao学AI** 的原版 ModelLink 功能复刻。
