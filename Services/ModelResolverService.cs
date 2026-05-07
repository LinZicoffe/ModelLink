using claude_model_setting.Models;

namespace claude_model_setting.Services;

/// <summary>
/// 模型槽位映射与解析服务
/// </summary>
public sealed class ModelResolverService : IModelResolverService
{
    /// <summary>
    /// 将配置展开为槽位列表
    /// </summary>
    public List<(string Slot, string Name, string To1m, string Url, string Key)> FlattenConfig(AppConfig config)
    {
        var result = new List<(string, string, string, string, string)>();
        var slotIdx = 0;

        foreach (var provider in config.Providers)
        {
            foreach (var m in provider.Models)
            {
                if (slotIdx < ModelSlot.MaxSlots && !string.IsNullOrEmpty(m.Name))
                {
                    result.Add((
                        ModelSlot.Slots[slotIdx],
                        m.Name,
                        m.To1m,
                        provider.TargetUrl,
                        provider.ApiKey));
                    slotIdx++;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 解析模型名称：支持 [1m] 后缀
    /// </summary>
    public ResolvedModel ResolveModel(string model, AppConfig config)
    {
        var is1m = model.EndsWith("[1m]");
        var baseName = is1m ? model[..^4] : model;

        foreach (var (slot, name, to1m, url, key) in FlattenConfig(config))
        {
            if (baseName == slot)
            {
                var resolvedName = is1m && !string.IsNullOrEmpty(to1m)
                    ? $"{name}[1m]"
                    : name;

                return new ResolvedModel
                {
                    Model = resolvedName,
                    TargetUrl = url,
                    ApiKey = key,
                };
            }
        }

        // 未找到匹配，返回原始模型名
        return new ResolvedModel
        {
            Model = model,
            TargetUrl = string.Empty,
            ApiKey = string.Empty,
        };
    }

    /// <summary>
    /// 获取合成模型列表（用于 GET /v1/models）
    /// </summary>
    public List<object> GetModelList(AppConfig config)
    {
        var models = new List<object>();
        foreach (var (slot, name, to1m, _, _) in FlattenConfig(config))
        {
            models.Add(new { id = slot, display_name = name, created_at = 0 });
            if (!string.IsNullOrEmpty(to1m))
            {
                models.Add(new { id = $"{slot}[1m]", display_name = $"{name}[1m]", created_at = 0 });
            }
        }
        return models;
    }
}
