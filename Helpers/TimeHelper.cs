namespace claude_model_setting.Helpers;

/// <summary>
/// 时间格式化辅助
/// </summary>
public static class TimeHelper
{
    /// <summary>
    /// 获取本地时间的 HH:mm:ss 格式字符串
    /// </summary>
    public static string FormatLocalTime()
    {
        return DateTime.Now.ToString("HH:mm:ss");
    }

    /// <summary>
    /// 获取带日期的完整时间格式
    /// </summary>
    public static string FormatFullTime()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
