using System.Collections.ObjectModel;
using System.Net.Http;
using claude_model_setting.Helpers;
using claude_model_setting.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace claude_model_setting.ViewModels;

/// <summary>
/// 单个 Provider 服务商视图模型
/// </summary>
public sealed partial class ProviderViewModel : ObservableObject
{
    [ObservableProperty]
    private string _targetUrl = string.Empty;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _testResult = string.Empty;

    [ObservableProperty]
    private bool _isTesting;

    [ObservableProperty]
    private int _index;

    [ObservableProperty]
    private bool _isKeyVisible;

    public ObservableCollection<ModelEntryViewModel> Models { get; } = [];

    public string DisplayTitle => $"服务商 {Index + 1}";
    public bool HasTestResult => !string.IsNullOrEmpty(TestResult);
    public bool IsTestSuccess => TestResult.StartsWith("连接成功");

    public ProviderViewModel() { }

    public ProviderViewModel(Provider provider)
    {
        TargetUrl = provider.TargetUrl;
        ApiKey = provider.ApiKey;
        foreach (var m in provider.Models)
        {
            Models.Add(new ModelEntryViewModel(m));
        }
    }

    partial void OnIndexChanged(int value)
    {
        OnPropertyChanged(nameof(DisplayTitle));
    }

    /// <summary>
    /// 转换为 Provider 模型
    /// </summary>
    public Provider ToProvider()
    {
        return new Provider
        {
            TargetUrl = TargetUrl,
            ApiKey = ApiKey,
            Models = Models.Select(m => m.ToModelEntry()).ToList(),
        };
    }

    [RelayCommand]
    private void AddModel()
    {
        Models.Add(new ModelEntryViewModel());
    }

    [RelayCommand]
    private void DeleteModel(ModelEntryViewModel model)
    {
        Models.Remove(model);
    }

    [RelayCommand]
    private async Task TestAsync()
    {
        if (IsTesting) return;
        IsTesting = true;
        TestResult = string.Empty;

        try
        {
            if (string.IsNullOrEmpty(TargetUrl) || string.IsNullOrEmpty(ApiKey))
            {
                TestResult = "请先填写 API 地址和密钥";
                return;
            }

            var model = Models.FirstOrDefault()?.ModelName ?? "test";
            var url = TargetUrl.TrimEnd('/') + "/v1/messages";
            var testBody = $$"""{"model":"{{model}}","max_tokens":1,"messages":[{"role":"user","content":"hi"}]}""";

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(testBody, System.Text.Encoding.UTF8, "application/json"),
            };
            req.Headers.Add("authorization", $"Bearer {ApiKey}");
            req.Headers.Add("anthropic-version", Constants.AnthropicVersion);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var resp = await client.SendAsync(req);
            sw.Stop();

            if (resp.IsSuccessStatusCode)
            {
                TestResult = $"连接成功 (延迟: {sw.ElapsedMilliseconds}ms)";
            }
            else
            {
                var body = await resp.Content.ReadAsStringAsync();
                var msg = TryExtractErrorMessage(body) ?? $"HTTP {(int)resp.StatusCode}";
                TestResult = $"连接失败: {msg}";
            }
        }
        catch (HttpRequestException)
        {
            TestResult = "连接失败: 无法连接，请检查 URL";
        }
        catch (TaskCanceledException)
        {
            TestResult = "连接失败: 请求超时";
        }
        catch (Exception ex)
        {
            TestResult = $"连接失败: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
            OnPropertyChanged(nameof(HasTestResult));
            OnPropertyChanged(nameof(IsTestSuccess));
        }
    }

    /// <summary>
    /// 从 API 错误响应体中提取 error.message 字段
    /// </summary>
    private static string? TryExtractErrorMessage(string body)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("message", out var message))
                    return message.GetString();
                return error.GetString();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
}
