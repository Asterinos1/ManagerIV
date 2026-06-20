using GtaIVModLoader.Core;

namespace GtaIVModLoader.ViewModels;

/// <summary>
/// Wrapper ViewModel around StagedMod to bind to UI list items.
/// </summary>
public class ModViewModel : ViewModelBase
{
    private readonly StagedMod _mod;
    private bool _isEnabled;
    private int _priority;
    private DeployTarget _target;
    private string _conflictStatus = "";
    private bool _hasConflict;

    public ModViewModel(StagedMod mod, bool isEnabled, int priority, DeployTarget target)
    {
        _mod = mod;
        _isEnabled = isEnabled;
        _priority = priority;
        _target = target;
    }

    public string Id => _mod.Id;
    public string Name => _mod.Name;
    public string Version => _mod.Version;
    public string Compatibility => _mod.Compatibility;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public int Priority
    {
        get => _priority;
        set => SetProperty(ref _priority, value);
    }

    public DeployTarget Target
    {
        get => _target;
        set => SetProperty(ref _target, value);
    }

    public string ConflictStatus
    {
        get => _conflictStatus;
        set
        {
            if (SetProperty(ref _conflictStatus, value))
            {
                HasConflict = !string.IsNullOrEmpty(value);
            }
        }
    }

    public bool HasConflict
    {
        get => _hasConflict;
        set => SetProperty(ref _hasConflict, value);
    }

    public string TypeTag => Target.ToString();

    public StagedMod Model => _mod;
}
