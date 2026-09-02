using ManagerIV.Core;

namespace ManagerIV.ViewModels;

public class BackendStatusViewModel : ViewModelBase
{
    private bool _asiLoaderInstalled;
    private bool _fusionFixInstalled;
    private bool _dxvkInstalled;
    private bool _scriptHookInstalled;
    private bool _memBiterInstalled;
    private bool _bassAudioInstalled;
    private TrainerStatus _libertyTrainerStatus = TrainerStatus.Missing;

    private string _asiLoaderVersion = "Unknown";
    private string _fusionFixVersion = "Unknown";
    private string _dxvkVersion = "Unknown";
    private string _libertyTrainerVersion = "Unknown";

    private string _asiLoaderLatest = "Fetching...";
    private string _fusionFixLatest = "Fetching...";
    private string _dxvkLatest = "Fetching...";
    private string _libertyTrainerLatest = "v2.4.1";

    public BackendStatusViewModel()
    {
    }

    public BackendStatusViewModel(
        bool asiLoader, string asiLoaderVersion,
        bool fusionFix, string fusionFixVersion,
        bool dxvk, string dxvkVersion,
        bool scriptHook,
        bool memBiter = false,
        bool bassAudio = false,
        TrainerStatus libertyTrainer = TrainerStatus.Missing,
        string libertyTrainerVersion = "Unknown")
    {
        _asiLoaderInstalled = asiLoader;
        _asiLoaderVersion = asiLoaderVersion;
        _fusionFixInstalled = fusionFix;
        _fusionFixVersion = fusionFixVersion;
        _dxvkInstalled = dxvk;
        _dxvkVersion = dxvkVersion;
        _scriptHookInstalled = scriptHook;
        _memBiterInstalled = memBiter;
        _bassAudioInstalled = bassAudio;
        _libertyTrainerStatus = libertyTrainer;
        _libertyTrainerVersion = libertyTrainerVersion;
    }

    public bool AsiLoaderInstalled => _asiLoaderInstalled;
    public string AsiLoaderText => _asiLoaderInstalled ? "Installed" : "Missing";
    public string AsiLoaderBrush => _asiLoaderInstalled ? "#FF107C41" : "#FF8A0A0A"; // Green vs Red
    public string AsiLoaderVersion => _asiLoaderVersion;
    
    public string AsiLoaderLatest
    {
        get => _asiLoaderLatest;
        set
        {
            if (SetProperty(ref _asiLoaderLatest, value))
            {
                OnPropertyChanged(nameof(AsiLoaderVersionInfo));
            }
        }
    }

    public string AsiLoaderVersionInfo => _asiLoaderInstalled 
        ? $"Installed: {_asiLoaderVersion} | Latest: {_asiLoaderLatest}"
        : $"Latest available: {_asiLoaderLatest}";

    public bool FusionFixInstalled => _fusionFixInstalled;
    public string FusionFixText => _fusionFixInstalled ? "Installed" : "Missing";
    public string FusionFixBrush => _fusionFixInstalled ? "#FF107C41" : "#FF8A0A0A";
    public string FusionFixVersion => _fusionFixVersion;

    public string FusionFixLatest
    {
        get => _fusionFixLatest;
        set
        {
            if (SetProperty(ref _fusionFixLatest, value))
            {
                OnPropertyChanged(nameof(FusionFixVersionInfo));
            }
        }
    }

    public string FusionFixVersionInfo => _fusionFixInstalled 
        ? $"Installed: {_fusionFixVersion} | Latest: {_fusionFixLatest}"
        : $"Latest available: {_fusionFixLatest}";

    public bool DxvkInstalled => _dxvkInstalled;
    public string DxvkText => _dxvkInstalled ? "Installed" : "Missing";
    public string DxvkBrush => _dxvkInstalled ? "#FF107C41" : "#FF8A0A0A";
    public string DxvkVersion => _dxvkVersion;

    public string DxvkLatest
    {
        get => _dxvkLatest;
        set
        {
            if (SetProperty(ref _dxvkLatest, value))
            {
                OnPropertyChanged(nameof(DxvkVersionInfo));
            }
        }
    }

    public string DxvkVersionInfo => _dxvkInstalled 
        ? $"Installed: {_dxvkVersion} | Latest: {_dxvkLatest}"
        : $"Latest available: {_dxvkLatest}";

    public bool ScriptHookInstalled => _scriptHookInstalled;
    public string ScriptHookText => _scriptHookInstalled ? "Installed" : "Missing";
    public string ScriptHookBrush => _scriptHookInstalled ? "#FF107C41" : "#FF8A0A0A";

    public bool MemBiterInstalled => _memBiterInstalled;
    public string MemBiterText => _memBiterInstalled ? "Installed" : "Missing";
    public string MemBiterBrush => _memBiterInstalled ? "#FF107C41" : "#FF8A0A0A";

    public bool BassAudioInstalled => _bassAudioInstalled;
    public string BassAudioText => _bassAudioInstalled ? "Installed" : "Missing";
    public string BassAudioBrush => _bassAudioInstalled ? "#FF107C41" : "#FF8A0A0A";

    public TrainerStatus LibertyTrainerStatus => _libertyTrainerStatus;
    public bool LibertyTrainerInstalled => _libertyTrainerStatus == TrainerStatus.Installed;
    public bool LibertyTrainerRepairNeeded => _libertyTrainerStatus == TrainerStatus.RepairNeeded;
    public bool LibertyTrainerCanUninstall => _libertyTrainerStatus == TrainerStatus.Installed || _libertyTrainerStatus == TrainerStatus.RepairNeeded;

    public string LibertyTrainerText => _libertyTrainerStatus switch
    {
        TrainerStatus.Installed => "Installed",
        TrainerStatus.RepairNeeded => "Repair needed",
        _ => "Missing"
    };

    public string LibertyTrainerBrush => _libertyTrainerStatus switch
    {
        TrainerStatus.Installed => "#FF107C41",     // Green
        TrainerStatus.RepairNeeded => "#FFF3A813",  // Warning Orange
        _ => "#FF8A0A0A"                            // Red
    };

    public string LibertyTrainerLatest
    {
        get => _libertyTrainerLatest;
        set
        {
            if (SetProperty(ref _libertyTrainerLatest, value))
            {
                OnPropertyChanged(nameof(LibertyTrainerVersionInfo));
            }
        }
    }

    public string LibertyTrainerVersionInfo => _libertyTrainerStatus switch
    {
        TrainerStatus.Installed => !string.IsNullOrEmpty(_libertyTrainerVersion) && _libertyTrainerVersion != "Unknown"
            ? $"Installed: {_libertyTrainerVersion} | Latest: {_libertyTrainerLatest} (GTAForums)"
            : $"Installed: Complete Edition | Latest: {_libertyTrainerLatest} (GTAForums)",
        TrainerStatus.RepairNeeded => "Incomplete installation (missing ASI or companion folder)",
        _ => $"Not installed (Latest available: {_libertyTrainerLatest} by const96b on GTAForums)"
    };

    public string FusionFixButtonText => _fusionFixInstalled ? "Update" : "Install";
    public string AsiLoaderButtonText => _asiLoaderInstalled ? "Update" : "Install";
    public string DxvkButtonText => _dxvkInstalled ? "Update" : "Install";
    public string ScriptHookButtonText => _scriptHookInstalled ? "Update" : "Install";
    public string MemBiterButtonText => _memBiterInstalled ? "Update" : "Install";
    public string BassAudioButtonText => _bassAudioInstalled ? "Update" : "Install";

    public string LibertyTrainerButtonText => _libertyTrainerStatus switch
    {
        TrainerStatus.Installed => "Update",
        TrainerStatus.RepairNeeded => "Repair",
        _ => "Get Trainer"
    };
}

