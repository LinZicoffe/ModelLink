using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using claude_model_setting.Helpers;
using claude_model_setting.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog;

namespace claude_model_setting.Services;

/// <summary>
/// 自宿主 Kestrel 代理服务器
/// </summary>
public sealed class ProxyServerService : IProxyServerService
{
    private const int Port = 5678;
    private const int MaxLogs = 100;

    private readonly IConfigService _configService;
    private readonly IModelResolverService _resolverService;
    private readonly IClaudeDesktopService _claudeDesktopService;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentQueue<LogEntry> _logs = new();
    private CancellationTokenSource _cts = new();
    private WebApplication? _app;

    public ProxyServerService(
        IConfigService configService,
        IModelResolverService resolverService,
        IClaudeDesktopService claudeDesktopService)
    {
        _configService = configService;
        _resolverService = resolverService;
        _claudeDesktopService = claudeDesktopService;

        _httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
        })
        {
            Timeout = TimeSpan.FromSeconds(300),
        };
    }

    /// <summary>
    /// 启动代理服务器
    /// </summary>
    public async Task StartAsync()
    {
        await _configService.LoadConfigAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
        });
        builder.WebHost.UseUrls($"http://127.0.0.1:{Port}");
        builder.Logging.ClearProviders();

        var app = builder.Build();
        _app = app;

        // 注册 API 路由
        app.MapGet("/api/config", HandleGetConfig);
        app.MapPost("/api/config", HandleSaveConfig);
        app.MapPost("/api/test", HandleTest);
        app.MapPost("/api/apply", HandleApply);
        app.MapGet("/api/logs", HandleGetLogs);
        app.MapGet("/api/autostart", HandleGetAutostart);
        app.MapPost("/api/autostart", HandleSetAutostart);

        // 代理回退
        app.MapFallback(ProxyFallback);

        _ = Task.Run(async () =>
        {
            try
            {
                await app.RunAsync();
            }
            catch (OperationCanceledException) { /* 正常关闭 */ }
            catch (Exception ex)
            {
                Log.Error(ex, "代理服务器异常退出");
            }
        });

        Log.Information("代理服务器已启动，监听 127.0.0.1:{Port}", Port);
    }

    /// <summary>
    /// 停止代理服务器
    /// </summary>
    public async Task StopAsync()
    {
        _cts.Cancel();
        if (_app != null)
        {
            await _app.StopAsync();
        }
        Log.Information("代理服务器已停止");
    }

    public List<LogEntry> GetLogs() => [.. _logs];

    #region API 路由处理

    private async Task HandleGetConfig(HttpContext context)
    {
        var json = JsonSerializer.Serialize(_configService.CurrentConfig);
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(json);
    }

    private async Task HandleSaveConfig(HttpContext context)
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        var config = JsonSerializer.Deserialize<AppConfig>(body) ?? new AppConfig();
        await _configService.SaveConfigAsync(config);
        await WriteJson(context, ApiResponse.Success("配置已保存"));
    }

    private async Task HandleTest(HttpContext context)
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var targetUrl = root.GetProperty("target_url").GetString() ?? "";
        var apiKey = root.GetProperty("api_key").GetString() ?? "";
        var model = root.TryGetProperty("model", out var modelEl) ? modelEl.GetString() ?? "" : "";

        try
        {
            var url = targetUrl.TrimEnd('/') + "/v1/messages";
            var testBody = JsonSerializer.Serialize(new
            {
                model = string.IsNullOrEmpty(model) ? "test" : model,
                max_tokens = 1,
                messages = new[] { new { role = "user", content = "hi" } },
            });

            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(testBody, Encoding.UTF8, "application/json"),
            };
            req.Headers.Add("x-api-key", apiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");

            using var resp = await _httpClient.SendAsync(req);
            await WriteJson(context, resp.IsSuccessStatusCode
                ? ApiResponse.Success($"连接成功 (HTTP {(int)resp.StatusCode})")
                : ApiResponse.Error($"连接失败 (HTTP {(int)resp.StatusCode})"));
        }
        catch (Exception ex)
        {
            await WriteJson(context, ApiResponse.Error($"连接失败: {ex.Message}"));
        }
    }

    private async Task HandleApply(HttpContext context)
    {
        try
        {
            var msg = await _claudeDesktopService.ApplyToClaudeDesktopAsync();
            await WriteJson(context, ApiResponse.Success(msg));
        }
        catch (Exception ex)
        {
            await WriteJson(context, ApiResponse.Error(ex.Message));
        }
    }

    private async Task HandleGetLogs(HttpContext context)
    {
        await WriteJson(context, GetLogs());
    }

    private async Task HandleGetAutostart(HttpContext context)
    {
        await WriteJson(context, new { enabled = AutoStartHelper.IsEnabled() });
    }

    private async Task HandleSetAutostart(HttpContext context)
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        var doc = JsonDocument.Parse(body);
        var enabled = doc.RootElement.GetProperty("enabled").GetBoolean();
        AutoStartHelper.SetEnabled(enabled);
        await WriteJson(context, ApiResponse.Success(enabled ? "已启用自启动" : "已禁用自启动"));
    }

    #endregion

    #region 代理转发

    private async Task ProxyFallback(HttpContext context)
    {
        // GET /v1/models → 返回合成模型列表
        if (context.Request.Method == "GET" && context.Request.Path.StartsWithSegments("/v1/models"))
        {
            var models = _resolverService.GetModelList(_configService.CurrentConfig);
            await WriteJson(context, new { data = models });
            return;
        }

        // 非 POST 请求返回 404
        if (context.Request.Method != "POST")
        {
            context.Response.StatusCode = 404;
            return;
        }

        // POST 请求 → 代理转发
        await ProxyPostRequest(context);
    }

    private async Task ProxyPostRequest(HttpContext context)
    {
        using var reader = new StreamReader(context.Request.Body);
        var bodyJson = await reader.ReadToEndAsync();

        try
        {
            var doc = JsonNode.Parse(bodyJson);
            var model = doc?["model"]?.ToString() ?? "";
            var resolved = _resolverService.ResolveModel(model, _configService.CurrentConfig);

            if (string.IsNullOrEmpty(resolved.TargetUrl))
            {
                context.Response.StatusCode = 502;
                await context.Response.WriteAsync($"{{\"error\":\"未找到模型映射: {model}\"}}");
                return;
            }

            doc!["model"] = resolved.Model;
            var bodyToSend = doc.ToJsonString();

            var upstreamUrl = resolved.TargetUrl.TrimEnd('/') + context.Request.Path;
            var upstreamReq = new HttpRequestMessage(HttpMethod.Post, upstreamUrl)
            {
                Content = new StringContent(bodyToSend, Encoding.UTF8, "application/json"),
            };

            if (!string.IsNullOrEmpty(resolved.ApiKey))
            {
                upstreamReq.Headers.Add("x-api-key", resolved.ApiKey);
                upstreamReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resolved.ApiKey);
            }

            if (context.Request.Headers.TryGetValue("anthropic-version", out var version))
                upstreamReq.Headers.Add("anthropic-version", version.ToString());
            if (context.Request.Headers.TryGetValue("anthropic-beta", out var beta))
                upstreamReq.Headers.Add("anthropic-beta", beta.ToString());

            using var upstreamResp = await _httpClient.SendAsync(upstreamReq, HttpCompletionOption.ResponseHeadersRead);

            AddLog(model, (int)upstreamResp.StatusCode);

            context.Response.StatusCode = (int)upstreamResp.StatusCode;
            foreach (var header in upstreamResp.Headers)
            {
                if (header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase))
                    continue;
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
            foreach (var header in upstreamResp.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            var stream = await upstreamResp.Content.ReadAsStreamAsync();
            await stream.CopyToAsync(context.Response.Body);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "代理转发失败");
            context.Response.StatusCode = 502;
            await context.Response.WriteAsync($"{{\"error\":\"代理转发失败: {ex.Message}\"}}");
        }
    }

    #endregion

    #region 辅助方法

    private void AddLog(string model, int status)
    {
        _logs.Enqueue(new LogEntry
        {
            Time = TimeHelper.FormatLocalTime(),
            Model = model,
            Status = status,
        });
        while (_logs.Count > MaxLogs)
            _logs.TryDequeue(out _);
    }

    private static async Task WriteJson(HttpContext context, object data)
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(data));
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _httpClient.Dispose();
        _cts.Dispose();
    }

    #endregion
}
