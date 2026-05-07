using claude_model_setting.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace claude_model_setting.ViewModels;

/// <summary>
/// 单个模型条目视图模型
/// </summary>
public sealed partial class ModelEntryViewModel : ObservableObject
{
    [ObservableProperty]
    private string _modelName = string.Empty;

    [ObservableProperty]
    private bool _is1mEnabled;

    public ModelEntryViewModel() { }

    public ModelEntryViewModel(ModelEntry entry)
    {
        ModelName = entry.Name;
        Is1mEnabled = !string.IsNullOrEmpty(entry.To1m);
    }

    /// <summary>
    /// 转换为 ModelEntry 模型
    /// </summary>
    public ModelEntry ToModelEntry()
    {
        return new ModelEntry
        {
            Name = ModelName,
            To1m = Is1mEnabled ? $"{ModelName}-1m" : string.Empty,
        };
    }
}
