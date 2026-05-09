namespace claude_model_setting.Models;

/// <summary>
/// 汉化状态
/// </summary>
public enum PatchStatus
{
    /// <summary>Claude Desktop 未安装</summary>
    NotInstalled,
    /// <summary>已安装但未汉化</summary>
    Unpatched,
    /// <summary>已汉化</summary>
    Patched
}
