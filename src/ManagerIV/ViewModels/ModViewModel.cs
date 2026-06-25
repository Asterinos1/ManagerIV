using ManagerIV.Core;

namespace ManagerIV.ViewModels;

/// <summary>
/// Wrapper ViewModel around StagedMod to bind to UI list items.
/// </summary>
public class ModViewModel : ViewModelBase
{
    private StagedMod _mod;
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

    public string Name
    {
        get => _mod.Name;
        set
        {
            if (_mod.Name != value)
            {
                _mod = _mod with { Name = value };
                OnPropertyChanged(nameof(Name));
            }
        }
    }

    public string DisplayName
    {
        get => string.IsNullOrEmpty(_mod.DisplayName) ? _mod.Name : _mod.DisplayName;
        set
        {
            if (_mod.DisplayName != value)
            {
                _mod = _mod with { DisplayName = value };
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public IReadOnlyList<string> Tags
    {
        get => _mod.Tags ?? new System.Collections.Generic.List<string>();
        set
        {
            if (_mod.Tags != value)
            {
                _mod = _mod with { Tags = value };
                OnPropertyChanged(nameof(Tags));
            }
        }
    }

    public string Version
    {
        get => _mod.Version;
        set
        {
            if (_mod.Version != value)
            {
                _mod = _mod with { Version = value };
                OnPropertyChanged(nameof(Version));
            }
        }
    }

    public string Compatibility
    {
        get => _mod.Compatibility;
        set
        {
            if (_mod.Compatibility != value)
            {
                _mod = _mod with { Compatibility = value };
                OnPropertyChanged(nameof(Compatibility));
            }
        }
    }

    public string Description
    {
        get => _mod.Description;
        set
        {
            if (_mod.Description != value)
            {
                _mod = _mod with { Description = value };
                OnPropertyChanged(nameof(Description));
            }
        }
    }

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

    private string? _conflictDetails;

    public string? ConflictDetails
    {
        get => _conflictDetails;
        set => SetProperty(ref _conflictDetails, value);
    }

    public string TypeTag => Target.ToString();

    public StagedMod Model => _mod;
}
