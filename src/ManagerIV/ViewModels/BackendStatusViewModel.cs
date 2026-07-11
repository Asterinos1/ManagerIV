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

    private string _asiLoaderVersion = "Unknown";
    private string _fusionFixVersion = "Unknown";
    private string _dxvkVersion = "Unknown";

    private string _asiLoaderLatest = "Fetching...";
    private string _fusionFixLatest = "Fetching...";
    private string _dxvkLatest = "Fetching...";

    public BackendStatusViewModel()
    {
    }

    public BackendStatusViewModel(
        bool asiLoader, string asiLoaderVersion,
        bool fusionFix, string fusionFixVersion,
        bool dxvk, string dxvkVersion,
        bool scriptHook,
        bool memBiter = false,
        bool bassAudio = false)
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

    public string FusionFixButtonText => _fusionFixInstalled ? "Update" : "Install";
    public string AsiLoaderButtonText => _asiLoaderInstalled ? "Update" : "Install";
    public string DxvkButtonText => _dxvkInstalled ? "Update" : "Install";
    public string ScriptHookButtonText => _scriptHookInstalled ? "Update" : "Install";
    public string MemBiterButtonText => _memBiterInstalled ? "Update" : "Install";
    public string BassAudioButtonText => _bassAudioInstalled ? "Update" : "Install";
}
