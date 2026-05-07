namespace claude_model_setting.Services;

/// <summary>
/// 用户通知服务接口
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// 显示成功通知
    /// </summary>
    void Success(string message);

    /// <summary>
    /// 显示错误通知
    /// </summary>
    void Error(string message);
}
