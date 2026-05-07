namespace claude_model_setting.Services;

/// <summary>
/// 基于 HandyControl Growl 的通知实现
/// </summary>
public sealed class NotificationService : INotificationService
{
    public void Success(string message) => HandyControl.Controls.Growl.Success(message);

    public void Error(string message) => HandyControl.Controls.Growl.Error(message);
}
