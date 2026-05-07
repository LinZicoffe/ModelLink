using claude_model_setting.Models;

namespace claude_model_setting.Services;

/// <summary>
/// 模型槽位映射与解析服务接口
/// </summary>
public interface IModelResolverService
{
    /// <summary>
    /// 将配置展开为槽位列表: (slot, name, to1m, url, key)
    /// </summary>
    List<(string Slot, string Name, string To1m, string Url, string Key)> FlattenConfig(AppConfig config);

    /// <summary>
    /// 解析模型名称：支持 [1m] 后缀
    /// </summary>
    ResolvedModel ResolveModel(string model, AppConfig config);

    /// <summary>
    /// 获取合成模型列表（用于 GET /v1/models）
    /// </summary>
    List<object> GetModelList(AppConfig config);
}
